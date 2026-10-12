<#
    doc-facts.ps1 - prints the numbers the README and the presentation
    cheat sheet quote, straight from the code and the live database.

    Those numbers went stale twice because they were hand-copied into prose
    and nobody noticed until a demo login turned out to be a customer
    instead of the store owner. Run this whenever something changes and you
    are about to quote a figure, then paste the fresh value.

    Every path is derived from this file's own location.
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot          # repo root
$web  = Join-Path $root 'STAR-DOM-Web'

function Rule($title) {
    Write-Host ''
    Write-Host "== $title " -NoNewline
    Write-Host ('=' * [Math]::Max(0, 52 - $title.Length))
}

# ---------------------------------------------------------------- code ----
Rule 'CODE'

$src = Get-ChildItem $web -Recurse -Include '*.vb','*.aspx','*.master' -File |
       Where-Object { $_.FullName -notmatch '\\(bin|obj|packages|_preview)\\' }

$vb  = $src | Where-Object { $_.Extension -eq '.vb' }
$loc = ($src | Get-Content | Measure-Object -Line).Lines

function Count($items) { ($items | Measure-Object).Count }
function InPath($items, $needle) { ($items | Where-Object { $_.FullName -match $needle } | Measure-Object).Count }

Write-Host ("pages (.aspx)      : {0}" -f (Count ($src | Where-Object { $_.Extension -eq '.aspx' })))
Write-Host ("master pages       : {0}" -f (Count ($src | Where-Object { $_.Extension -eq '.master' })))
Write-Host ("VB.NET files       : {0}" -f (Count $vb))
Write-Host ("  App\             : {0}" -f (InPath $vb '\\App\\'))
Write-Host ("  Shared\Models\   : {0}" -f (InPath $vb '\\Shared\\Models\\'))
Write-Host ("  Shared\Repos\    : {0}" -f (InPath $vb '\\Shared\\Repositories\\'))
Write-Host ("  Shared\Services\ : {0}" -f (InPath $vb '\\Shared\\Services\\'))
Write-Host ("  Code\            : {0}" -f (InPath $vb '\\Code\\'))
Write-Host ("total app LOC      : {0}" -f $loc)

Write-Host ''
Write-Host ("test project LOC   : {0}" -f ((Get-ChildItem (Join-Path $root 'STAR-DOM-Tests') -Recurse -Include '*.vb' -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Get-Content | Measure-Object -Line).Lines))

$binDlls = @(Get-ChildItem (Join-Path $web 'bin') -Filter '*.dll' -File -ErrorAction SilentlyContinue)
Write-Host ("bin\ files / DLLs   : {0} / {1}" -f $binDlls.Count, (Get-ChildItem (Join-Path $web 'bin') -File -ErrorAction SilentlyContinue).Count)

# ------------------------------------------------------------ database ----
Rule 'DATABASE'

$cred = Join-Path $root 'tools\supabase-credentials.txt'
$psql = Join-Path $root 'tools\pgsql\bin\psql.exe'

if (-not (Test-Path $cred)) {
    Write-Host 'SKIPPED - no tools\supabase-credentials.txt'
    Write-Host 'Copy tools\supabase-credentials.example.txt and fill it in.'
    exit 0
}
if (-not (Test-Path $psql)) {
    Write-Host 'SKIPPED - tools\pgsql\bin\psql.exe is missing'
    exit 0
}

$line = Get-Content $cred | Where-Object { $_ -match '^\s*SUPABASE_DB_URL=' } | Select-Object -First 1
if (-not $line) { Write-Host 'SKIPPED - no SUPABASE_DB_URL in the credentials file'; exit 0 }
$dbUrl = ($line -split '=', 2)[1].Trim().Trim('"')

$sql = @'
SELECT 'tables ' || count(*) FROM pg_tables WHERE schemaname='public'
UNION ALL SELECT 'products ' || count(*) FROM products
UNION ALL SELECT 'products_active ' || count(*) FROM products WHERE isactive
UNION ALL SELECT 'bundles_active ' || count(*) FROM bundles WHERE isactive
UNION ALL SELECT 'popupevents ' || count(*) FROM popupevents
UNION ALL SELECT 'admin_users ' || count(*) FROM users WHERE roleid = (SELECT id FROM roles WHERE name = 'ADMIN')
UNION ALL SELECT 'customer_users ' || count(*) FROM users WHERE roleid = (SELECT id FROM roles WHERE name = 'CUSTOMER')
UNION ALL SELECT 'channels ' || string_agg(channel, ' / ' ORDER BY channel) FROM paymentsettings
UNION ALL SELECT 'channels_enabled ' || count(*) FROM paymentsettings WHERE isenabled
UNION ALL SELECT 'roles ' || string_agg(name, ' / ' ORDER BY name) FROM roles
UNION ALL SELECT 'eventsales_rows ' || count(*) FROM eventsales
ORDER BY 1;
'@

$sqlFile = Join-Path $env:TEMP 'doc-facts.sql'
Set-Content -Path $sqlFile -Value $sql -Encoding UTF8

$rows = & $psql "--dbname=$dbUrl" '--tuples-only' '--no-align' '-f' $sqlFile 2>&1
Remove-Item $sqlFile -ErrorAction SilentlyContinue

if ($LASTEXITCODE -ne 0) {
    Write-Host 'Could not reach the database:'
    $rows | ForEach-Object { Write-Host "  $_" }
    Write-Host 'Check the password in tools\supabase-credentials.txt.'
    exit 1
}

# Split on the FIRST space only: values may contain spaces of their own.
$seen = @{}
foreach ($row in $rows) {
    $line = "$row".Trim()
    if (-not $line -or $line -like 'ERROR*') { continue }
    $parts = $line -split ' ', 2
    if ($parts.Count -ne 2) { continue }
    $seen[$parts[0]] = $parts[1]
    Write-Host ("{0,-17} {1}" -f $parts[0], $parts[1])
}

if ($seen.Count -eq 0) {
    Write-Host 'No rows came back. The credentials file may be pointing at an'
    Write-Host 'empty database, or the schema may not have been seeded yet.'
    exit 1
}

Write-Host ''
Write-Host 'Note: eventsales_rows is legacy. STAR:DOM is online-only and never'
Write-Host 'writes booth takings - the write path was removed. The table stays so'
Write-Host 'an existing database does not need a migration.'
Write-Host ''