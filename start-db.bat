@echo off
rem ============================================================
rem  STAR:DOM database - start / stop helper
rem
rem  Lives in the REPOSITORY ROOT and is called by run-website.bat and
rem  share-website.bat, which sit next to it.
rem
rem  Two databases, and this script only ever manages one of them:
rem
rem    Supabase   the real one. Used whenever SUPABASE_DB_URL is set, which
rem                run-website.bat and share-website.bat do by loading
rem                tools\supabase-credentials.txt. It is already running
rem                in the cloud, so there is nothing to start.
rem
rem    Local      a portable PostgreSQL (tools\pgsql, committed to the repo)
rem                with its data directory in tools\pgdata on
rem                localhost:5432 - the fallback connection string in
rem                STAR-DOM-Web\web.config. Used on a fresh clone that has no
rem                credentials file, so the site works before any setup.
rem
rem  One-time bootstrap for the local copy (fully automatic):
rem    - tools\pgdata missing            -> initdb it (user postgres / pw postgres)
rem    - database "stardom" missing      -> createdb + load supabase_schema.sql
rem                                         and supabase_seed.sql
rem
rem  Set SD_LOCAL_DB=1 to ignore Supabase and force the local copy.
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

rem Supabase is a managed service, so there is no local process to start and
rem no data directory of ours to initialise. Both are wasted work when the
rem app is pointed at the cloud, so bail out early.
if defined SUPABASE_DB_URL if not defined SD_LOCAL_DB (
    echo Using Supabase - no local database to start.
    goto :done
)

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
goto :done

:done
endlocal & exit /b 0

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
if defined SUPABASE_DB_URL if not defined SD_LOCAL_DB (
    echo Configured database: Supabase ^(hosted - no local process^)
    exit /b 0
)
"%PGBIN%\pg_isready.exe" -h localhost -p 5432
exit /b 0
