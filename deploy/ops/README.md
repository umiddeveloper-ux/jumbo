# Operations on the server (/opt/littlepolyglot/deploy/ops)

| script | when | what |
|---|---|---|
| `backup-db.sh` | cron 03:30 every night | dump of the database to `/opt/littlepolyglot/backups/db-YYYYmmdd-HHMM.sql.gz`; checks the copy (gzip ok, not tiny, users table inside); keeps the newest 14; tells the owner in Telegram if it fails |
| `restore-test.sh` | by hand (once a month is good) | loads the newest copy into a temporary database, counts users and topics, drops it; the real database is not touched |
| `healthcheck.sh` | cron every 5 minutes | checks https://jumbo.uz/, the API, the 4 containers and the disk (< 90 %); Telegram message after 2 bad checks in a row, again every 6 h, and when it is good again |
| `alert.sh "text"` | called by the others | sends the message to the chat in `/opt/littlepolyglot/deploy/alert-chat-id` with the bot token of the API container |

The owner gets the chat id by writing `/id` to the Jumbo bot; it goes into `alert-chat-id` (one number in the file).

Copy to the owner's computer: `tools/pull-backup.ps1` (to `D:\JumboBackups`).

Restore of the real database from a copy (stop the API first, the copy of today is the safest source):
`zcat db-....sql.gz | docker exec -i jumbo-postgres psql -U littlepolyglot -d littlepolyglot` into an EMPTY database
(drop and create it first); then start the API.
