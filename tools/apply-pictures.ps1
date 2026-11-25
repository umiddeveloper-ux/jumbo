# Puts the picture FILES of the words (made by  node tools/build-pictures.mjs  into media-dev/pics) on the PRODUCTION server and teaches nginx
# to serve them at /pics/. Nothing in the database or in the running containers changes except one nginx reload; a failed nginx test
# puts the old config back. Run in PowerShell from anywhere:  .\tools\apply-pictures.ps1
#
# ORDER of the whole roll-out (the file names do not depend on the database, so the files can be made here from the local database):
#   1. deploy the API (tools\deploy-api.ps1): the migration adds svg_images.image_file and the start fills in the names
#   2. this script (the files + the nginx location)
#   3. deploy the website (tools\deploy-web.ps1): it shows the files (before that nothing changes for the learners)
# A picture whose file is missing would show nothing, so step 2 comes before step 3 and the files must be complete (the script checks the count).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$pics = Join-Path $root 'media-dev\pics'
if (-not (Test-Path $pics)) { throw "No folder ${pics}: run  node tools/build-pictures.mjs  first" }
$count = (Get-ChildItem $pics -Filter *.webp).Count
if ($count -lt 1000) { throw "Only $count files in ${pics}: the run of tools/build-pictures.mjs was not complete" }
$key = Join-Path $HOME '.ssh\jumbo_deploy'
$server = 'ubuntu@46.8.195.183'
$tgz = Join-Path $env:TEMP 'jumbo-pics.tgz'

Write-Host "Packing $count files..." -ForegroundColor Cyan
tar -cf $tgz -C $pics .
if ($LASTEXITCODE -ne 0) { throw 'tar failed' }

Write-Host 'Uploading...' -ForegroundColor Cyan
scp -i $key -o BatchMode=yes $tgz "${server}:/tmp/jumbo-pics.tar"
if ($LASTEXITCODE -ne 0) { throw 'scp failed (files)' }
scp -i $key -o BatchMode=yes (Join-Path $root 'deploy\nginx.conf') "${server}:/tmp/nginx.conf.new"
if ($LASTEXITCODE -ne 0) { throw 'scp failed (nginx.conf)' }

$remote = @'
set -e -o pipefail
mkdir -p /opt/littlepolyglot/media/pics
tar -xf /tmp/jumbo-pics.tar -C /opt/littlepolyglot/media/pics
chmod -R a+rX /opt/littlepolyglot/media/pics
echo "Files on the server: $(ls /opt/littlepolyglot/media/pics | wc -l), $(du -sh /opt/littlepolyglot/media/pics | cut -f1)"

cd /opt/littlepolyglot/deploy
cp nginx.conf ~/nginx.conf.before-pics
cp /tmp/nginx.conf.new nginx.conf
if docker exec jumbo-nginx nginx -t < /dev/null 2>&1; then
  docker exec jumbo-nginx nginx -s reload < /dev/null
  echo "nginx reloaded"
else
  cp ~/nginx.conf.before-pics nginx.conf
  echo "nginx test FAILED, the old config is back"; exit 1
fi

f=$(ls /opt/littlepolyglot/media/pics | head -1)
echo "Check: $(curl -s -o /dev/null -w '%{http_code} %{content_type} %{size_download} bytes' -H 'Host: jumbo.uz' http://127.0.0.1:8080/pics/$f)"
curl -sI -H 'Host: jumbo.uz' http://127.0.0.1:8080/pics/$f | grep -i 'cache-control' || true
rm -f /tmp/jumbo-pics.tar /tmp/nginx.conf.new
'@
# the script goes as base64 (piping text puts a byte-order mark in front of it; a command that reads stdin would also eat the rest of the script)
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($remote -replace "`r`n", "`n")))
ssh -i $key -o BatchMode=yes $server "echo $b64 | base64 -d | bash"
if ($LASTEXITCODE -ne 0) { throw 'applying on the server failed' }
Write-Host 'Done: /pics/ is served. Next: tools\deploy-web.ps1 (if the API with the migration is already deployed).' -ForegroundColor Green
