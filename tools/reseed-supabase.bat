@echo off
rem ============================================================
rem  STAR:DOM - wipe Supabase and reload it from the repo's SQL
rem
rem  THIS DELETES EVERYTHING IN THE SUPABASE DATABASE. Orders,
rem  payments, users, the QR images and every payment setting are
rem  dropped and replaced by the committed schema + seed. There is
rem  no undo; take a pg_dump first if the data matters.
rem
rem  Why this exists: the portable copy in tools\pgdata is only ever
rem  a development fallback, and nothing in it travels with a clone.
rem  Pointing the app at Supabase instead means every machine reads
rem  and writes the same database, so "which machine has the real
rem  data" stops being a question.
rem
rem  Usage:  tools\reseed-supabase.bat
rem ============================================================
setlocal
set "ROOT=%~dp0"
set "PGBIN=%ROOT%pgsql\bin"
set "SQLDIR=%ROOT%..\STAR-DOM-Web\Database"

call "%ROOT%supabase-env.bat"
if errorlevel 1 exit /b 1

echo.
echo ============================================================
echo   RESET THE SUPABASE DATABASE?
echo ============================================================
echo.
echo   Project:  %SUPABASE_DB_URL%
echo.
echo   Everything in the public schema will be DROPPED, including
echo   orders, payments, users, and the uploaded QR images.
echo   It will then be rebuilt from:
echo     Database\supabase_schema.sql
echo     Database\supabase_seed.sql
echo.
echo   This cannot be undone.
echo.
set /p CONFIRM="   Type RESET to continue (anything else aborts): "
if /i not "%CONFIRM%"=="RESET" (
    echo.
    echo   Cancelled. Nothing was changed.
    pause
    exit /b 0
)

echo.
echo   Testing the connection ...
"%PGBIN%\psql.exe" -tAc "SELECT 1" "%SUPABASE_DSN%" >nul 2>nul
if errorlevel 1 (
    echo.
    echo   Could not connect to Supabase. Check tools\supabase-credentials.txt --
    echo   the password is usually the problem, and Supabase sometimes needs a
    echo   moment after a password reset. Nothing was changed.
    echo.
    "%PGBIN%\psql.exe" -tAc "SELECT 1" "%SUPABASE_DSN%"
    pause
    exit /b 1
)
echo   Connected.

echo.
echo   Dropping and recreating the public schema ...
rem Supabase's PostgREST role needs its grants back on the new schema;
rem without them the API reads nothing even though the tables exist.
"%PGBIN%\psql.exe" -v ON_ERROR_STOP=1 -q -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public; GRANT ALL ON SCHEMA public TO postgres; GRANT ALL ON SCHEMA public TO anon; GRANT ALL ON SCHEMA public TO authenticated; GRANT ALL ON SCHEMA public TO service_role;" "%SUPABASE_DSN%"
if errorlevel 1 (
    echo   Could not reset the schema -- see the error above. Nothing further was done.
    pause
    exit /b 1
)

echo   Loading schema ...
"%PGBIN%\psql.exe" -v ON_ERROR_STOP=1 -q -f "%SQLDIR%\supabase_schema.sql" "%SUPABASE_DSN%"
if errorlevel 1 (
    echo   Schema load FAILED -- see the errors above.
    pause
    exit /b 1
)

echo   Loading seed ...
"%PGBIN%\psql.exe" -v ON_ERROR_STOP=1 -q -f "%SQLDIR%\supabase_seed.sql" "%SUPABASE_DSN%"
if errorlevel 1 (
    echo   Seed load FAILED -- see the errors above.
    pause
    exit /b 1
)

echo.
echo   What is in there now:
"%PGBIN%\psql.exe" -tAc "SELECT '    tables: ' || count(*) FROM information_schema.tables WHERE table_schema='public';" "%SUPABASE_DSN%"
"%PGBIN%\psql.exe" -tAc "SELECT '    users:   ' || count(*) FROM users;" "%SUPABASE_DSN%"
"%PGBIN%\psql.exe" -tAc "SELECT '    orders:  ' || count(*) FROM orders;" "%SUPABASE_DSN%"
"%PGBIN%\psql.exe" -tAc "SELECT '    ' || channel || ' QR image bytes: ' || coalesce(length(qrimagedata),0) FROM paymentsettings ORDER BY channel;" "%SUPABASE_DSN%"
echo.
echo   Done. Start the site with run-website.bat so it uses this database.
echo.
pause
exit /b 0
