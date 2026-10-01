@echo off
rem ============================================================
rem  STAR:DOM local PostgreSQL - start / stop helper
rem
rem  The website talks PostgreSQL via Npgsql. This helper runs a
rem  portable PostgreSQL (tools\pgsql, committed to the repo) with
rem  its data directory in tools\pgdata, listening on localhost:5432
rem  - exactly what STAR-DOM-Web\web.config's fallback connection
rem  string expects:
rem      Host=localhost;Port=5432;Database=stardom;Username=postgres;Password=postgres
rem
rem  One-time bootstrap (fully automatic on a fresh clone):
rem    - tools\pgdata missing            -> initdb it (user postgres / pw postgres)
rem    - database "stardom" missing      -> createdb + load supabase_schema.sql
rem                                         and supabase_seed.sql
rem
rem  Usage:
rem    start-db.bat          start the database if it is not running
rem    start-db.bat stop     stop the database
rem    start-db.bat status   show whether it is accepting connections
rem ============================================================
setlocal
rem No "title" here on purpose: run-website.bat and share-website.bat "call"
rem this script, and a title set here would replace their window titles for good.

set "ROOT=%~dp0"
set "PGBIN=%ROOT%tools\pgsql\bin"
set "PGDATA=%ROOT%tools\pgdata"
set "SQLDIR=%ROOT%STAR-DOM-Web\Database"

if not exist "%PGBIN%\pg_ctl.exe" (
    echo PostgreSQL binaries not found at %PGBIN%
    echo Expected the committed layout: tools\pgsql\bin\pg_ctl.exe
    pause
    exit /b 1
)

if "%~1"=="stop" goto :stop
if "%~1"=="status" goto :status

:ensure-running
if not exist "%PGDATA%\PG_VERSION" call :initdb
if not exist "%PGDATA%\PG_VERSION" (
    echo Data directory was not initialised at %PGDATA%
    pause
    exit /b 1
)

"%PGBIN%\pg_isready.exe" -h localhost -p 5432 >nul 2>nul
if not errorlevel 1 (
    echo Database is already running on localhost:5432.
    goto :bootstrap
)

echo Starting STAR:DOM database on localhost:5432 ...
rem -w waits until the server is ready (or fails loudly);
rem stdin/stdout are redirected so pg_ctl detaches from this console.
"%PGBIN%\pg_ctl.exe" -D "%PGDATA%" -l "%PGDATA%\log.txt" -o "-p 5432" -w start <nul >nul 2>nul

set /a tries=0
:waitloop
set /a tries+=1
if %tries% gtr 15 (
    echo The database did not come up in time. Check %PGDATA%\log.txt
    pause
    exit /b 1
)
"%PGBIN%\pg_isready.exe" -h localhost -p 5432 >nul 2>nul
if errorlevel 1 (
    rem ping-based delay: "timeout" hangs when this script's stdin is redirected
    ping -n 2 127.0.0.1 >nul
    goto waitloop
)
echo Database is up.

:bootstrap
rem ---- one-time bootstrap: create the stardom DB and load schema + seed ----
set "PGPASSWORD=postgres"
"%PGBIN%\psql.exe" -h localhost -U postgres -tAc "SELECT 1 FROM pg_database WHERE datname='stardom'" 2>nul | findstr "1" >nul
if errorlevel 1 (
    echo First run: creating the stardom database and loading schema + seed ...
    echo This takes a minute and runs only once.
    "%PGBIN%\createdb.exe" -h localhost -U postgres stardom
    "%PGBIN%\psql.exe" -h localhost -U postgres -d stardom -v ON_ERROR_STOP=1 -q -f "%SQLDIR%\supabase_schema.sql"
    if errorlevel 1 (
        echo Schema load FAILED - see the errors above.
        pause
        exit /b 1
    )
    "%PGBIN%\psql.exe" -h localhost -U postgres -d stardom -v ON_ERROR_STOP=1 -q -f "%SQLDIR%\supabase_seed.sql"
    if errorlevel 1 (
        echo Seed load FAILED - see the errors above.
        pause
        exit /b 1
    )
    echo Database ready: stardom - 28 tables, seeded.
)
exit /b 0

:initdb
echo First run: initialising the PostgreSQL data directory ...
echo postgres> "%TEMP%\stardom-pgpw.txt"
"%PGBIN%\initdb.exe" -D "%PGDATA%" -U postgres -A scram-sha-256 --pwfile="%TEMP%\stardom-pgpw.txt" -E UTF8 >nul
if errorlevel 1 (
    echo initdb FAILED - see the errors above.
    pause
    exit /b 1
)
del "%TEMP%\stardom-pgpw.txt" >nul 2>nul
exit /b 0

:stop
echo Stopping STAR:DOM database ...
"%PGBIN%\pg_ctl.exe" -D "%PGDATA%" -m fast stop <nul >nul 2>nul
echo Stopped.
exit /b 0

:status
"%PGBIN%\pg_isready.exe" -h localhost -p 5432
exit /b 0
