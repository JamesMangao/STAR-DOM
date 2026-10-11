@echo off
rem ============================================================
rem  STAR:DOM - one-time setup on a new machine.
rem
rem  Goal: after cloning, you should have to do this ONE
rem  thing and nothing else. Run it, then run-website.bat.
rem
rem  The database is Supabase. There is no local database to fall
rem  back to, so the only thing a clone cannot do for itself is
rem  learn the password -- and a password does not belong in git.
rem  This script asks for it once, behind a masked prompt, and
rem  writes it to the git-ignored tools\supabase-credentials.txt.
rem
rem  What this script does, each step skipped if already done:
rem    1. find or install IIS Express (registry, then ProgramFiles)
rem    2. confirm the .NET Framework 4.8 runtime is present
rem    3. check the website port is free
rem    4. trust the Supabase root CA
rem    5. write the database credentials
rem    6. build the site, then prove Supabase really answers
rem
rem  Every path comes from %~dp0, this script's own folder, so the
rem  repo can sit on any drive in any folder under any name.
rem
rem  Usage:
rem    setup.bat              full setup
rem    setup.bat --check      verify only, change nothing
rem
rem  Run doctor.bat any time to re-check a machine without changing it.
rem ============================================================
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "MSI=%ROOT%tools\downloads\iisexpress_amd64_en-US.msi"
set "CA=%ROOT%tools\supabase\prod-ca-2021.crt"
set "CREDS=%ROOT%tools\supabase-credentials.txt"
set "TEMPLATE=%ROOT%tools\supabase-credentials.example.txt"
set "PS1=%ROOT%tools\set-credentials.ps1"
set "PSQL=%ROOT%tools\pgsql\bin\psql.exe"
set "IISEXE="
set "CA_NAME=Supabase Root 2021 CA"
set "MODE=%~1"
set "CHECKONLY=0"
if /i "%MODE%"=="--check" set "CHECKONLY=1"
set "FAIL=0"
set "WARN=0"

echo.
echo ============================================================
echo   STAR:DOM - first-time machine setup
echo ============================================================
echo   Repo: %ROOT%
echo.

rem ---------- step 1: IIS Express ---------------------------------
echo [1/6] IIS Express ...

call :find_iis
if defined IISEXE (
    echo       Found: %IISEXE%
    goto :iis_ok
)

if not exist "%MSI%" goto :iis_nomsi
call :is_admin
if errorlevel 1 goto :iis_needadmin

echo       Not installed. Installing from the bundled MSI, this
echo       takes a minute and needs administrator rights ...
msiexec /i "%MSI%" /qn /norestart
rem msiexec returns before the files always settle, so wait and re-ask.
ping -n 4 127.0.0.1 >nul
call :find_iis
if defined IISEXE goto :iis_ok
goto :iis_failed

:iis_nomsi
echo       NOT installed, and tools\downloads has no installer.
echo       Install it with:  winget install Microsoft.IISExpress
echo       or download it from microsoft.com ^(search "IIS Express 10"^),
echo       then re-run setup.bat.
set "FAIL=1"
goto :next

:iis_needadmin
echo       NOT installed, and installing it needs administrator.
echo       Close this window, right-click setup.bat, choose
echo       "Run as administrator", and run it again.
set "FAIL=1"
goto :next

:iis_failed
echo       The install did not complete. Check your antivirus, or
echo       install IIS Express by hand and re-run setup.bat.
set "FAIL=1"
goto :next

:iis_ok
echo       OK.

:next
echo.

rem ---------- step 2: .NET Framework 4.8 ---------------------------
echo [2/6] .NET Framework 4.8 runtime ...

