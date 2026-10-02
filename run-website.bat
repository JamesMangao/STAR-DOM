@echo off
rem ============================================================
rem  STAR:DOM Website - quick launcher (no Visual Studio needed)
rem  Makes sure a database is available, runs the site under IIS
rem  Express, and opens your browser.
rem
rem  Which database depends on one file:
rem
rem    tools\supabase-credentials.txt present  ->  Supabase. Everyone
rem        sees the same data, and the QR codes come back with it. This is
rem        the intended setup; see tools\supabase-credentials.example.txt.
rem
rem    absent  ->  the portable PostgreSQL in tools\pgdata, created on
rem        first run. Keeps a fresh clone working with no setup at all.
rem
rem  Set SD_LOCAL_DB=1 to force the local database even when Supabase is
rem  configured, which is handy when comparing the two.
rem ============================================================
setlocal
title STAR:DOM Website

rem This script sits in the REPOSITORY ROOT, one level above the site folder,
rem so the site root is the STAR-DOM-Web folder next to it. %~dp0 ends with
rem a backslash and IIS Express rejects a /path:"...\" that ends in one, so
rem strip the trailing backslash.
set "SITE=%~dp0STAR-DOM-Web"
if "%SITE:~-1%"=="\" set "SITE=%SITE:~0,-1%"

set "IISEXE=C:\Program Files\IIS Express\iisexpress.exe"
if not exist "%IISEXE%" set "IISEXE=C:\Program Files (x86)\IIS Express\iisexpress.exe"
if not exist "%IISEXE%" (
    echo IIS Express is not installed.
    echo The repo ships the installer at tools\downloads\iisexpress_amd64_en-US.msi -
    echo double-click it, or download from https://www.microsoft.com/en-us/download/details.aspx?id=48264
    pause
    exit /b 1
)

rem Exported before IIS Express starts so the worker process inherits it:
rem Code\Db.vb reads SUPABASE_DB_URL first and only falls back to the
rem localhost connection string in web.config when it is not set.
if not defined SD_LOCAL_DB (
    if exist "%~dp0tools\supabase-credentials.txt" (
        call "%~dp0tools\supabase-env.bat"
        if errorlevel 1 (
            echo Could not read the Supabase credentials - see the messages above.
            pause
            exit /b 1
        )
        echo [0/3] Database: Supabase ^(shared - same data on every machine^)
    )
)

echo [1/3] Starting the STAR:DOM database on localhost:5432 ...
call "%~dp0start-db.bat"
if errorlevel 1 (
    echo The database did not start - see the messages above.
    pause
    exit /b 1
)

echo [2/3] Starting IIS Express at http://localhost:8095 ...
start "STAR:DOM - IIS Express" /min "%IISEXE%" /path:"%SITE%" /port:8095 /clr:v4.0 /systray:false

rem --- wait until the site answers an HTTP request ---
rem ping-based delay: "timeout" misbehaves when stdin is redirected
set /a tries=0
:waitloop
set /a tries+=1
if %tries% gtr 30 (
    echo The site did not start in time. Is port 8095 already in use?
    pause
    exit /b 1
)
curl.exe -s -o NUL --max-time 2 http://localhost:8095/ >nul 2>nul
if errorlevel 1 (
    ping -n 2 127.0.0.1 >nul
    goto waitloop
)

echo [3/3] Website is up - opening http://localhost:8095 ...
rem Set SD_NO_BROWSER=1 to skip opening a browser (useful for scripts/CI).
if not defined SD_NO_BROWSER start http://localhost:8095

echo.
echo IIS Express is running minimized in the background.
echo Close its window (or Ctrl+C in it) to stop the website.
if defined SUPABASE_DB_URL (
    echo.
    echo This site is reading and writing Supabase, so orders placed here
    echo are visible from every machine. To use a private local copy
    echo instead, start it with:  set SD_LOCAL_DB=1 ^&^& run-website.bat
)
echo.
endlocal
