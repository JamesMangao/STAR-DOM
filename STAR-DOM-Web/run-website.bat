@echo off
rem ============================================================
rem  STAR:DOM Website - quick launcher (no Visual Studio needed)
rem  Runs the site under IIS Express and opens your browser.
rem ============================================================
setlocal
title STAR:DOM Website

set "SITE=%~dp0STAR-DOM-Web"
set "IISEXE=C:\Program Files\IIS Express\iisexpress.exe"
if not exist "%IISEXE%" set "IISEXE=C:\Program Files (x86)\IIS Express\iisexpress.exe"
if not exist "%IISEXE%" (
    echo IIS Express is not installed.
    echo Download and install it from: https://www.microsoft.com/en-us/download/details.aspx?id=48264
    pause
    exit /b 1
)

echo Starting STAR:DOM at http://localhost:8095 ...
start "STAR:DOM - IIS Express" /min "%IISEXE%" /path:"%SITE%" /port:8095 /clr:v4.0 /systray:false
timeout /t 2 /nobreak >nul
start http://localhost:8095

echo.
echo IIS Express is now running minimized in the background.
echo Close its window (or Ctrl+C in it) to stop the website.
echo.
endlocal
