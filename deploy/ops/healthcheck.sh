#!/bin/bash
# Every 5 minutes (cron): is the site alive? Checks the site, the API, the four containers and the disk. Tells the owner in Telegram:
# when it has been bad twice in a row (a restart during a deploy takes seconds and is not reported), again every 6 hours while it lasts,
# and when it is good again.
DIR=${JUMBO_DEPLOY_DIR:-/opt/littlepolyglot/deploy}
STATE="$DIR/.health-state"          # ok | bad1 | bad:<epoch of the last message>
LOG=${JUMBO_HEALTH_LOG:-/home/ubuntu/health.log}

code() { curl -s -o /dev/null -m 15 -w '%{http_code}' "$1"; }
problems=""
[ "$(code https://jumbo.uz/)" = "200" ] || problems+="сайт не открывается; "
[ "$(code 'https://jumbo.uz/api/app/version?platform=android')" = "200" ] || problems+="API не отвечает; "
for c in jumbo-api jumbo-web jumbo-nginx jumbo-postgres; do
  [ "$(docker inspect -f '{{.State.Running}}' $c 2>/dev/null)" = "true" ] || problems+="контейнер $c остановлен; "
done
use=$(df --output=pcent / | tail -1 | tr -dc '0-9')
[ "${use:-0}" -lt 90 ] || problems+="диск заполнен на ${use}%; "

last=$(cat "$STATE" 2>/dev/null || echo ok)
now=$(date +%s)
if [ -z "$problems" ]; then
  if [[ "$last" == bad:* ]]; then "$DIR/ops/alert.sh" "✅ Jumbo: всё снова работает." >> "$LOG" 2>&1; fi
  echo ok > "$STATE"
else
  if [ "$last" = "ok" ]; then
    echo bad1 > "$STATE"                                            # the first time: wait for the next check
  elif [ "$last" = "bad1" ]; then
    echo "bad:$now" > "$STATE"; "$DIR/ops/alert.sh" "🚨 Jumbo: $problems" >> "$LOG" 2>&1
  else
    since=${last#bad:}
    if [ $((now - since)) -ge 21600 ]; then echo "bad:$now" > "$STATE"; "$DIR/ops/alert.sh" "🚨 Jumbo всё ещё: $problems" >> "$LOG" 2>&1; fi
  fi
  echo "$(date -Is) problems: $problems" >> "$LOG"
fi
# the log stays small
[ -f "$LOG" ] && [ "$(stat -c %s "$LOG")" -gt 1048576 ] && tail -n 200 "$LOG" > "$LOG.tmp" && mv "$LOG.tmp" "$LOG"
exit 0
