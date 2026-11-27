#!/bin/bash
# Tests of watch-errors.sh without a server: fake `curl`, fake `docker` and a fake alert.sh answer from files, so that every branch is run.
# Run (Git Bash / Linux):  bash deploy/ops/test-watch-errors.sh      Exit code 0 = all tests passed.
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
SCRIPT="$HERE/watch-errors.sh"
T=$(mktemp -d)
trap 'rm -rf "$T"' EXIT
mkdir -p "$T/bin" "$T/fake" "$T/state"

# --- the shims -------------------------------------------------------------------------------------------------------------------
cat > "$T/bin/curl" <<'EOF'
#!/bin/bash
# prints the code from $FAKE/curl_code (default 200)
cat "$FAKE/curl_code" 2>/dev/null || echo 200
EOF
cat > "$T/bin/docker" <<'EOF'
#!/bin/bash
# docker logs --since 5m NAME  -> $FAKE/logs_NAME ; docker inspect -f FORMAT NAME -> $FAKE/inspect_NAME ; $FAKE/docker_down makes docker fail
[ -f "$FAKE/docker_down" ] && exit 1
case "$1" in
  logs) cat "$FAKE/logs_${4}" 2>/dev/null ;;
  inspect) cat "$FAKE/inspect_${4}" 2>/dev/null || exit 1 ;;
esac
EOF
cat > "$T/alert.sh" <<'EOF'
#!/bin/bash
echo "$1" >> "$FAKE/alerts.txt"
EOF
chmod +x "$T/bin/curl" "$T/bin/docker" "$T/alert.sh"

export FAKE="$T/fake"
export PATH="$T/bin:$PATH"
export JUMBO_ALERT="$T/alert.sh"
export JUMBO_WATCH_STATE="$T/state"
export JUMBO_WATCH_LOG="$T/watch.log"
export JUMBO_BASE_URL="https://example.invalid"

