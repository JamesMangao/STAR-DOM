@echo off
rem ============================================================
rem  STAR:DOM - run the unit tests.
rem
rem  Usage:
rem    run-tests.bat              all tests
rem    run-tests.bat <substring>  only tests whose name matches
rem
rem  Two things this handles that are easy to get wrong by hand:
rem
rem    1. SUPABASE_DB_URL. Roughly three quarters of the suite opens a
rem       real connection. Without the variable set, Db throws
rem       "No database connection is configured." and every one of those
rem       tests fails while looking like a product bug.
rem
rem    2. The MSTest adapter path. The adapter ships inside the
rem       website's packages\ folder and is not next to the test DLL,
rem       so vstest cannot discover the tests unless it is pointed at
rem       the net462 build explicitly. Without it vstest reports
rem       "No test is available" and zero tests run.
rem ============================================================
setlocal

set "ROOT=%~dp0"
set "TESTS=%ROOT%STAR-DOM-Tests\bin\Debug\STAR_DOM_Tests.dll"
set "ADAPTER=%ROOT%STAR-DOM-Web\packages\mstest.testadapter.4.5.1\buildTransitive\net462"
set "CRED=%ROOT%tools\supabase-credentials.txt"

if not exist "%TESTS%" (
    echo.
    echo   ERROR: %TESTS% not found.
    echo   Build the tests first, or just build the solution.
    echo.
    exit /b 1
)

rem ---- database connection -------------------------------------------
rem Read straight out of the git-ignored credentials file rather than
rem expecting the caller to export it. The value is never echoed.
if defined SUPABASE_DB_URL goto :have_url
if exist "%CRED%" (
    for /f "usebackq tokens=1,* delims==" %%A in ("%CRED%") do (
        if /I "%%A"=="SUPABASE_DB_URL" set "SUPABASE_DB_URL=%%B"
    )
)
:have_url
if not defined SUPABASE_DB_URL (
    echo.
    echo   WARNING: SUPABASE_DB_URL is not set.
    echo   Tests that open a database connection will fail rather than skip.
    echo.
)

rem ---- locate vstest -------------------------------------------------
set "VSTEST="
for /f "usebackq delims=" %%P in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.MSTest.TestAdapter -find Common7\IDE\Extensions\TestPlatform\vstest.console.exe 2^>nul`) do if not defined VSTEST set "VSTEST=%%P"
if not defined VSTEST for /f "usebackq delims=" %%P in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -find Common7\IDE\Extensions\TestPlatform\vstest.console.exe 2^>nul`) do if not defined VSTEST set "VSTEST=%%P"

if not defined VSTEST (
    echo.
    echo   ERROR: vstest.console.exe was not found.
    echo.
    exit /b 1
)

echo.
echo   Tests:  %TESTS%
echo.

if "%~1"=="" (
    "%VSTEST%" "%TESTS%" /TestAdapterPath:"%ADAPTER%"
) else (
    "%VSTEST%" "%TESTS%" /TestAdapterPath:"%ADAPTER%" /Tests:%~1
)

set "RC=%ERRORLEVEL%"
echo.
exit /b %RC%