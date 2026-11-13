# Deploys the API (Jumbo.Api + Jumbo.Shared) to the production server: uploads the sources, backs the old ones up (and the database),
# copies the new ones over, rebuilds the container `api` and checks it. Run in PowerShell from anywhere:  .\tools\deploy-api.ps1
# The database is not changed by this script (a migration, if there is one, runs when the API starts). The seed data (SeedData: the books,
# the 1.8 GB of card images) is NOT uploaded: the server keeps its own. The site (`web`) is deployed by tools/deploy-web.ps1.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$key = Join-Path $HOME '.ssh\jumbo_deploy'
$server = 'ubuntu@46.8.195.183'
$tgz = Join-Path $env:TEMP 'jumbo-api-src.tgz'

Write-Host 'Packing the sources...' -ForegroundColor Cyan
tar --exclude=bin --exclude=obj --exclude=SeedData -czf $tgz -C $root Jumbo.Api Jumbo.Shared
if ($LASTEXITCODE -ne 0) { throw 'tar failed' }

Write-Host 'Uploading...' -ForegroundColor Cyan
scp -i $key -o BatchMode=yes $tgz "${server}:/tmp/deploy-api-src.tgz"
if ($LASTEXITCODE -ne 0) { throw 'scp failed' }

$remote = @'
set -e -o pipefail
stamp=$(date +%Y%m%d-%H%M)
rm -rf /tmp/newapi && mkdir /tmp/newapi && tar -xzf /tmp/deploy-api-src.tgz -C /tmp/newapi
cd /opt/littlepolyglot
echo "Files that change:"
diff -rq --exclude=bin --exclude=obj --exclude=SeedData /tmp/newapi/Jumbo.Api Jumbo.Api || true
diff -rq --exclude=bin --exclude=obj /tmp/newapi/Jumbo.Shared Jumbo.Shared || true
echo "Backup of the database..."
/opt/littlepolyglot/deploy/ops/backup-db.sh < /dev/null
mkdir -p ~/src-backup-api-$stamp && tar --exclude=bin --exclude=obj --exclude=SeedData -czf ~/src-backup-api-$stamp/Jumbo.Api.tgz Jumbo.Api Jumbo.Shared
echo "Backup of the sources: ~/src-backup-api-$stamp/Jumbo.Api.tgz"
cp -a /tmp/newapi/Jumbo.Api/. Jumbo.Api/
cp -a /tmp/newapi/Jumbo.Shared/. Jumbo.Shared/
cd deploy
docker compose -f docker-compose.prod.yml build api < /dev/null 2>&1 | tail -3
docker compose -f docker-compose.prod.yml up -d --no-deps api < /dev/null 2>&1 | tail -3
sleep 10
docker exec jumbo-nginx nginx -s reload < /dev/null
docker ps --format "{{.Names}} {{.Status}}"
'@
Write-Host 'Deploying on the server...' -ForegroundColor Cyan
# the script goes as base64 (piping text puts a byte-order mark in front of it and the quotes survive)
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($remote -replace "`r`n", "`n")))
ssh -i $key -o BatchMode=yes $server "echo $b64 | base64 -d | bash"
if ($LASTEXITCODE -ne 0) { throw 'the deploy on the server failed' }

Start-Sleep -Seconds 4
foreach ($u in 'https://jumbo.uz/', 'https://jumbo.uz/api/lives') {
    try { $code = (Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 30).StatusCode } catch { $code = $_.Exception.Response.StatusCode.value__ }
    Write-Host "$u -> $code (401 is fine for /api/lives: it needs a token)"
}
Write-Host 'Done.' -ForegroundColor Green
