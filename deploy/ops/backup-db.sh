#!/bin/bash
# The nightly copy of the Jumbo database (cron, 03:30). A copy is kept only if it is a good one: a complete gzip, not tiny, with the
# users table inside. The newest 14 are kept; a failure is reported to the owner in Telegram.
set -o pipefail
DIR=/opt/littlepolyglot/backups
KEEP=14
mkdir -p "$DIR"
OUT="$DIR/db-$(date +%Y%m%d-%H%M).sql.gz"
TMP="$OUT.part"

fail() {
  echo "$(date -Is) BACKUP FAILED: $1"
  rm -f "$TMP"
  /opt/littlepolyglot/deploy/ops/alert.sh "❌ Jumbo: резервная копия базы не сделана ($1)"
  exit 1
}

docker exec jumbo-postgres pg_dump -U littlepolyglot littlepolyglot | gzip > "$TMP" || fail "pg_dump"
gzip -t "$TMP" || fail "архив повреждён"
SIZE=$(stat -c %s "$TMP")
[ "$SIZE" -gt 100000 ] || fail "слишком маленький файл: $SIZE байт"
# (grep -c reads to the end: grep -q would close the pipe early and pipefail would call that a failure)
USERS_TABLE=$(zcat "$TMP" | grep -c "^COPY public.users ")
[ "$USERS_TABLE" -ge 1 ] || fail "в копии нет таблицы пользователей"
mv "$TMP" "$OUT"

# only the newest $KEEP copies stay
ls -1t "$DIR"/db-*.sql.gz | tail -n +$((KEEP + 1)) | xargs -r rm -f
echo "$(date -Is) backup ok: $OUT ($SIZE bytes), copies kept: $(ls -1 "$DIR"/db-*.sql.gz | wc -l)"
