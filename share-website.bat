@echo off
rem ============================================================
rem  STAR:DOM Website - share it publicly via a Cloudflare tunnel
rem  Makes sure a database is available, runs IIS Express on port
rem  8095, then opens a free
rem  Cloudflare "quick tunnel" so anyone with the link can view
rem  the site without installing anything on their side.
rem  Close this window to stop sharing.
rem
rem  Database selection is identical to run-website.bat: Supabase when
rem  tools\supabase-credentials.txt exists, otherwise the portable local
rem  copy. Sharing a public tunnel pointed at a private local database
rem  would show seed data to everyone; with Supabase the tunnel and every
rem  other machine read the same thing.
rem ============================================================
setlocal
title STAR:DOM - Share via Cloudflare

rem This script sits in the REPOSITORY ROOT, one level above the site folder,
rem so the site root is the STAR-DOM-Web folder next to it. %~dp0 ends with
rem a backslash and IIS Express rejects a /path:"...\" that ends in one -
rem strip it.
set "SITE=%~dp0STAR-DOM-Web"
if "%SITE:~-1%"=="\" set "SITE=%SITE:~0,-1%"

rem --- locate IIS Express ---
set "IISEXE=C:\Program Files\IIS Express\iisexpress.exe"
if not exist "%IISEXE%" set "IISEXE=C:\Program Files (x86)\IIS Express\iisexpress.exe"
if not exist "%IISEXE%" (
    echo IIS Express is not installed.
    echo Download and install it from: https://www.microsoft.com/en-us/download/details.aspx?id=48264
    pause
    exit /b 1
)

rem --- locate cloudflared: the repo's tools\ copy first, then PATH, then
rem     Program Files. Sequential checks with goto (not nested parentheses):
rem     %CFD% inside a (...) block expands at parse time, which once made the
rem     Program Files check read the wrong value and always fail.
set "CFD=%~dp0tools\cloudflared\cloudflared.exe"
if exist "%CFD%" goto :cfd_found

set "CFD=cloudflared"
where cloudflared >nul 2>nul
if not errorlevel 1 goto :cfd_found

set "CFD=C:\Program Files (x86)\cloudflared\cloudflared.exe"
if exist "%CFD%" goto :cfd_found

echo cloudflared was not found.
echo The repo ships a copy at tools\cloudflared\cloudflared.exe —
echo run "git pull" if it is missing, or install with: winget install Cloudflare.cloudflared
pause
exit /b 1

:cfd_found

rem --- pick the database: Supabase when configured, else the local one ---
rem Exported before IIS Express starts so the worker process inherits it.
if not defined SD_LOCAL_DB (
    if exist "%~dp0tools\supabase-credentials.txt" (
        call "%~dp0tools\supabase-env.bat"
        if errorlevel 1 (
            echo Could not read the Supabase credentials - see the messages above.
            pause
            exit /b 1
        )
        echo Database: Supabase ^(shared - same data on every machine^)
    )
)

rem --- make sure the database is available (starts the local one if needed) ---
call "%~dp0start-db.bat"
if errorlevel 1 (
    echo The database did not start - see the messages above.
    pause
    exit /b 1
)

echo [1/3] Starting IIS Express on http://localhost:8095 ...
start "STAR:DOM - IIS Express" /min "%IISEXE%" /path:"%SITE%" /port:8095 /clr:v4.0 /systray:false

rem --- wait until the site answers an HTTP request ---
echo [2/3] Waiting for the site to come up ...
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
    rem ping-based delay: "timeout" misbehaves when stdin is redirected
    ping -n 2 127.0.0.1 >nul
    goto waitloop
)

echo [3/3] Opening Cloudflare tunnel - copy the trycloudflare.com link below.
echo        Keep this window open. Close it any time to stop sharing.
if not defined SUPABASE_DB_URL (
    echo.
    echo   NOTE: no Supabase credentials, so this link is showing the LOCAL
    echo   database - seed data only. Add tools\supabase-credentials.txt to
    echo   share the real data instead.
)
echo.
"%CFD%" tunnel --url http://localhost:8095 --http-host-header localhost:8095

echo.
echo Tunnel stopped. IIS Express is still running in the background
echo (close its minimized window to stop the website).
echo.
endlocal