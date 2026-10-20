# Copies the newest database backups from the server to this computer (outside the project folder and outside git).
# Run it by hand, or let Windows run it every day (Task Scheduler). Needs the deploy key (~/.ssh/jumbo_deploy).
# The copies on the server are made at night by deploy/ops/backup-db.sh (the newest 14 are kept there).
param(
    [string]$Target = "D:\JumboBackups",
    [int]$Keep = 60
)
$ErrorActionPreference = "Stop"
$server = "ubuntu@46.8.195.183"
$key = Join-Path $HOME ".ssh\jumbo_deploy"

New-Item -ItemType Directory -Force -Path $Target | Out-Null
$latest = (ssh -i $key -o ConnectTimeout=20 $server "ls -1t /opt/littlepolyglot/backups/db-*.sql.gz | head -3").Trim() -split "`n"
foreach ($remote in $latest) {
    $name = Split-Path $remote -Leaf
    $local = Join-Path $Target $name
    if (-not (Test-Path $local)) {
        scp -i $key -o ConnectTimeout=20 "${server}:$remote" $local
        Write-Host "copied $name ($([math]::Round((Get-Item $local).Length / 1KB)) KB)"
    }
}
# only the newest $Keep copies stay on this computer
Get-ChildItem $Target -Filter "db-*.sql.gz" | Sort-Object LastWriteTime -Descending | Select-Object -Skip $Keep | Remove-Item -Force
Write-Host "in $Target now: $((Get-ChildItem $Target -Filter 'db-*.sql.gz').Count) copies"
