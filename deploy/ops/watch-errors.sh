#!/bin/bash
# Every 5 minutes (cron): errors that healthcheck.sh does not see. healthcheck.sh answers "is the site up?" (site, API, containers running, disk);
# this script answers "is it working properly?" and tells the owner in Telegram (through alert.sh, the same chat):
#   a) the page that needs the database (/api/placement/block/A1) does not answer 200 within 10 s, twice in a row
#   b) HTTP 5xx in the nginx log of the last 5 minutes: at least 5 and at least 5 % of the requests, twice in a row (a deploy makes a short
#      burst of 502; it is over by the next window), with the paths that fail most
#   c) a container restarted itself (crash, out of memory) since the last check
#   d) new "fail:" / "crit:" lines in the log of the API or of the website (any: a healthy day has none), with the last message
# One message per problem; a reminder every hour while it lasts; "✅ ... снова в порядке" when it is over. Messages never contain the
# addresses' query strings, e-mails, long numbers or tokens (see mask). The first run only learns the restart counters.
# Install: see deploy/ops/README.md (cron line `*/5 * * * * /opt/littlepolyglot/deploy/ops/watch-errors.sh`).
set -u

DIR=${JUMBO_DEPLOY_DIR:-/opt/littlepolyglot/deploy}
STATE_DIR=${JUMBO_WATCH_STATE:-/var/tmp/jumbo-watch}
ALERT=${JUMBO_ALERT:-$DIR/ops/alert.sh}
LOG=${JUMBO_WATCH_LOG:-/home/ubuntu/watch.log}
BASE=${JUMBO_BASE_URL:-https://jumbo.uz}
WINDOW=${JUMBO_WATCH_WINDOW:-5m}
MIN_5XX=${JUMBO_MIN_5XX:-5}
MIN_5XX_PCT=${JUMBO_MIN_5XX_PCT:-5}
REPEAT=${JUMBO_WATCH_REPEAT:-3600}
CONTAINERS="jumbo-api jumbo-web jumbo-nginx jumbo-postgres"

mkdir -p "$STATE_DIR" 2>/dev/null || exit 0
now=$(date +%s)

notify() {
  echo "$(date -Is 2>/dev/null) $1" >> "$LOG" 2>/dev/null
  timeout 30 "$ALERT" "$1" >> "$LOG" 2>&1
}

# One line of a log, safe to send: no e-mails, no long numbers, no token-like strings, no control characters, at most 120 characters.
mask() {
  printf '%s' "$1" | tr -d '\000-\010\013-\037' |
    sed -E 's/[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+/<email>/g; s/[A-Za-z0-9+\/=_-]{32,}/<...>/g; s/[0-9]{5,}/<n>/g' |
    cut -c1-120
}

# report KEY BAD(0|1) TEXT NEEDED_IN_A_ROW
# A problem that lasts: the message when it has been bad NEEDED times in a row, a reminder every REPEAT seconds, "recovered" at the end.
report() {
  local key=$1 bad=$2 text=$3 need=${4:-1} n=0 last
  if [ "$bad" = 1 ]; then
    [ -f "$STATE_DIR/$key.n" ] && n=$(cat "$STATE_DIR/$key.n" 2>/dev/null)
    n=$(( ${n:-0} + 1 ))
    echo "$n" > "$STATE_DIR/$key.n"
    [ "$n" -ge "$need" ] || return 0
    if [ ! -f "$STATE_DIR/$key.alerted" ]; then
      echo "$now" > "$STATE_DIR/$key.alerted"
      notify "🚨 Jumbo: $text"
    else
      last=$(cat "$STATE_DIR/$key.alerted" 2>/dev/null)
      if [ $(( now - ${last:-0} )) -ge "$REPEAT" ]; then
        echo "$now" > "$STATE_DIR/$key.alerted"
        notify "🚨 Jumbo всё ещё: $text"
      fi
    fi
  else
    rm -f "$STATE_DIR/$key.n"
    if [ -f "$STATE_DIR/$key.alerted" ]; then
      rm -f "$STATE_DIR/$key.alerted"
      notify "✅ Jumbo: снова в порядке — $text"
    fi
  fi
}

# a) the page that needs the database
code=$(curl -s -o /dev/null -m 10 -w '%{http_code}' "$BASE/api/placement/block/A1" 2>/dev/null)
[ "${code:-000}" = "200" ] && bad=0 || bad=1
report page_db "$bad" "страница с базой данных не отвечает (код ${code:-000})" 2

# b) 5xx in the nginx log
nginx_log=$(docker logs --since "$WINDOW" jumbo-nginx 2>&1)
if [ -n "$nginx_log" ]; then
  stats=$(printf '%s\n' "$nginx_log" | awk '
    match($0, /"[A-Z]+ [^ ]+ HTTP\/[0-9.]+" [0-9][0-9][0-9]/) {
      s = substr($0, RSTART, RLENGTH); split(s, a, " "); path = a[2]; status = a[4]; sub(/\?.*/, "", path)
      total++
      if (status >= 500) { bad++; top[status " " path]++ }
    }
    END { printf "%d %d\n", total, bad; for (k in top) printf "%d %s\n", top[k], k }')
  total=$(printf '%s\n' "$stats" | head -1 | cut -d' ' -f1)
  n5=$(printf '%s\n' "$stats" | head -1 | cut -d' ' -f2)
  total=${total:-0}; n5=${n5:-0}
  if [ "$n5" -ge "$MIN_5XX" ] && [ $(( n5 * 100 )) -ge $(( MIN_5XX_PCT * total )) ]; then
    top=$(printf '%s\n' "$stats" | tail -n +2 | sort -rn | head -3 | while read -r c s p; do printf '%s %s ×%s, ' "$s" "$(mask "$p")" "$c"; done)
    report http_5xx 1 "ошибки сервера: $n5 из $total запросов за $WINDOW (${top%, })" 2
  else
    report http_5xx 0 "ошибок сервера (5xx) нет"
  fi
fi

# c) a container that restarted itself, or was killed for memory
for c in $CONTAINERS; do
  info=$(docker inspect -f '{{.RestartCount}}|{{.State.OOMKilled}}|{{.State.StartedAt}}' "$c" 2>/dev/null) || continue
  count=${info%%|*}; rest=${info#*|}; oom=${rest%%|*}; started=${rest#*|}
  seen="$STATE_DIR/restarts.$c"
  if [ -f "$seen" ]; then
    prev=$(cat "$seen" 2>/dev/null); prev=${prev:-0}
    if [ "$count" -gt "$prev" ] 2>/dev/null; then
      mem=нет; [ "$oom" = "true" ] && mem="ДА, не хватило памяти"
      notify "⚠️ Jumbo: контейнер $c перезапустился сам (раз: $((count - prev)), нехватка памяти: $mem)"
    fi
  fi
  echo "$count" > "$seen"
  if [ "$oom" = "true" ] && [ "$(cat "$STATE_DIR/oom.$c" 2>/dev/null)" != "$started" ]; then
    echo "$started" > "$STATE_DIR/oom.$c"
    notify "⚠️ Jumbo: контейнер $c был остановлен из-за нехватки памяти"
  fi
done

# d) the logs of the API and of the website: "fail:" and "crit:" are the lines of .NET for errors that were not handled
for c in jumbo-api jumbo-web; do
  text=$(docker logs --since "$WINDOW" "$c" 2>&1)
  n=$(printf '%s\n' "$text" | grep -cE '^(fail|crit):')
  if [ "${n:-0}" -ge 1 ]; then
    # the category is on the "fail:" line itself, the message on the next line
    cat=$(printf '%s\n' "$text" | grep -E '^(fail|crit):' | tail -1 | sed -E 's/^(fail|crit):[[:space:]]*//; s/\[[0-9]+\]$//')
    msg=$(printf '%s\n' "$text" | grep -A1 -E '^(fail|crit):' | grep -vE '^(fail|crit):|^--$' | tail -1 | sed -E 's/^[[:space:]]+//')
    report "log_$c" 1 "в логе $c ошибок: $n за $WINDOW, последняя: $(mask "$cat") — $(mask "$msg")" 1
  else
    report "log_$c" 0 "в логе $c новых ошибок нет"
  fi
done

# the log of this script stays small
[ -f "$LOG" ] && [ "$(stat -c %s "$LOG" 2>/dev/null || echo 0)" -gt 1048576 ] && tail -n 200 "$LOG" > "$LOG.tmp" && mv "$LOG.tmp" "$LOG"
exit 0
