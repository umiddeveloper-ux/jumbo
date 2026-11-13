# Puts the pictures of the printed cards (made by tools/import-card-images.mjs, file .local-test/card-images.sql) into the PRODUCTION database.
# Only the pictures change (table svg_images); users, progress and words stay. A copy of the database is made first, on the server.
# Run in PowerShell from anywhere:  .\tools\apply-card-images.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sql = Join-Path $root '.local-test\card-images.sql'
if (-not (Test-Path $sql)) { throw "No file ${sql}: run  node tools/import-card-images.mjs  first" }
$key = Join-Path $HOME '.ssh\jumbo_deploy'
$server = 'ubuntu@46.8.195.183'

Write-Host 'Uploading the pictures...' -ForegroundColor Cyan
scp -i $key -o BatchMode=yes $sql "${server}:/tmp/card-images.sql"
if ($LASTEXITCODE -ne 0) { throw 'scp failed' }

$remote = @'
set -e
echo "Backup of the database..."
/opt/littlepolyglot/deploy/ops/backup-db.sh
echo "Applying..."
docker exec -i jumbo-postgres psql -U littlepolyglot -d littlepolyglot -v ON_ERROR_STOP=1 < /tmp/card-images.sql | sort | uniq -c
docker exec jumbo-postgres psql -U littlepolyglot -d littlepolyglot -t -c "select count(*) filter (where svg like '%image/jpeg%') from svg_images"
rm -f /tmp/card-images.sql
'@
($remote -replace "`r`n", "`n") | ssh -i $key -o BatchMode=yes $server "bash -s"
if ($LASTEXITCODE -ne 0) { throw 'applying on the server failed' }
Write-Host 'Done: card pictures of all books applied. The app and the site show them after the next load.' -ForegroundColor Green
