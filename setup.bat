@echo off
rem ============================================================
rem  STAR:DOM - one-time setup on a new machine.
rem
rem  Do the three things a fresh clone cannot do for itself:
rem    1. install IIS Express, from the MSI already in the repo
rem    2. trust the Supabase root CA in your USER certificate
rem       store, so the pooler handshake is not rejected
rem    3. create tools\supabase-credentials.txt from the
rem       template and let you paste the database password
rem
rem  Then it verifies the database really answers.
rem
rem  Every path comes from %~dp0, this script's own folder, so the
rem  repo can sit on any drive in any folder under any name.
rem
rem  Usage:  setup.bat
rem
rem  Step 1 is the only one that needs administrator rights, and
rem  only when IIS Express is not installed yet. The script tells
rem  you if that is the case instead of failing silently.
rem ============================================================
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "MSI=%ROOT%tools\downloads\iisexpress_amd64_en-US.msi"
set "CA=%ROOT%tools\supabase\prod-ca-2021.crt"
set "CREDS=%ROOT%tools\supabase-credentials.txt"
set "TEMPLATE=%ROOT%tools\supabase-credentials.example.txt"
set "IISEXE=%ProgramFiles%\IIS Express\iisexpress.exe"
set "PSQL=%ROOT%tools\pgsql\bin\psql.exe"

set "CA_NAME=Supabase Root 2021 CA"
set "OKCOUNT=0"
set "TODO=0"

echo.
echo ============================================================
echo   STAR:DOM - first-time machine setup
echo ============================================================
echo   Repo: %ROOT%
echo.

rem ---------- step 1: IIS Express ---------------------------------
echo [1/4] IIS Express ...

if exist "%IISEXE%" goto :iis_ok

if not exist "%MSI%" goto :iis_nomsi

call :is_admin
if errorlevel 1 goto :iis_needadmin

echo       Installing from the bundled MSI. This takes a minute ...
msiexec /i "%MSI%" /qn /norestart
if errorlevel 1 goto :iis_failed

rem msiexec returns before the files always settle, so confirm.
ping -n 4 127.0.0.1 >nul
if exist "%IISEXE%" goto :iis_ok
goto :iis_failed

:iis_nomsi
echo       NOT FOUND and the bundled installer is missing.
echo       Install it yourself, then re-run setup.bat:
echo         winget install Microsoft.IISExpress
set "TODO=1"
goto :next

:iis_needadmin
echo       NOT installed. Installing it needs administrator rights.
echo       Close this, right-click setup.bat, "Run as administrator",
echo       then run it again.
set "TODO=1"
goto :next

:iis_failed
echo       The install did not complete. Check your antivirus or
echo       install IIS Express by hand, then re-run setup.bat.
set "TODO=1"
goto :next

:iis_ok
echo       Already installed.
set "OKCOUNT=!OKCOUNT!+1"

:next
echo.

rem ---------- step 2: Supabase root CA ----------------------------
echo [2/4] Supabase root CA ...

if not exist "%CA%" goto :ca_nofile

certutil -user -store Root | findstr /i /c:"%CA_NAME%" >nul
if not errorlevel 1 goto :ca_ok

echo       Trusting it in your USER store, no admin needed ...
certutil -user -addstore -f Root "%CA%" >nul
if errorlevel 1 goto :ca_failed
goto :ca_ok

:ca_nofile
echo       NOT FOUND. The file %CA% is missing from this clone.
set "TODO=1"
goto :next2

:ca_failed
echo       certutil refused. Check the file is really the
echo       Supabase CA before trusting anything.
set "TODO=1"
goto :next2

:ca_ok
echo       Trusted.
set "OKCOUNT=!OKCOUNT!+1"

:next2
echo.

rem ---------- step 3: credentials file ---------------------------
echo [3/4] Database credentials ...

if exist "%CREDS%" goto :creds_exist

if not exist "%TEMPLATE%" goto :creds_notemplate

copy "%TEMPLATE%" "%CREDS%" >nul
if errorlevel 1 goto :creds_copyfail

echo       Created tools\supabase-credentials.txt from the template.
echo       Notepad is opening. Paste your database password in place of
echo       PASTE_PASSWORD_HERE, save, then come back here.
echo.
start "" notepad "%CREDS%"
pause

:creds_exist
findstr /c:"PASTE_PASSWORD_HERE" "%CREDS%" >nul
if errorlevel 1 goto :creds_ready
echo       Still says PASTE_PASSWORD_HERE - edit the file before running.
echo         notepad "%CREDS%"
set "TODO=1"
goto :next3

:creds_notemplate
echo       Neither the template nor the credentials file exists here.
set "TODO=1"
goto :next3

:creds_copyfail
echo       Could not create the file. Check folder permissions.
set "TODO=1"
goto :next3

:creds_ready
echo       Present.
set "OKCOUNT=!OKCOUNT!+1"

:next3
echo.

rem ---------- step 4: can we actually reach the database? --------
echo [4/4] Testing the connection ...

if "%TODO%"=="0" goto :conn_try
if exist "%CREDS%" goto :conn_try
echo       Skipped, credentials are not ready yet.
goto :summary

:conn_try
if not exist "%PSQL%" goto :conn_nopsql

call "%ROOT%tools\supabase-env.bat" >nul 2>&1
if errorlevel 1 goto :conn_envfail

"%PSQL%" -tA -c "SELECT 1" "%SUPABASE_DSN%" >nul 2>&1
if errorlevel 1 goto :conn_fail

echo       Connected. Supabase answered.
set "OKCOUNT=!OKCOUNT!+1"
goto :summary

:conn_nopsql
echo       Skipped, tools\pgsql is not in this clone.
goto :summary

:conn_envfail
echo       Could not read the credentials file. See its comments.
set "TODO=1"
goto :summary

:conn_fail
echo       Could not connect. In order of likelihood:
echo         - the password is wrong, or still PASTE_PASSWORD_HERE
echo         - you used the Transaction pooler, it must be Session, 5432
echo         - the CA is not trusted yet, redo step 2
set "TODO=1"

:summary
echo.
echo ============================================================
if "%TODO%"=="0" goto :allgood
echo   Setup is INCOMPLETE. Fix the item above, then re-run setup.bat.
echo ============================================================
echo.
echo   Start the site with run-website.bat
echo.
exit /b 1

:allgood
echo   Setup is COMPLETE.
echo ============================================================
echo.
echo   Start the site with run-website.bat
echo   Sign in as admin / admin123 or bella / customer123
echo.
exit /b 0

rem ---------- helpers ---------------------------------------------

:is_admin
net session >nul 2>&1
exit /b 0