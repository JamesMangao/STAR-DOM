@echo off
rem ============================================================
rem  Loads the Supabase connection string into the environment.
rem
rem  Code\Db.vb prefers SUPABASE_DB_URL over the localhost fallback in
rem  web.config, so once this has run every page talks to Supabase
rem  instead of the portable copy in tools\pgdata. Nothing else needs
rem  to change: the same code, the same queries, the same schema.
rem
rem  Reads tools\supabase-credentials.txt, which is git-ignored. Call
rem  this from a batch that has not already called setlocal, or the
rem  exported variable will not reach your process tree.
rem ============================================================
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "CREDS=%ROOT%supabase-credentials.txt"

if not exist "%CREDS%" (
    echo.
    echo   No Supabase credentials found.
    echo.
    echo   Create it once:
    echo     copy "%ROOT%supabase-credentials.example.txt" "%CREDS%"
    echo.
    echo   Then paste your connection string into it. Get it from:
    echo     Supabase -^> Project Settings -^> Database -^> Connection string
    echo   Use the SESSION POOLER tab; see the example file for why.
    echo.
    pause
    exit /b 1
)

rem Parse the file line by line, splitting on the first "=". usebackq with a
rem double-quoted name is how a for loop is told to read a FILE -- passing
rem the value itself in quotes would be read as a filename and fail.
set "SUPABASE_DB_URL="
for /f "usebackq tokens=1,* delims==" %%A in ("%CREDS%") do (
    rem Notepad on a fresh file leaves a UTF-8 BOM, which glues itself to the
    rem first key and would stop it matching.
    set "KEY=%%A"
    if defined KEY set "KEY=!KEY: =!"
    if /i "!KEY!"=="SUPABASE_DB_URL" set "SUPABASE_DB_URL=%%B"
)

if "%SUPABASE_DB_URL%"=="" (
    echo.
    echo   SUPABASE_DB_URL is missing or empty in
    echo     %CREDS%
    echo   It should be one line like:
    echo     SUPABASE_DB_URL=postgresql://USER:PASSWORD@POOLERHOST:5432/postgres
    echo.
    pause
    exit /b 1
)

rem Npgsql and psql both take the password out of the URL, so nothing else
rem is needed. Exported to the caller so IIS Express inherits it.
endlocal & set "SUPABASE_DB_URL=%SUPABASE_DB_URL%" & set "SUPABASE_DSN=%SUPABASE_DB_URL%"
exit /b 0