rem 461808 is the release key for 4.8 on Windows 10/11. The registry stores it
rem as a DWORD, which reg query prints in hex (0x82405), so PowerShell does the
rem read and the arithmetic rather than comparing hex text against a decimal.
for /f "usebackq delims=" %%R in (`powershell -NoProfile -Command "$k='HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full'; $v=(Get-ItemProperty -Path $k -Name Release -EA SilentlyContinue).Release; if ($null -eq $v) { 'MISSING' } else { '' + $v }" 2^>nul`) do set "RELEASE=%%R"
if not defined RELEASE set "RELEASE=MISSING"
if "!RELEASE!"=="MISSING" (
    echo       Could not read the registry key. If the site fails to
    echo       start, install the .NET Framework 4.8 runtime, then
    echo       re-run setup.bat.
    set "WARN=1"
) else (
    set /a RELNUM=!RELEASE!
    if !RELNUM! LSS 461808 (
        echo       release !RELEASE! is too old - need 461808 or newer ^(4.8^).
        set "WARN=1"
    ) else (
        echo       Found release !RELEASE!.
    )
)

echo.

rem ---------- step 3: the website port ---------------------------
echo [3/6] Port ...

call :port_free 8095
if errorlevel 1 (
    echo       8095 is TAKEN - the website could not bind.
    echo       Close whatever is using it ^(an old iisexpress.exe,^)
    echo       or start the site with:  set SD_PORT=8096 ^&^& run-website.bat
    set "WARN=1"
) else (
    echo       8095 free - the website can bind.
)

echo.

rem ---------- step 4: Supabase CA ---------------------------------
echo [4/6] Supabase root CA ...

certutil -user -store Root 2>nul | findstr /i /c:"%CA_NAME%" >nul
if not errorlevel 1 (
    echo       Already trusted.
    goto :ca_next
)

if not exist "%CA%" (
    echo       Not trusted, and tools\supabase\prod-ca-2021.crt is
    echo       missing from this clone.
    echo       Only needed for Supabase mode, so this is fine on local.
    set "WARN=1"
    goto :ca_next
)

echo       Trusting it in your USER store - no admin rights, no
echo       system-wide change ...
certutil -user -addstore -f Root "%CA%" >nul 2>nul
if errorlevel 1 (
    echo       certutil refused it. Check the file really is the
    echo       Supabase CA before trusting anything.
    set "WARN=1"
) else (
    echo       Trusted.
)

:ca_next
echo.

rem ---------- step 5: credentials ---------------------------------
rem The only thing a clone genuinely cannot do for itself.
echo [5/6] Database credentials ...

call :creds_filled
if not errorlevel 1 (
    echo       Already set up on this machine.
    goto :db_done
)

if "%CHECKONLY%"=="1" (
    echo       MISSING. Run setup.bat without --check and type the
    echo       password when asked.
    set "FAIL=1"
    goto :db_done
)

if not exist "%TEMPLATE%" (
    echo       Cannot continue: tools\supabase-credentials.example.txt
    echo       is missing, so there is nothing to build the file from.
    set "FAIL=1"
    goto :db_done
)

echo       Your password is hidden while you type it and is written
echo       straight to the git-ignored file - no Notepad, nothing left
echo       on screen.
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -Target "%CREDS%"
if errorlevel 1 (
    echo       Could not write the credentials file.
    set "FAIL=1"
    goto :db_done
)
call :creds_filled
if not errorlevel 1 (
    echo       The file still has the placeholder in it.
    set "FAIL=1"
    goto :db_done
)
echo       Credentials written.

:db_done
echo.

rem ---------- step 6: build and prove the database ----------------
echo [6/6] Building the site ...

if not exist "%ROOT%build.bat" (
    echo       build.bat is missing from this clone - skipping.
) else if "%CHECKONLY%"=="1" (
    echo       --check, so not rebuilding.
) else (
    call "%ROOT%build.bat" >"%TEMP%\stardom-build.log" 2>&1
    if errorlevel 1 (
        echo       Build FAILED. The last lines were:
        type "%TEMP%\stardom-build.log" | findstr /i /c:"error" /c:"FAILED"
        echo       Full log: %TEMP%\stardom-build.log
        set "FAIL=1"
    ) else (
        echo       Build OK.
    )
)
del "%TEMP%\stardom-build.log" >nul 2>nul

