# Builds the SIGNED files of Jumbo with the release key:
#   - the AAB for Google Play
#   - the APK to hand to colleagues (installs directly on an Android phone)
# Run in PowerShell:  .\build-release.ps1
# The password of the key is asked here, kept only for this run and never written anywhere.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$keystore = Join-Path $here 'signing\jumbo-release.keystore'
if (-not (Test-Path $keystore)) { throw "Нет файла ключа: $keystore" }

# the version number is read from the project: the files are named after it
$csproj = Get-Content (Join-Path $here 'Jumbo.csproj') -Raw
$code = [regex]::Match($csproj, '<ApplicationVersion>(\d+)</ApplicationVersion>').Groups[1].Value
$name = [regex]::Match($csproj, '<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>').Groups[1].Value
$out = Join-Path $here 'release-out'
New-Item -ItemType Directory -Force $out | Out-Null

$secure = Read-Host 'Пароль ключа Jumbo' -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
$env:JUMBO_KEY_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

function Build($format, $extension) {
    Write-Host "Сборка $($extension.ToUpper()) (несколько минут)..." -ForegroundColor Cyan
    dotnet publish "$here\Jumbo.csproj" -f net10.0-android -c Release "-p:AndroidPackageFormat=$format"
    if ($LASTEXITCODE -ne 0) { throw "Сборка $extension не удалась. Запустите скрипт ещё раз." }
    $file = Get-ChildItem "$here\bin\Release\net10.0-android" -Recurse -Filter "*-Signed.$extension" |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $file) { throw "Файл .$extension не найден." }
    $target = Join-Path $out "Jumbo-v$name-code$code.$extension"
    Copy-Item $file.FullName $target -Force
    return $target
}

try {
    $aab = Build 'aab' 'aab'
    $apk = Build 'apk' 'apk'

    Write-Host 'Проверка подписи AAB:' -ForegroundColor Cyan
    & 'C:\Program Files\Android\openjdk\jdk-21.0.8\bin\jarsigner.exe' -verify -verbose:summary $aab
    Write-Host 'Проверка подписи APK:' -ForegroundColor Cyan
    $apksigner = Get-ChildItem 'C:\Program Files (x86)\Android\android-sdk\build-tools' -Recurse -Filter apksigner.bat -ErrorAction SilentlyContinue | Select-Object -Last 1
    if ($apksigner) { & $apksigner.FullName verify --print-certs $apk } else { & 'C:\Program Files\Android\openjdk\jdk-21.0.8\bin\jarsigner.exe' -verify -verbose:summary $apk }

    Write-Host ''
    Write-Host 'Готово. Файлы лежат в папке:' -ForegroundColor Green
    Write-Host "  $out" -ForegroundColor Green
    Get-ChildItem $out | ForEach-Object { Write-Host ("  " + $_.Name + "  " + [math]::Round($_.Length / 1MB, 1) + " МБ") }
}
finally {
    Remove-Item Env:\JUMBO_KEY_PASSWORD -ErrorAction SilentlyContinue
}
