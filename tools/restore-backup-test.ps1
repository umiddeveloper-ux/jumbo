# Proves, on THIS computer, that a database backup of Jumbo can be restored and is complete. The newest copy from D:\JumboBackups (made
# nightly on the server and pulled by tools\pull-backup.ps1) is loaded into a scratch database of the local docker Postgres
# (container jumbo-postgres), checked, and the scratch database is dropped again. The development database (littlepolyglot) and
# the test database (jumbo_tests) are never touched.
#
#   .\tools\restore-backup-test.ps1                       newest copy in D:\JumboBackups
#   .\tools\restore-backup-test.ps1 -File D:\JumboBackups\db-20261126-0330.sql.gz
#   .\tools\restore-backup-test.ps1 -Keep                 keep the scratch database (jumbo_restore_test) to look at it, drop it by hand later
#
# Needs Docker Desktop running with the container jumbo-postgres. Exit code 0 = all checks passed, 1 = something failed.
# (The server has its own simpler check: deploy/ops/restore-test.sh. How to restore for real: deploy/ops/RESTORE.md.)
param(
    [string]$File,
    [string]$Folder = 'D:\JumboBackups',
    [string]$Container = 'jumbo-postgres',
    [string]$Db = 'jumbo_restore_test',
    [switch]$Keep
)
$ErrorActionPreference = 'Continue'
$root = Split-Path $PSScriptRoot -Parent
if ($Db -in @('littlepolyglot', 'jumbo_tests', 'postgres', 'template0', 'template1')) { throw "Refusing to use '$Db' as the scratch database." }
if (-not $File) {
    $newest = Get-ChildItem $Folder -Filter 'db-*.sql.gz' -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $newest) { throw "No db-*.sql.gz in $Folder (run tools\pull-backup.ps1 first)." }
    $File = $newest.FullName
}
if (-not (Test-Path $File)) { throw "No such file: $File" }

$failed = @()
$warned = @()
function Pass($t) { Write-Host "  OK    $t" -ForegroundColor Green }
function Fail($t) { Write-Host "  FAIL  $t" -ForegroundColor Red; $script:failed += $t }
function Warn($t) { Write-Host "  WARN  $t" -ForegroundColor Yellow; $script:warned += $t }
# a query on the scratch database (or another): the text of the answer, one value per line
function Q($sql, $db = $Db) {
    # (Windows PowerShell 5.1 drops the double quotes of an argument for a program: they are escaped for it)
    $arg = $sql -replace '"', '\"'
    (docker exec $Container psql -U littlepolyglot -d $db -At -c $arg 2>$null) -join "`n"
}

docker exec $Container true 2>$null
if ($LASTEXITCODE -ne 0) { throw "The container '$Container' is not running (start Docker Desktop)." }

$sizeMb = [math]::Round((Get-Item $File).Length / 1MB, 1)
Write-Host "Backup: $File ($sizeMb MB, $((Get-Item $File).LastWriteTime))" -ForegroundColor Cyan

# 1. load it. The file goes into the container and is unpacked there: a PowerShell pipe would damage the bytes.
Write-Host 'Restoring...' -ForegroundColor Cyan
docker exec $Container psql -U littlepolyglot -d postgres -q -c "DROP DATABASE IF EXISTS $Db" -c "CREATE DATABASE $Db" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot create the scratch database.' }
docker cp $File "${Container}:/tmp/restore-test.sql.gz" | Out-Null
$sw = [Diagnostics.Stopwatch]::StartNew()
docker exec $Container sh -c "gunzip -c /tmp/restore-test.sql.gz | psql -U littlepolyglot -d $Db -v ON_ERROR_STOP=1 -q -o /dev/null 2>/tmp/restore-test.err"
$code = $LASTEXITCODE
$sw.Stop()
$errors = [int](docker exec $Container sh -c 'grep -c ERROR /tmp/restore-test.err' 2>$null)
$firstError = (docker exec $Container sh -c 'grep -m1 ERROR /tmp/restore-test.err' 2>$null)
docker exec $Container rm -f /tmp/restore-test.sql.gz /tmp/restore-test.err | Out-Null
Write-Host ("Checks (restore took {0:N1} s):" -f $sw.Elapsed.TotalSeconds) -ForegroundColor Cyan

if ($code -eq 0 -and $errors -eq 0) { Pass 'the file loads without a single error (the owner role littlepolyglot exists, no extensions needed)' }
else { Fail "the load stopped (exit $code, $errors errors): $firstError" }

