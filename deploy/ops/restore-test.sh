#!/bin/bash
# Proves that a copy can be restored: loads the newest copy into a temporary database next to the real one, counts the users, drops it.
# The real database is not touched. Usage: restore-test.sh [file.sql.gz]
set -o pipefail
FILE="${1:-$(ls -1t /opt/littlepolyglot/backups/db-*.sql.gz | head -1)}"
TEST=restore_test_tmp
PSQL="docker exec -i jumbo-postgres psql -U littlepolyglot"
echo "copy: $FILE"
$PSQL -d postgres -c "DROP DATABASE IF EXISTS $TEST" >/dev/null
$PSQL -d postgres -c "CREATE DATABASE $TEST" >/dev/null || { echo "cannot create the test database"; exit 1; }
zcat "$FILE" | $PSQL -d $TEST -v ON_ERROR_STOP=0 -q >/dev/null 2>/tmp/restore-test.err
USERS=$($PSQL -d $TEST -tAc "select count(*) from users" 2>/dev/null)
REAL=$($PSQL -d littlepolyglot -tAc "select count(*) from users" 2>/dev/null)
BOOKS=$($PSQL -d $TEST -tAc "select count(*) from subjects" 2>/dev/null)
$PSQL -d postgres -c "DROP DATABASE $TEST" >/dev/null
echo "restored users: $USERS (real now: $REAL), topics: $BOOKS, errors in the log: $(grep -c ERROR /tmp/restore-test.err)"
[ -n "$USERS" ] && [ "$USERS" -gt 0 ] && echo "RESTORE OK" || { echo "RESTORE FAILED"; exit 1; }
