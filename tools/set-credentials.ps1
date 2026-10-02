<#
    STAR:DOM - write tools\supabase-credentials.txt with a masked password prompt.

    Called by setup.bat. Batch cannot mask what you type, PowerShell can, so
    this exists instead of dropping the user into Notepad: the password is
    never echoed, never lands in the command history, and never sits on
    screen after the fact.

    The connection string is copied from the example template, which already
    has the project ref, region and pooler host filled in. Only the password
    is asked for, and only PASTE_PASSWORD_HERE is replaced.
#>
[CmdletBinding()]
param(
    [string] $Template,
    [string] $Target,
    # Read the password from stdin instead of the masked console prompt. Used by
    # tests and by unattended installs; a password on a command line would end
    # up in shell history and in the process list, stdin does neither.
    [switch] $FromStdin
)

$ErrorActionPreference = 'Stop'
$PLACEHOLDER = 'PASTE_PASSWORD_HERE'

# $PSScriptRoot is empty while param defaults are evaluated, so resolve here.
# This script lives in tools\, which is also where both the template and the
# real credentials file sit.
$root = $PSScriptRoot
if (-not $Template) { $Template = Join-Path $root 'supabase-credentials.example.txt' }
if (-not $Target)   { $Target   = Join-Path $root 'supabase-credentials.txt' }

if (-not (Test-Path $Template)) {
    Write-Host "  ERROR: template not found: $Template" -ForegroundColor Red
    exit 1
}

$templateText = Get-Content $Template -Raw -Encoding UTF8
if ($templateText -notmatch [regex]::Escape($PLACEHOLDER)) {
    Write-Host "  ERROR: template has no $PLACEHOLDER to replace." -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host '  Paste the password from Supabase -> Project Settings -> Database'
Write-Host '  -> Connection string -> the SESSION POOLER tab.'
Write-Host '  Nothing you type here is shown on screen.'
Write-Host ''

if ($FromStdin) {
    $plain = [Console]::In.ReadLine()
} else {
    $secure = Read-Host '  Database password' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

if ([string]::IsNullOrWhiteSpace($plain)) {
    Write-Host ''
    Write-Host '  ERROR: empty password, nothing was written.' -ForegroundColor Red
    exit 1
}

# Percent-encode so a password containing @ : / ? # or a space cannot break the
# URI apart. Both ends decode it: libpq for psql, Db.vb Unescape for Npgsql.
$encoded = [System.Uri]::EscapeDataString($plain)

$out = $templateText.Replace($PLACEHOLDER, $encoded)
[IO.File]::WriteAllText($Target, $out, (New-Object Text.UTF8Encoding $false))

# Do not keep the plaintext around longer than the write.
$plain = $null
$secure = $null

Write-Host ''
Write-Host "  Wrote $Target" -ForegroundColor Green

exit 0