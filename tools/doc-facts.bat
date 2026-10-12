@echo off
rem ============================================================
rem  STAR:DOM - print the numbers the docs quote.
rem
rem  The README and the presentation cheat sheet both quote
rem  page counts, repository counts and demo logins. Those
rem  went stale before, because they were hand-copied into
rem  prose. Run this before a demo or before editing either
rem  document, and paste the fresh values.
rem
rem  Usage:
rem    doc-facts.bat
rem
rem  Needs tools\supabase-credentials.txt for the database
rem  half. The code half works without it.
rem ============================================================
setlocal

set "ROOT=%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%doc-facts.ps1"
exit /b %ERRORLEVEL%