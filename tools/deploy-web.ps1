# Deploys the website (JumboWeb) to the production server: uploads the sources, backs the old ones up, copies the new ones over,
# rebuilds the container `web`, reloads nginx and checks the site. Run in PowerShell from anywhere:  .\tools\deploy-web.ps1
# (the API, the database and the other containers are not touched; the server has no git: the sources are copied, see deploy/ops/README.md)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$key = Join-Path $HOME '.ssh\jumbo_deploy'
$server = 'ubuntu@46.8.195.183'
$tgz = Join-Path $env:TEMP 'jumbo-web-src.tgz'

Write-Host 'Packing the sources...' -ForegroundColor Cyan
tar --exclude=bin --exclude=obj --exclude=node_modules -czf $tgz -C $root JumboWeb
if ($LASTEXITCODE -ne 0) { throw 'tar failed' }

Write-Host 'Uploading...' -ForegroundColor Cyan
scp -i $key -o BatchMode=yes $tgz "${server}:/tmp/deploy-src.tgz"
if ($LASTEXITCODE -ne 0) { throw 'scp failed' }

$remote = @'
set -e
stamp=$(date +%Y%m%d-%H%M)
rm -rf /tmp/newsrc && mkdir /tmp/newsrc && tar -xzf /tmp/deploy-src.tgz -C /tmp/newsrc
cd /opt/littlepolyglot
echo "Files that change:"
diff -rq --exclude=bin --exclude=obj --exclude=node_modules /tmp/newsrc/JumboWeb JumboWeb || true
mkdir -p ~/src-backup-web-$stamp && tar --exclude=bin --exclude=obj -czf ~/src-backup-web-$stamp/JumboWeb.tgz JumboWeb
echo "Backup: ~/src-backup-web-$stamp/JumboWeb.tgz"
cp -a /tmp/newsrc/JumboWeb/. JumboWeb/
cd deploy
docker compose -f docker-compose.prod.yml build web 2>&1 | tail -2
docker compose -f docker-compose.prod.yml up -d --no-deps web 2>&1 | tail -2
sleep 8
docker exec jumbo-nginx nginx -s reload
docker ps --format "{{.Names}} {{.Status}}" | head -4
'@
Write-Host 'Deploying on the server...' -ForegroundColor Cyan
ssh -i $key -o BatchMode=yes $server ($remote -replace "`r`n", "`n")
if ($LASTEXITCODE -ne 0) { throw 'the deploy on the server failed' }

Start-Sleep -Seconds 4
foreach ($u in 'https://jumbo.uz/', 'https://jumbo.uz/login', 'https://jumbo.uz/register') {
    $code = (Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 30).StatusCode
    Write-Host "$u -> $code"
}
Write-Host 'Done.' -ForegroundColor Green
