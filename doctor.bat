@echo off
rem ============================================================
rem  STAR:DOM - machine diagnostic.
rem
rem  Changes NOTHING. It only looks, so it is safe to run on any
rem  machine at any time, including one that is already working.
rem
rem  This exists because the hard question is never "what is wrong"
rem  but "why does it work on my machine and not on theirs". Run
rem  this on BOTH machines and compare the lines that differ.
rem
rem  Usage:
rem    doctor.bat          check everything
rem    doctor.bat --quiet  only print problems, exit 1 if any
rem
rem  Exit code:  0 all good   1 something needs attention
rem ============================================================
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "IISEXE="
set "PSQL=%ROOT%tools\pgsql\bin\psql.exe"
set "CREDS=%ROOT%tools\supabase-credentials.txt"
set "QUIET="
if /i "%~1"=="--quiet" set "QUIET=1"

set "PROBLEMS=0"
set "NOTES=0"

call :say "============================================================"
call :say "  STAR:DOM - machine check"
call :say "  Repo: %ROOT%"
call :say "============================================================"
echo.

rem ---------- 1. IIS Express ---------------------------------------
call :section "Web server"
call :find_iis
if defined IISEXE (
    call :ok   "IIS Express  : %IISEXE%"
) else (
    call :bad  "IIS Express  : NOT FOUND"
    call :note "               run setup.bat, or: winget install Microsoft.IISExpress"
)

rem ---------- 2. .NET Framework ------------------------------------
call :section "Runtime"
rem The Release value is a REG_DWORD, which reg query prints in hex
rem (0x82405 = 532997). It has to be converted before it can be compared,
rem so PowerShell does the reading and the arithmetic.
for /f "usebackq delims=" %%R in (`powershell -NoProfile -Command "$k='HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full'; $v=(Get-ItemProperty -Path $k -Name Release -EA SilentlyContinue).Release; if ($null -eq $v) { 'MISSING' } else { '' + $v }" 2^>nul`) do set "RELEASE=%%R"
if not defined RELEASE set "RELEASE=MISSING"
if "!RELEASE!"=="MISSING" (
    call :bad  ".NET 4.8     : could not read the registry key"
    call :note "               install the .NET Framework 4.8 runtime, then re-run"
) else (
    set /a RELNUM=!RELEASE!
    if !RELNUM! LSS 461808 (
        call :bad  ".NET 4.8     : release !RELEASE! is too old, need 461808 or newer"
    ) else (
        call :ok   ".NET 4.8     : release !RELEASE!"
    )
)

rem ---------- 3. the built site ------------------------------------
call :section "Build output"
if exist "%ROOT%STAR-DOM-Web\bin\STAR_DOM_Web.dll" (
    call :ok   "STAR_DOM_Web : bin\STAR_DOM_Web.dll is present"
) else (
    call :bad  "STAR_DOM_Web : bin\STAR_DOM_Web.dll is MISSING"
    call :note "               run build.bat"
)
rem bin holds 16 files, of which 10 are DLLs. The ones that matter are the
rem vendored dependencies: the site throws at startup if any is missing, and
rem MSBuild's Clean target deletes exactly these because nothing in the
rem project references them as build outputs.
set /a DLLN=0
for %%F in ("%ROOT%STAR-DOM-Web\bin\*.dll") do set /a DLLN+=1
if !DLLN! GEQ 10 (
    call :ok "Vendored DLLs: !DLLN! in bin (10 expected)"
) else (
    call :bad "Vendored DLLs: only !DLLN! in bin, 10 expected"
    call :note "               the vendored Npgsql dependencies were deleted."
    call :note "               Restore them:  git checkout -- STAR-DOM-Web/bin"
    call :note "               And build with build.bat, never Rebuild (Clean)."
)
if exist "%ROOT%tools\refasm\build\.NETFramework\v4.8" (
    call :ok   "refasm       : targeting pack present"
) else (
    call :note "refasm       : absent - fine if this machine has the"
    call :note "               .NET Framework 4.8 Developer Pack"
)

