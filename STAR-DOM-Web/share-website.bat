@echo off
rem ============================================================
rem  STAR:DOM Website - share it publicly via a Cloudflare tunnel
rem  Starts IIS Express on port 8095, then opens a free
rem  Cloudflare "quick tunnel" so anyone with the link can view
rem  the site without installing anything on their side.
rem  Close this window to stop sharing.
rem ============================================================
setlocal
title STAR:DOM - Share via Cloudflare

set "SITE=%~dp0STAR-DOM-Web"

rem --- locate IIS Express ---
set "IISEXE=C:\Program Files\IIS Express\iisexpress.exe"
if not exist "%IISEXE%" set "IISEXE=C:\Program Files (x86)\IIS Express\iisexpress.exe"
if not exist "%IISEXE%" (
    echo IIS Express is not installed.
    echo Download and install it from: https://www.microsoft.com/en-us/download/details.aspx?id=48264
    pause
    exit /b 1
)

rem --- locate cloudflared ---
set "CFD=cloudflared"
where cloudflared >nul 2>nul
if errorlevel 1 (
    set "CFD=C:\Program Files (x86)\cloudflared\cloudflared.exe"
    if not exist "%CFD%" (
        echo cloudflared was not found.
        echo Install it with: winget install cloudflare.cloudflared
        pause
        exit /b 1
    )
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
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo [3/3] Opening Cloudflare tunnel - copy the trycloudflare.com link below.
echo        Keep this window open. Close it any time to stop sharing.
echo.
"%CFD%" tunnel --url http://localhost:8095 --http-host-header localhost:8095

echo.
echo Tunnel stopped. IIS Express is still running in the background
echo (close its minimized window to stop the website).
echo.
endlocal