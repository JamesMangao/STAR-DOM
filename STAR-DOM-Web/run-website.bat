@echo off
rem ============================================================
rem  STAR:DOM Website - quick launcher (no Visual Studio needed)
rem  Starts the local PostgreSQL database, runs the site under
rem  IIS Express, and opens your browser.
rem ============================================================
setlocal
title STAR:DOM Website

rem This script lives INSIDE the STAR-DOM-Web site folder, so the
rem site root is simply the folder this script sits in. %~dp0 ends
rem with a backslash and IIS Express rejects a /path:"...\" that
rem ends in one, so strip the trailing backslash.
set "SITE=%~dp0"
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

echo [1/3] Starting the STAR:DOM database on localhost:5432 ...
call "%~dp0..\start-db.bat"
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
echo.
endlocal
