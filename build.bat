@echo off
rem ============================================================
rem  STAR:DOM - build the website from wherever the repo lives.
rem
rem  Every path is derived from %~dp0, this script's own folder.
rem  The repo can be cloned to any drive, any path, any folder
rem  name and this still works. Nothing absolute is baked in.
rem
rem  Usage:
rem    build.bat            Debug build, the normal case
rem    build.bat Release    Release build
rem
rem  Do NOT change -t:Build to -t:Rebuild. Rebuild runs the
rem  Clean target first, and Clean deletes the four vendored
rem  Npgsql dependency DLLs out of bin because nothing in the
rem  project references them as build outputs. -t:Build only
rem  overwrites what this project owns, so bin keeps all 16.
rem ============================================================
setlocal

set "ROOT=%~dp0"
set "SLN=%ROOT%STAR-DOM-Web.sln"
set "REFDIR=%ROOT%tools\refasm\build\.NETFramework\v4.8"
set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"

if not exist "%SLN%" (
    echo.
    echo   ERROR: could not find %SLN%
    echo   Run this script from inside the repo.
    echo.
    exit /b 1
)

rem ---- locate MSBuild ------------------------------------------------
rem 1. vswhere is the supported way to ask where MSBuild is, and it
rem    keeps working when Visual Studio moves to a new year folder.
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "MSBUILD="

if exist "%VSWHERE%" call :find_vswhere

rem 2. The Build Tools install used to develop this project.
if not defined MSBUILD call :find_buildtools

rem 3. Anything already on PATH, e.g. a Developer Command Prompt.
if not defined MSBUILD call :find_onpath

if not defined MSBUILD (
    echo.
    echo   ERROR: MSBuild was not found.
    echo   Install Visual Studio 2022 Build Tools with the
    echo   .NET desktop build tools component, or run this from
    echo   a Visual Studio Developer Command Prompt.
    echo.
    exit /b 1
)

echo.
echo   Repo:     %ROOT%
echo   MSBuild:  %MSBUILD%
echo   Config:   %CONFIG%
echo.

rem ---- reference assemblies -------------------------------------------
rem tools\refasm ships the .NET Framework 4.8 targeting pack, so the
rem build works on a machine that has the 4.8 runtime but not the
rem developer pack. Skipped when the folder is absent, in which case
rem this Visual Studio supplies its own reference assemblies.
set "REFARG="
if exist "%REFDIR%" (
    set "REFARG=-p:FrameworkPathOverride=%REFDIR%"
) else (
    echo   Note: tools\refasm not found - using this VS's own reference assemblies.
    echo.
)

"%MSBUILD%" "%SLN%" -t:Build -p:Configuration=%CONFIG% %REFARG% -v:minimal -nologo
set "RC=%ERRORLEVEL%"

echo.
if "%RC%"=="0" goto :ok
echo   Build FAILED, exit code %RC%. See the errors above.
echo.
exit /b %RC%

:ok
echo   Build OK. Output: %ROOT%STAR-DOM-Web\bin\STAR_DOM_Web.dll
echo   Start the site with run-website.bat, or just refresh the
echo   browser. IIS Express picks up the new bin automatically.
echo.
exit /b 0

rem ---- helpers --------------------------------------------------------

:find_vswhere
for /f "usebackq delims=" %%M in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2^>nul`) do if not defined MSBUILD set "MSBUILD=%%M"
exit /b 0

:find_buildtools
for %%Y in (2022 2019) do if not defined MSBUILD if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\%%Y\BuildTools\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=%ProgramFiles(x86)%\Microsoft Visual Studio\%%Y\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
exit /b 0

:find_onpath
for /f "usebackq delims=" %%M in (`where msbuild 2^>nul`) do if not defined MSBUILD set "MSBUILD=%%M"
exit /b 0
