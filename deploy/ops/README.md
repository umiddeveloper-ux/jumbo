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

## Added 2026-11-27 (prepared and tested on the owner's computer; the server does NOT have them yet unless written below)

| file | what |
|---|---|
| `watch-errors.sh` | cron every 5 minutes: what `healthcheck.sh` does not see. The page that needs the database (`/api/placement/block/A1`) not answering, HTTP 5xx in the nginx log (>= 5 and >= 5 %, twice in a row), a container that restarted itself or ran out of memory, new `fail:`/`crit:` lines in the API or website log. Same Telegram chat (`alert.sh`), one message per problem, a reminder every hour, "recovered" at the end. Messages hide query strings, e-mails, long numbers, tokens. Does not repeat the container-running / disk checks of `healthcheck.sh`. |
| `test-watch-errors.sh` | 36 tests of the above with a fake curl / docker / alert (`bash deploy/ops/test-watch-errors.sh`) |
| `RESTORE.md` | how to restore a backup for real (new database, check, swap), written from a real restore: 3-5 seconds for 108 MB |
| `OFFSITE-BACKUP.md`, `offsite-sync.sh` | a third copy of the dump outside the server and the owner's computer: options, costs, and a rclone-crypt script (NOT enabled) |
| `../../tools/restore-backup-test.ps1` | on the owner's computer: restores the newest `D:\JumboBackups` copy into a scratch database and checks it |

Install `watch-errors.sh` on the server (the owner approves each step):
1. `scp -i ~/.ssh/jumbo_deploy deploy/ops/watch-errors.sh ubuntu@46.8.195.183:/opt/littlepolyglot/deploy/ops/`
2. on the server: `sed -i 's/\r$//' /opt/littlepolyglot/deploy/ops/watch-errors.sh && chmod +x /opt/littlepolyglot/deploy/ops/watch-errors.sh`
3. try it once by hand: `/opt/littlepolyglot/deploy/ops/watch-errors.sh; echo $?; tail -3 /home/ubuntu/watch.log` (it sends nothing when all is well; it needs `docker` and `curl`, as `healthcheck.sh` does)
4. cron (`crontab -e`): `*/5 * * * * /opt/littlepolyglot/deploy/ops/watch-errors.sh`
To undo: delete that cron line (the script and `/var/tmp/jumbo-watch` can stay or be removed).