rem ---------- 4. database ------------------------------------------
call :section "Database"
if exist "%PSQL%" (
    call :ok   "psql         : tools\pgsql is committed and present"
) else (
    call :bad  "psql         : tools\pgsql is MISSING from this clone"
    call :note "               re-clone; the PostgreSQL client is committed on purpose"
)

if exist "%CREDS%" (
    findstr /c:"PASTE_PASSWORD_HERE" "%CREDS%" >nul 2>nul
    if errorlevel 1 (
        call :ok   "Credentials  : Supabase file present and filled in"
        call :supabase_test
    ) else (
        call :bad  "Credentials  : Supabase file has the placeholder password"
        call :note "               re-run setup.bat and type the real password"
    )
) else (
    call :bad  "Credentials  : no Supabase file - the site has no database"
    call :note "               run setup.bat and type the password when asked"
)

rem ---------- 5. Supabase CA ---------------------------------------
call :section "Trust"
certutil -user -store Root 2>nul | findstr /i /c:"Supabase Root 2021 CA" >nul
if not errorlevel 1 (
    call :ok   "Supabase CA  : trusted in your USER store"
) else (
    call :note "Supabase CA  : not trusted - only needed for Supabase mode"
    call :note "               certutil -user -addstore -f Root tools\supabase\prod-ca-2021.crt"
)

rem ---------- 6. ports ---------------------------------------------
call :section "Ports"
call :port 8095 "website"

rem ---------- summary ----------------------------------------------
echo.
call :say "============================================================"
if "!PROBLEMS!"=="0" (
    call :say "  RESULT: ready.  start it with run-website.bat"
) else (
    call :say "  RESULT: !PROBLEMS! problem(s) above - fix those first."
)
call :say "============================================================"
echo.

if "!PROBLEMS!"=="0" exit /b 0
exit /b 1

rem ---------- helpers ---------------------------------------------

:supabase_test
rem Prove the cloud database actually answers. This is the single most
rem common "works on mine" failure: right file, wrong password, or the
rem Transaction pooler instead of the Session pooler.
if not exist "%PSQL%" exit /b 0
call "%ROOT%tools\supabase-env.bat" >nul 2>&1
if errorlevel 1 (
    call :bad "Supabase test: could not read the credentials file"
    exit /b 0
)
"%PSQL%" -tA -c "SELECT 1" "%SUPABASE_DSN%" >nul 2>&1
if errorlevel 1 (
    call :bad  "Supabase test: COULD NOT CONNECT"
    call :note "               - password wrong, or still the placeholder"
    call :note "               - Transaction pooler? it must be Session, port 5432"
    call :note "               - CA not trusted (see the Trust section)"
) else (
    call :ok "Supabase test: connected, Supabase answered"
)
exit /b 0

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
exit /b 0

:port
call :port_free %~1
if errorlevel 1 (
    call :warn "%~2 (%~1)  : IN USE"
) else (
    call :ok   "%~2 (%~1)  : free"
)
exit /b 0

rem errorlevel 1 when the port is NOT free
:port_free
netstat -ano 2>nul | findstr /r /c:":%~1 .*LISTENING" >nul 2>nul && exit /b 1
exit /b 0

:section
echo.
call :say "  -- %~1"
exit /b 0

:say
if defined QUIET exit /b 0
echo %~1
exit /b 0

:ok
set /a PROBLEMS+=0
if defined QUIET exit /b 0
echo   [ ok ] %~1
exit /b 0

:warn
set /a NOTES+=1
if defined QUIET exit /b 0
echo   [busy] %~1
exit /b 0

:bad
set /a PROBLEMS+=1
if defined QUIET (
    echo   PROBLEM: %~1
    exit /b 0
)
echo   [FAIL] %~1
exit /b 0

:note
if defined QUIET exit /b 0
echo          %~1
exit /b 0
