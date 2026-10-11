@echo off
rem ============================================================
rem  STAR:DOM Website - quick launcher (no Visual Studio needed)
rem
rem  Starts the site under IIS Express and opens your browser.
rem
rem  The database is Supabase. There is no local database: nothing to
rem  install, nothing to initialise, nothing that can drift per
rem  machine. Every developer sees exactly the same data.
rem
rem  The one thing a clone cannot carry is the database password,
rem  because a password does not belong in git. setup.bat writes it
rem  once into the git-ignored tools\supabase-credentials.txt. If
rem  that file is missing this script says so and stops.
rem
rem  Set SD_PORT to use a port other than 8095.
rem ============================================================
rem enabledelayedexpansion is needed by the IIS lookup below, which trims a
rem trailing backslash out of the registry value inside a for-loop body.
setlocal enabledelayedexpansion
title STAR:DOM Website

rem This script sits in the REPOSITORY ROOT, one level above the site folder,
rem so the site root is the STAR-DOM-Web folder next to it. %~dp0 ends with
rem a backslash and IIS Express rejects a /path:"...\" that ends in one, so
rem strip the trailing backslash.
set "SITE=%~dp0STAR-DOM-Web"
if "%SITE:~-1%"=="\" set "SITE=%SITE:~0,-1%"

rem Locate IIS Express the same way setup.bat does: registry first, then the
rem two Program Files locations. A hardcoded "C:\Program Files\..." only works
rem on a machine that installed it to the default folder, which is exactly the
rem kind of thing that works on one machine and not another.
set "IISEXE="
for /f "tokens=2,*" %%A in ('reg query "HKLM\SOFTWARE\Microsoft\IIS Express\10.0" /v InstallDir 2^>nul ^| findstr /i "InstallDir"') do (
    set "D=%%B"
    if defined D if "!D:~-1!"=="\" set "D=!D:~0,-1!"
    if defined D if exist "!D!\iisexpress.exe" set "IISEXE=!D!\iisexpress.exe"
)
if not defined IISEXE for %%D in ("%ProgramFiles%" "%ProgramFiles(x86)%") do (
    if not defined IISEXE if exist "%%~D\IIS Express\iisexpress.exe" set "IISEXE=%%~D\IIS Express\iisexpress.exe"
)
if not defined IISEXE (
    echo IIS Express is not installed on this machine.
    echo Run setup.bat once - it finds or installs it for you.
    echo Or install it yourself from https://www.microsoft.com/en-us/download/details.aspx?id=48264
    pause
    exit /b 1
)

rem The port is configurable because 8095 is not sacred, and a clash with
rem something else already listening is the most common reason a machine that
rem "worked yesterday" will not start today.
if not defined SD_PORT set "SD_PORT=8095"
set "URL=http://localhost:%SD_PORT%"

rem Exported before IIS Express starts so the worker process inherits it:
rem Code\Db.vb reads SUPABASE_DB_URL, so the site talks to Supabase.
if not exist "%~dp0tools\supabase-credentials.txt" (
    echo The database password is not set up on this machine yet.
    echo.
    echo   tools\supabase-credentials.txt is missing. It is git-ignored
    echo   on purpose, so every clone creates its own copy once.
    echo.
    echo   Run setup.bat once and type the password when asked.
    echo.
    pause
    exit /b 1
)
call "%~dp0tools\supabase-env.bat"
if errorlevel 1 (
    echo Could not read the Supabase credentials - see the messages above.
    echo If the password is still PASTE_PASSWORD_HERE, re-run setup.bat.
    pause
    exit /b 1
)
echo Database: Supabase ^(shared - every machine sees the same data^)

echo [1/2] Starting IIS Express at %URL% ...
start "STAR:DOM - IIS Express" /min "%IISEXE%" /path:"%SITE%" /port:%SD_PORT% /clr:v4.0 /systray:false

rem --- wait until the site answers an HTTP request ---
rem ping-based delay: "timeout" misbehaves when stdin is redirected
set /a tries=0
:waitloop
set /a tries+=1
if %tries% gtr 30 (
    echo The site did not start in time. Is port %SD_PORT% already in use?
    echo Try another port:  set SD_PORT=8096 ^&^& run-website.bat
    pause
    exit /b 1
)
curl.exe -s -o NUL --max-time 2 "%URL%/" >nul 2>nul
if errorlevel 1 (
    ping -n 2 127.0.0.1 >nul
    goto waitloop
)

echo [2/2] Website is up - opening %URL% ...
rem Set SD_NO_BROWSER=1 to skip opening a browser (useful for scripts/CI).
if not defined SD_NO_BROWSER start "" "%URL%"

echo.
echo IIS Express is running minimized in the background.
echo Close its window (or Ctrl+C in it) to stop the website.
echo.
echo Not working? Run doctor.bat - it checks this machine and tells you why.
echo.
endlocal
