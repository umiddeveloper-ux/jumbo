#!/bin/bash
# SKETCH - NOT INSTALLED, NOT ENABLED: no cloud account exists yet (see OFFSITE-BACKUP.md).
# Copies the newest nightly database copy to an ENCRYPTED rclone remote (a crypt remote over an object storage or Google Drive), checks it
# arrived, keeps the newest KEEP files there, and tells the owner in Telegram (alert.sh) when anything goes wrong.
# The data in the dump is personal (phones, password hashes, access tokens): it must never be uploaded to a remote that is not a crypt remote.
#   offsite-sync.sh              do it
#   offsite-sync.sh --dry-run    only show what would be copied / deleted
# Cron (after backup-db.sh, 03:30):  45 3 * * * /opt/littlepolyglot/deploy/ops/offsite-sync.sh >> /home/ubuntu/offsite.log 2>&1
set -u
set -o pipefail

DIR=${JUMBO_DEPLOY_DIR:-/opt/littlepolyglot/deploy}
BACKUPS=${JUMBO_BACKUP_DIR:-/opt/littlepolyglot/backups}
REMOTE=${JUMBO_OFFSITE_REMOTE:-jumbo-offsite}        # the crypt remote made with `rclone config` (never the plain storage behind it)
KEEP=${JUMBO_OFFSITE_KEEP:-30}
DRY=""
[ "${1:-}" = "--dry-run" ] && DRY="--dry-run"
LOCK=/var/tmp/jumbo-offsite.lock

fail() {
  echo "$(date -Is) OFFSITE FAILED: $1"
  "$DIR/ops/alert.sh" "❌ Jumbo: копия базы не ушла за пределы сервера ($1)"
  exit 1
}

command -v rclone >/dev/null 2>&1 || fail "rclone не установлен"
exec 9>"$LOCK"; flock -n 9 || { echo "$(date -Is) another run is going"; exit 0; }

# the remote must be a crypt remote: refuse to upload the dump in the clear
type=$(rclone config dump 2>/dev/null | jq -r --arg r "$REMOTE" '.[$r].type // empty')
[ "$type" = "crypt" ] || fail "удалённое хранилище $REMOTE не зашифровано (тип: ${type:-нет})"

FILE=$(ls -1t "$BACKUPS"/db-*.sql.gz 2>/dev/null | head -1)
[ -n "$FILE" ] || fail "нет копии в $BACKUPS"
# the same checks as backup-db.sh: only a good copy goes out
gzip -t "$FILE" || fail "архив $(basename "$FILE") повреждён"
SIZE=$(stat -c %s "$FILE")
[ "$SIZE" -gt 100000 ] || fail "слишком маленький файл: $SIZE байт"

NAME=$(basename "$FILE")
if rclone lsf "$REMOTE:" --files-only 2>/dev/null | grep -qx "$NAME"; then
  echo "$(date -Is) $NAME is already there"
else
  timeout 1800 rclone copyto "$FILE" "$REMOTE:$NAME" --transfers 1 --retries 3 --low-level-retries 5 $DRY || fail "копирование $NAME"
  if [ -z "$DRY" ]; then
    remote_size=$(rclone size "$REMOTE:$NAME" --json 2>/dev/null | jq -r '.bytes // 0')
    [ "$remote_size" = "$SIZE" ] || fail "размер на той стороне ($remote_size) не равен $SIZE"
    echo "$(date -Is) uploaded $NAME ($SIZE bytes)"
  fi
fi

# only the newest KEEP stay (the names carry the date, so the sort by name is the sort by age)
rclone lsf "$REMOTE:" --files-only 2>/dev/null | grep '^db-.*\.sql\.gz$' | sort -r | tail -n +$((KEEP + 1)) |
  while read -r old; do rclone deletefile "$REMOTE:$old" $DRY && echo "$(date -Is) deleted $old"; done
exit 0
