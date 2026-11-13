# One-off: uploads the new book editions (English book_NN_*.json + the Turkish Turk/book_NN_tr.json) to production and reloads
# the English books that changed (SeedData__ReloadBooks, one restart only - this DELETES the learners' progress in the words that
# changed in those books; the admin endpoint reload/admin/books/{n}/reload compares content first, so a book that did not really
# change is left alone). Does NOT touch book 7 (its docx was not edited) and does NOT seed Turkish itself: Turkish is a brand-new
# language on prod, so the next `tools/deploy-api.ps1` (which ships the Turkish code) seeds it fresh, reading these already-fixed
# English words. Run this BEFORE deploy-api.ps1.  Run in PowerShell from anywhere:  .\tools\deploy-books.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$key = Join-Path $HOME '.ssh\jumbo_deploy'
$server = 'ubuntu@46.8.195.183'
$tgz = Join-Path $env:TEMP 'jumbo-books.tgz'
$booksDir = Join-Path $root 'Jumbo.Api\SeedData\Books'

Write-Host 'Packing the book files...' -ForegroundColor Cyan
$files = Get-ChildItem $booksDir -Filter 'book_*.json' | ForEach-Object { "Books/$($_.Name)" }
$files += Get-ChildItem (Join-Path $booksDir 'Turk') -Filter 'book_*_tr.json' | ForEach-Object { "Books/Turk/$($_.Name)" }
Push-Location (Join-Path $root 'Jumbo.Api\SeedData')
try { tar -czf $tgz $files } finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'tar failed' }
Write-Host "  $($files.Count) files"

Write-Host 'Uploading...' -ForegroundColor Cyan
scp -i $key -o BatchMode=yes $tgz "${server}:/tmp/jumbo-books.tgz"
if ($LASTEXITCODE -ne 0) { throw 'scp failed' }

$remote = @'
set -e -o pipefail
stamp=$(date +%Y%m%d-%H%M)
cd /opt/littlepolyglot
echo "Backup of the database..."
/opt/littlepolyglot/deploy/ops/backup-db.sh < /dev/null
mkdir -p ~/src-backup-books-$stamp && cp -a Jumbo.Api/SeedData/Books ~/src-backup-books-$stamp/
echo "Backup of the old book files: ~/src-backup-books-$stamp/Books"
rm -rf /tmp/newbooks && mkdir /tmp/newbooks && tar -xzf /tmp/jumbo-books.tgz -C /tmp/newbooks
cp -a /tmp/newbooks/Books/. Jumbo.Api/SeedData/Books/
cat > deploy/docker-compose.reload.yml <<'YML'
services:
  api:
    environment:
      SeedData__ReloadBooks: "1,2,3,4,5,6,8,9,10"
YML
cd deploy
echo "Reloading the changed books (one restart)..."
docker compose -f docker-compose.prod.yml -f docker-compose.reload.yml up -d --no-deps api < /dev/null 2>&1 | tail -5
echo "Waiting for the reload to finish (checks word 118 = 'thirteen', only in the new edition)..."
for i in $(seq 1 30); do
  sleep 3
  got=$(docker exec jumbo-postgres psql -U littlepolyglot -d littlepolyglot -t -A -c "select word_name from words where language_id=1 and word_number=118" 2>/dev/null || true)
  if [ "$got" = "thirteen" ]; then echo "Reload confirmed after $((i*3))s."; break; fi
  if [ "$i" = "30" ]; then echo "PROBLEM: word 118 is still '$got' after 90s - the reload may not have finished. Not restarting without the setting; check 'docker logs jumbo-api' by hand." ; exit 1; fi
done
docker logs jumbo-api --since 2m 2>&1 | grep -i "reload\|book" || true
echo "Restarting without the reload setting..."
docker compose -f docker-compose.prod.yml up -d --no-deps api < /dev/null 2>&1 | tail -5
sleep 6
rm -f docker-compose.reload.yml
docker exec jumbo-nginx nginx -s reload < /dev/null
docker exec jumbo-postgres psql -U littlepolyglot -d littlepolyglot -t -c "select s.book_id, count(*) from words w join subjects s on s.subject_id=w.subject_id where w.language_id=1 group by 1 order by 1"
docker ps --format "{{.Names}} {{.Status}}"
'@
Write-Host 'Deploying on the server...' -ForegroundColor Cyan
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($remote -replace "`r`n", "`n")))
ssh -i $key -o BatchMode=yes $server "echo $b64 | base64 -d | bash"
if ($LASTEXITCODE -ne 0) { throw 'the deploy on the server failed' }

Write-Host 'Done: the changed English books are reloaded. Now run tools/deploy-api.ps1 to ship the Turkish feature itself.' -ForegroundColor Green