pass=0; failn=0
ok()   { pass=$((pass + 1)); echo "  ok    $1"; }
bad()  { failn=$((failn + 1)); echo "  FAIL  $1"; }
run()  { bash "$SCRIPT"; }
alerts() { cat "$FAKE/alerts.txt" 2>/dev/null | wc -l | tr -d ' '; }
last()   { tail -1 "$FAKE/alerts.txt" 2>/dev/null; }
reset() { rm -rf "$FAKE"/* "$T/state"/*; for c in jumbo-api jumbo-web jumbo-nginx jumbo-postgres; do echo "0|false|2026-11-27T00:00:00Z" > "$FAKE/inspect_$c"; done; }
expect_alerts() { # NAME COUNT
  local n; n=$(alerts)
  if [ "$n" = "$2" ]; then ok "$1"; else bad "$1 (alerts: $n, expected $2) last: $(last)"; fi
}
expect_last_has() { # NAME TEXT
  if last | grep -qF -- "$2"; then ok "$1"; else bad "$1 (last alert: $(last))"; fi
}

nginx_lines() { # GOOD BAD : writes a nginx-like log with GOOD 200s and BAD 502s on /_blazor/negotiate?x=SECRET
  : > "$FAKE/logs_jumbo-nginx"
  local i
  for ((i = 0; i < $1; i++)); do echo '10.0.0.1 - - [27/Nov/2026:06:35:53 +0000] "GET /icons/a.svg HTTP/1.1" 200 316 "https://jumbo.uz/" "Mozilla/5.0" "1.2.3.4"' >> "$FAKE/logs_jumbo-nginx"; done
  for ((i = 0; i < $2; i++)); do echo '10.0.0.1 - - [27/Nov/2026:06:35:54 +0000] "POST /_blazor/negotiate?token=SECRETSECRETSECRETSECRET1234567890 HTTP/1.1" 502 150 "https://jumbo.uz/" "Mozilla/5.0" "1.2.3.4"' >> "$FAKE/logs_jumbo-nginx"; done
}

echo "1. everything is fine"
reset; nginx_lines 100 0; run; run
expect_alerts "no alert on a healthy site" 0

echo "2. the page with the database does not answer"
reset; echo 500 > "$FAKE/curl_code"
run; expect_alerts "one bad check is not reported (a restart takes seconds)" 0
run; expect_alerts "two bad checks in a row are reported" 1
expect_last_has "the message says the code" "500"
run; expect_alerts "no second message while it lasts" 1
echo 200 > "$FAKE/curl_code"; run
expect_alerts "recovered message" 2
expect_last_has "it says it is fine again" "✅"
run; expect_alerts "nothing more after recovery" 2
echo 000 > "$FAKE/curl_code"; run; echo 200 > "$FAKE/curl_code"; run; run
expect_alerts "a single bad check between good ones is forgotten" 2

echo "3. 5xx in the nginx log"
reset; nginx_lines 100 3; run; run
expect_alerts "3 of 103 is below the threshold" 0
reset; nginx_lines 100 12; run
expect_alerts "12 of 112 is not reported at the first window (deploy burst)" 0
run; expect_alerts "reported when the next window is bad too" 1
expect_last_has "the failing path is listed" "502 /_blazor/negotiate ×12"
if grep -q "SECRET" "$FAKE/alerts.txt"; then bad "the query string with a token leaked into the message"; else ok "the query string (token) is not in the message"; fi
run; expect_alerts "no second message in the same hour" 1
JUMBO_WATCH_REPEAT=0 run; expect_alerts "a reminder when the repeat time has passed" 2
expect_last_has "the reminder says it still lasts" "всё ещё"
nginx_lines 100 0; run
expect_last_has "recovered message for 5xx" "✅"
reset; nginx_lines 3 3; run; run
expect_alerts "6 errors of 6 requests but under 5 errors in total is quiet (3 < 5)" 0
reset; nginx_lines 1000 20; run; run
expect_alerts "20 of 1020 is under 5 % and is quiet" 0

echo "4. a container restarted itself"
reset; run
expect_alerts "the first run only learns the counters" 0
echo "2|false|2026-11-27T01:00:00Z" > "$FAKE/inspect_jumbo-api"; run
expect_alerts "a restart counter that went up is reported" 1
expect_last_has "it names the container" "jumbo-api"
run; expect_alerts "reported once" 1
echo "2|true|2026-11-27T02:00:00Z" > "$FAKE/inspect_jumbo-web"; run
expect_last_has "out of memory is reported" "нехватк"
n=$(alerts); run; expect_alerts "out of memory is reported once per start" "$n"

echo "5. errors in the logs of the API"
reset
printf 'info: Jumbo.Api[0]\n      started\nfail: Microsoft.EntityFrameworkCore.Query[10100]\n      An exception occurred for anna.petrova@example.com id 1234567890 token AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n' > "$FAKE/logs_jumbo-api"
run
expect_alerts "an error line is reported at once" 1
expect_last_has "it says which log" "jumbo-api"
expect_last_has "the category is there" "Microsoft.EntityFrameworkCore.Query"
for secret in "anna.petrova@example.com" "1234567890" "AAAAAAAAAAAAAAAA"; do
  if last | grep -qF -- "$secret"; then bad "'$secret' leaked into the message"; else ok "'$secret' is masked"; fi
done
run; expect_alerts "no repeat in the same hour" 1
: > "$FAKE/logs_jumbo-api"; run
expect_last_has "recovered message for the log" "✅"
printf 'crit: Jumbo.Web[0]\n      Something broke\n' > "$FAKE/logs_jumbo-web"; run
expect_last_has "the website log is watched too" "jumbo-web"

echo "6. docker not answering"
reset; touch "$FAKE/docker_down"; run; run
expect_alerts "no crash and no false alarm when docker does not answer" 0

echo
echo "passed: $pass, failed: $failn"
[ "$failn" -eq 0 ]
