# Puts the Payme settings (cash register id, login, key) on the PRODUCTION server. The OWNER runs this by hand: the key is typed here (hidden),
# goes to the server over ssh and is never printed, never written in the repository and never seen by anybody else (not by Claude either).
#   .\tools\set-payme.ps1
# Asks: mode (sandbox = the TEST KEY and test.paycom.uz; production = the real KEY and checkout.paycom.uz), cash register id, login, key.
# Then on the server: backs up .env and docker-compose.prod.yml, adds the Payme lines to the api service once, writes the PAYME_* values to .env,
# recreates the api container (a plain "up -d" does not re-read .env) and runs two checks against the Merchant API:
#   - without a key it must answer -32504 (nobody gets in without the key);
#   - with the key it must answer -31003 (transaction not found = the key and the login are accepted). -32504 here means the login or the key is wrong.
# Run it again with another mode to switch sandbox <-> production. The tax data (Payme__Mxik, Payme__PackageCode, Payme__VatPercent) are NOT asked
# here: add them to the same .env as PAYME_MXIK, PAYME_PACKAGE_CODE, PAYME_VAT_PERCENT when the accountant has given them (see deploy/payme/README.md).
$ErrorActionPreference = 'Stop'
$key = Join-Path $HOME '.ssh\jumbo_deploy'
$server = 'ubuntu@46.8.195.183'

$mode = (Read-Host 'Mode: sandbox or production [sandbox]').Trim().ToLowerInvariant()
if ($mode -eq '') { $mode = 'sandbox' }
if ($mode -notin 'sandbox', 'production') { throw 'Type sandbox or production.' }
if ($mode -eq 'production') {
    $sure = Read-Host 'PRODUCTION: real money, the real KEY. Is the sandbox finished and the accountant data set? Type yes'
    if ($sure.Trim().ToLowerInvariant() -ne 'yes') { throw 'Cancelled.' }
}

$merchant = (Read-Host 'Cash register id (ID-... without the "ID-" is fine too)').Trim()
$merchant = $merchant -replace '^(?i)ID-', ''
$login = (Read-Host 'Login of the Basic authorization [Paycom]').Trim()
if ($login -eq '') { $login = 'Paycom' }
$secure = Read-Host ('Key ({0}), hidden' -f $(if ($mode -eq 'sandbox') { 'the TEST KEY' } else { 'the real KEY' })) -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
$payKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

foreach ($v in @($merchant, $login, $payKey)) {
    if ([string]::IsNullOrWhiteSpace($v) -or $v -match "['`"\$\s\\]") { throw 'A value is empty or has a space, quote, $ or backslash: check what you typed.' }
}
if ($merchant -notmatch '^[0-9a-fA-F]{24}$') { Write-Host 'Note: a cash register id is normally 24 hex characters; check it if the test below fails.' -ForegroundColor Yellow }

$checkout = if ($mode -eq 'sandbox') { 'https://checkout.test.paycom.uz' } else { 'https://checkout.paycom.uz' }
# in the sandbox the address of the caller is not checked (the key still is); in production only Payme's addresses are let in
$checkIp = if ($mode -eq 'sandbox') { 'false' } else { 'true' }

$remote = @'
set -e -o pipefail
umask 077
. /tmp/payme.env
rm -f /tmp/payme.env
cd /opt/littlepolyglot/deploy
stamp=$(date +%Y%m%d-%H%M)
cp .env ~/env.before-payme-$stamp
cp docker-compose.prod.yml ~/compose.before-payme-$stamp.yml

if ! grep -q 'Payme__Login' docker-compose.prod.yml; then
  sed -i '/Telegram__SiteUrl:/a\      Payme__Login: "${PAYME_LOGIN:-}"\n      Payme__Key: "${PAYME_KEY:-}"\n      Payme__MerchantId: "${PAYME_MERCHANT_ID:-}"\n      Payme__CheckoutUrl: "${PAYME_CHECKOUT_URL:-https://checkout.paycom.uz}"\n      Payme__CheckIp: "${PAYME_CHECK_IP:-true}"\n      Payme__Mxik: "${PAYME_MXIK:-}"\n      Payme__PackageCode: "${PAYME_PACKAGE_CODE:-}"\n      Payme__VatPercent: "${PAYME_VAT_PERCENT:-}"' docker-compose.prod.yml
  echo "compose: Payme lines added"
else
  echo "compose: Payme lines already there"
fi

for k in PAYME_LOGIN PAYME_KEY PAYME_MERCHANT_ID PAYME_CHECKOUT_URL PAYME_CHECK_IP; do sed -i "/^$k=/d" .env; done
printf "PAYME_LOGIN='%s'\nPAYME_KEY='%s'\nPAYME_MERCHANT_ID='%s'\nPAYME_CHECKOUT_URL='%s'\nPAYME_CHECK_IP='%s'\n" "$PM_LOGIN" "$PM_KEY" "$PM_ID" "$PM_URL" "$PM_CHECKIP" >> .env
chmod 600 .env

docker compose -f docker-compose.prod.yml config -q && echo "compose: file is valid"
docker compose -f docker-compose.prod.yml up -d --no-deps --force-recreate api < /dev/null 2>&1 | tail -2

for i in $(seq 1 40); do
  code=$(curl -s -o /dev/null -w '%{http_code}' -H 'Host: jumbo.uz' http://127.0.0.1:8080/api/placement/block/A1 || true)
  [ "$code" = 200 ] && break
  sleep 3
done
echo "api answers: $code"

body='{"method":"CheckTransaction","params":{"id":"000000000000000000000000"},"id":1}'
no_key=$(curl -s -X POST -H 'Host: jumbo.uz' -H 'Content-Type: application/json' -d "$body" http://127.0.0.1:8080/api/payme | grep -o '"code":-\?[0-9]*' | head -1)
auth=$(printf '%s:%s' "$PM_LOGIN" "$PM_KEY" | base64 -w0)
with_key=$(curl -s -X POST -H 'Host: jumbo.uz' -H "Authorization: Basic $auth" -H 'Content-Type: application/json' -d "$body" http://127.0.0.1:8080/api/payme | grep -o '"code":-\?[0-9]*' | head -1)
echo "without the key: $no_key   (must be -32504)"
echo "with the key:    $with_key   (must be -31003)"
'@

$tmp = Join-Path $env:TEMP ('payme-' + [guid]::NewGuid().ToString('N') + '.env')
try {
    $envText = "PM_ID='$merchant'`nPM_LOGIN='$login'`nPM_KEY='$payKey'`nPM_URL='$checkout'`nPM_CHECKIP='$checkIp'`n"
    [IO.File]::WriteAllText($tmp, $envText, (New-Object Text.UTF8Encoding($false)))
    Write-Host 'Sending the settings to the server...' -ForegroundColor Cyan
    scp -i $key -o BatchMode=yes $tmp "${server}:/tmp/payme.env"
    if ($LASTEXITCODE -ne 0) { throw 'scp failed' }
}
finally {
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    $payKey = $null
}

# the script goes as base64 (see deploy-api.ps1: stdin text would carry a byte-order mark)
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($remote -replace "`r`n", "`n")))
ssh -i $key -o BatchMode=yes $server "echo $b64 | base64 -d | bash"
if ($LASTEXITCODE -ne 0) { throw 'Applying on the server failed (the backups ~/env.before-payme-* and ~/compose.before-payme-* are there).' }
Write-Host "Done ($mode). If the second check is not -31003, the login or the key is wrong." -ForegroundColor Green