rem A build that compiles and a database that answers are different
rem problems, so prove the database rather than assuming it.
echo.
echo       Testing the database ...
call :test_db
echo.

echo.
echo ============================================================
if "%FAIL%"=="1" (
    echo   Setup INCOMPLETE - something above failed.
    echo   Fix it and re-run setup.bat. It is safe to re-run.
) else (
    if "%WARN%"=="1" (
        echo   Setup DONE, with warnings above.
        echo   It will probably still work. If it does not, run doctor.bat
    ) else (
        echo   Setup COMPLETE.
    )
)
echo ============================================================
echo.
echo   Next:  run-website.bat
echo   Then sign in as admin / admin123  or  bella / customer123
echo.
if "%FAIL%"=="1" exit /b 1
exit /b 0

rem --- Prove Supabase really answers.
rem --- This is a subroutine, NOT an inline block, on purpose: inside a
rem --- parenthesised block cmd expands %VAR% when the block is PARSED,
rem --- which is before the call that sets the variable has run. The
rem --- DSN would be read as empty and psql would silently try localhost.
:test_db
if not exist "%PSQL%" (
    echo       tools\pgsql\bin\psql.exe is missing from this clone, so
    echo       the connection cannot be tested here.
    set "WARN=1"
    exit /b 0
)
call "%ROOT%tools\supabase-env.bat" >nul 2>&1
if errorlevel 1 (
    echo       Could not read the credentials file.
    set "FAIL=1"
    exit /b 0
)
"%PSQL%" -tA -c "SELECT 1" "%SUPABASE_DSN%" >nul 2>&1
if errorlevel 1 (
    echo       COULD NOT CONNECT. In order of likelihood:
    echo         - the password is wrong
    echo         - you used the Transaction pooler, it must be
    echo           Session, port 5432
    echo         - the CA is not trusted yet, redo step 4
    set "FAIL=1"
    exit /b 0
)
echo       Connected. Supabase answered.
exit /b 0

rem ---------- helpers ---------------------------------------------

rem --- errorlevel 0 when tools\supabase-credentials.txt exists and
rem --- the password is no longer the placeholder
:creds_filled
if not exist "%CREDS%" exit /b 1
findstr /c:"PASTE_PASSWORD_HERE" "%CREDS%" >nul 2>nul
if not errorlevel 1 exit /b 1
exit /b 0

rem --- locate iisexpress.exe: registry first (this is where the
rem --- installer records the truth), then the two Program Files
rem --- locations, then PATH. Checking the registry first is what
rem --- makes this work on a machine where IIS Express is somewhere
rem --- other than the default folder.
:find_iis
set "IISEXE="
for /f "tokens=2,*" %%A in ('reg query "HKLM\SOFTWARE\Microsoft\IIS Express\10.0" /v InstallDir 2^>nul ^| findstr /i "InstallDir"') do (
    set "D=%%B"
    if defined D if "!D:~-1!"=="\" set "D=!D:~0,-1!"
    if defined D if exist "!D!\iisexpress.exe" set "IISEXE=!D!\iisexpress.exe"
)
if defined IISEXE exit /b 0
for %%D in ("%ProgramFiles%" "%ProgramFiles(x86)%") do (
    if not defined IISEXE if exist "%%~D\IIS Express\iisexpress.exe" set "IISEXE=%%~D\IIS Express\iisexpress.exe"
)
if defined IISEXE exit /b 0
for /f "delims=" %%P in ('where iisexpress.exe 2^>nul') do (
    if not defined IISEXE set "IISEXE=%%P"
)
exit /b 0

:is_admin
net session >nul 2>&1
exit /b 0

rem --- errorlevel 1 when the port is NOT free
:port_free
powershell -NoProfile -Command "if (Get-NetTCPConnection -LocalPort %~1 -State Listen -EA SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>nul
if errorlevel 1 (
    rem Get-NetTCPConnection is not on every SKU; fall back to netstat.
    netstat -ano | findstr /r /c:":%~1 .*LISTENING" >nul 2>nul && exit /b 1
    exit /b 0
)
exit /b 0