# Restoring the Jumbo database from a backup (emergency guide)

Written from a real restore on 2026-11-27 (tools/restore-backup-test.ps1): the newest copy, 108 MB, loaded into an empty database
**in 3-5 seconds** with no errors; 36 tables, 45 foreign keys, 97 indexes, 470 users with tokens and passwords, all counters ahead of the data.

## What a copy is
`/opt/littlepolyglot/backups/db-YYYYmmdd-HHMM.sql.gz` (server, nightly 03:30, newest 14) and `D:\JumboBackups` (owner's computer, pulled by
`tools/pull-backup.ps1`, newest 60). It is a plain `pg_dump` of the database `littlepolyglot` (Postgres 17), made as the role
`littlepolyglot`: tables, data, keys, indexes, owners (`OWNER TO littlepolyglot`). No `CREATE DATABASE`, no extensions.

**What is NOT in a copy:** the files in `/opt/littlepolyglot/media` (topic videos `videos/`, word pictures `pics/`, app downloads `downloads/`),
the Docker volume `littlepolyglot-webkeys` (login cookies: after a loss everybody just signs in again), `deploy/.env`, `deploy/alert-chat-id`, the
server compose file. Pictures can be rebuilt: `node tools/build-pictures.mjs` + `tools\apply-pictures.ps1`; videos exist only on the owner's
computer (`TopicVideos\`) and the server.

## A. Prove that a copy works (any time, nothing is changed)
- On the owner's computer: `.\tools\restore-backup-test.ps1` (scratch database in the local Docker, dropped at the end, prints OK / WARN / FAIL).
- On the server: `deploy/ops/restore-test.sh` (loads the newest copy into `restore_test_tmp`, counts users, drops it).
Do one of them about once a month.

## B. Real restore on the server (the database is damaged or lost)
Run as `ubuntu` on the server, in `/opt/littlepolyglot/deploy`. Takes about 5 minutes with checks. The site is down only between steps 3 and 6.

1. Pick the copy (the newest good one) and check it:
   ```bash
   F=$(ls -1t /opt/littlepolyglot/backups/db-*.sql.gz | head -1); echo $F; gzip -t $F && echo gzip-ok
   ```
2. Load it into a NEW database next to the old one (the site keeps running meanwhile):
   ```bash
   P="docker exec -i jumbo-postgres psql -U littlepolyglot"
   $P -d postgres -c "CREATE DATABASE littlepolyglot_new"
   zcat $F | $P -d littlepolyglot_new -v ON_ERROR_STOP=1 -q -o /dev/null
   ```
   It must end without any `ERROR`. If it stops, the copy is bad: `DROP DATABASE littlepolyglot_new` and try the next older copy.
3. Check it (numbers close to what the site has; the newest migration is the last one deployed):
   ```bash
   $P -d littlepolyglot_new -Atc 'select count(*) from users; select count(*) from words; select count(*) from subjects;'
   $P -d littlepolyglot_new -Atc 'select migration_id from "__EFMigrationsHistory" order by 1 desc limit 1'
   ```
   (about 470 users, 12000 words, 200 subjects on 2026-11-27.)
4. Stop what uses the database, so that nobody is connected:
   ```bash
   docker compose -f docker-compose.prod.yml stop web api
   ```
5. Swap the databases (the API's connection string says `Database=littlepolyglot`, so it never has to be edited):
   ```bash
   $P -d postgres -c "ALTER DATABASE littlepolyglot RENAME TO littlepolyglot_broken" \
                  -c "ALTER DATABASE littlepolyglot_new RENAME TO littlepolyglot"
   ```
6. Start again and look:
   ```bash
   docker compose -f docker-compose.prod.yml up -d api web
   docker logs --tail 20 jumbo-api      # "Now listening", no crash; the API applies any newer migration itself
   curl -s -o /dev/null -w '%{http_code}\n' https://jumbo.uz/api/placement/block/A1     # 200
   ```
7. Keep `littlepolyglot_broken` for a few days (disk is cheap), then `DROP DATABASE littlepolyglot_broken`.

Notes
- Everything learners did after the copy was made (up to 24 hours with the nightly copy) is lost; tell the teachers if an exam fell in that window.
- If the Postgres volume itself is gone: `docker compose -f docker-compose.prod.yml up -d postgres` creates an empty `littlepolyglot` (the compose
  file does it); then do steps 2-6 loading straight into `littlepolyglot` (no rename needed), or just load the copy into it before starting `api`.
- The API refills the first level-test questions and the picture file names at start (only what is missing).
- A copy made by a newer version of the code than the running API has migrations the API does not know: deploy the matching API first.
- On Windows (the owner's computer) never pipe a `.gz` through PowerShell: copy the file into the container (`docker cp`) and unpack there,
  which is what `tools/restore-backup-test.ps1` does.