# 2. the tables, the keys, the indexes: the same as in the development database
$tables = Q "select count(*) from pg_tables where schemaname='public'"
$devTables = Q "select count(*) from pg_tables where schemaname='public'" 'littlepolyglot'
if ([int]$tables -ge 30) { Pass "$tables tables" } else { Fail "only $tables tables" }
if ($devTables -and $tables -ne $devTables) {
    $list = "select tablename from pg_tables where schemaname='public' order by 1"
    $diff = Compare-Object ((Q $list) -split "`n") ((Q $list 'littlepolyglot') -split "`n") | ForEach-Object { $_.InputObject }
    Warn "tables differ from the development database ($tables here, $devTables there): $($diff -join ', ') (a deploy newer or older than the copy?)"
}
$fk = Q "select count(*) from pg_constraint where contype='f'"
$idx = Q "select count(*) from pg_indexes where schemaname='public'"
$devFk = Q "select count(*) from pg_constraint where contype='f'" 'littlepolyglot'
$devIdx = Q "select count(*) from pg_indexes where schemaname='public'" 'littlepolyglot'
if ($fk -eq $devFk -and $idx -eq $devIdx) { Pass "$fk foreign keys and $idx indexes, the same as in the development database" }
else { Warn "foreign keys / indexes: $fk / $idx here, $devFk / $devIdx in the development database" }

# 3. the data: the important tables are not empty
$must = 'users', 'words', 'subjects', 'svg_images', 'languages', 'awards', 'user_subject_progresses', 'users_words'
$counts = @()
foreach ($t in $must) {
    $n = [int](Q "select count(*) from $t")
    $counts += "$t=$n"
    if ($n -le 0) { Fail "table $t is empty" }
}
Pass ('row counts: ' + ($counts -join ', '))
$others = 'school_classes', 'schools', 'teacher_exams', 'exam_results', 'exam_mistakes', 'user_awards', 'user_daily_words', 'placement_questions', 'placement_passages', 'placement_audios', 'telegram_connections'
Write-Host ('        also: ' + (($others | ForEach-Object { "$_=$(Q "select count(*) from $_")" }) -join ', '))

# 4. what must be inside, not just present
$users = Q 'select count(*), count(access_token), count(password_hash), count(*) filter (where role = 3) from users'
$u = $users -split '\|'
if ([int]$u[0] -gt 0 -and $u[0] -eq $u[1]) { Pass "users: $($u[0]) accounts, all with an access token; $($u[2]) with a password, $($u[3]) admin(s)" }
else { Fail "users: $users (accounts / tokens / passwords / admins)" }
$pid1 = Q 'select public_id from users order by user_id limit 1'
if ($pid1 -match '^[A-Z][0-9]{7}$') { Pass "the stored public id is rebuilt ($pid1)" } else { Fail "public_id of the first user is '$pid1'" }

$last = Q 'select migration_id from "__EFMigrationsHistory" order by migration_id desc limit 1'
$repoLast = Get-ChildItem (Join-Path $root 'Jumbo.Api\Data\Migrations') -Filter '*.cs' | Where-Object { $_.Name -notmatch 'Designer|Snapshot' } |
            Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty BaseName
if ($last -eq $repoLast) { Pass "the newest migration in the copy is the newest in the code ($last)" }
elseif ($last) { Warn "the copy ends at migration $last, the code at $repoLast (a deploy after the copy was made, or not deployed yet)" }
else { Fail 'no migration history in the copy' }

$img = (Q 'select count(*) filter (where image_file is not null), count(*) from svg_images') -split '\|'
if ([int]$img[0] -gt 0) { Pass "picture file names: $($img[0]) of $($img[1]) pictures have a file name" } else { Warn 'no picture has a file name (the copy is from before the picture files, or the API did not start yet)' }

# 5. the counters that give out the next number must be ahead of the data, or the first new row would collide
$seqBad = @()
foreach ($p in @(@('users', 'user_id'), @('words', 'word_id'), @('subjects', 'subject_id'), @('svg_images', 'svg_image_id'), @('exam_results', 'exam_result_id'), @('users_words', 'users_word_id'))) {
    $max = Q "select coalesce(max($($p[1])), 0) from $($p[0])"
    $seq = Q "select coalesce(last_value, 0) from pg_sequences where schemaname='public' and sequencename='$($p[0])_$($p[1])_seq'"
    if (-not $seq -or [long]$seq -lt [long]$max) { $seqBad += "$($p[0]): max $max, counter $seq" }
}
if ($seqBad.Count -eq 0) { Pass 'the id counters are ahead of the data' } else { Fail ('id counters behind the data: ' + ($seqBad -join '; ')) }

# 6. clean up
if ($Keep) { Write-Host "The scratch database '$Db' is kept. Drop it with: docker exec $Container psql -U littlepolyglot -d postgres -c `"DROP DATABASE $Db`"" -ForegroundColor Yellow }
else { docker exec $Container psql -U littlepolyglot -d postgres -q -c "DROP DATABASE $Db" | Out-Null }

Write-Host ''
if ($failed.Count -eq 0) {
    $w = if ($warned.Count) { " ($($warned.Count) warning(s))" } else { '' }
    Write-Host ("RESTORE OK in {0:N1} s{1}" -f $sw.Elapsed.TotalSeconds, $w) -ForegroundColor Green
    exit 0
}
Write-Host "RESTORE FAILED: $($failed.Count) check(s)" -ForegroundColor Red
exit 1
