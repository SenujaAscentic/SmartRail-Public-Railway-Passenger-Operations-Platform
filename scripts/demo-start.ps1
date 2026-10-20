# Demo start: one command instead of six windows. It opens separate PowerShell windows for
#   1. the API (Development, with the demo clock Clock__Start)
#   1b. RailBites, the catering provider's own service (its own database and port), on the same demo clock
#   2. the simulator (a cab tablet driving one journey)
#   3. the ops web dev server   4. the station display dev server
# and prints the addresses to open. Run it from anywhere:
#   powershell -File scripts/demo-start.ps1 -ClockStart "2026-10-20T06:25:00+05:30"
# Needs a running database (scripts/demo-reset.ps1 makes a fresh one) and the user-secrets of the code guide, section 7.
#
# Secrets: SMARTRAIL_CAB_SECRET (the tablet's secret) and SMARTRAIL_DEMO_PASSWORD are read from the environment, or asked for
# (hidden input). They are never written to a file or printed; the simulator window gets them through its environment only.
# The catering secrets (the webhook signature secret and the provider API key) are made up fresh on every start and handed to the API
# and RailBites through their environment only, so both always agree and nothing has to be set up. The RailBites database connection comes from
# RAILBITES_CONNECTION, or is the API's own connection (from its user-secrets, not printed) with the database name railbites.
param(
    # The time the application believes it is when it starts: WITH an offset, on a weekday (1001 does not run on a Sunday).
    # Default 06:25 (1001 leaves at 06:30), or 05:55 with -WithSlowTrain (2001 leaves at 06:00).
    [string]$ClockStart = '',
    # The apps expect 5045. Another port is for rehearsals next to a running demo (the dev servers are pointed at it).
    [int]$ApiPort = 5045,
    # RailBites, the catering provider. Its event log page is http://localhost:<port>/events.
    [int]$RailBitesPort = 5100,
    # The journey the simulator drives, and the tablet / driver it uses (the code guide, section 4.15).
    [string]$Service = '1001',
    [string]$Device = 'CAB-01',
    [string]$Driver = 'DRV-01',
    [string]$From = 'FOT',
    [string]$To = 'KDT',
    # How long to wait for the API to answer (the first start on an empty database migrates and seeds).
    [int]$ApiTimeoutSeconds = 180,
    # Run npm ci in web/ even if node_modules exists (it is also run when node_modules is missing).
    [switch]$ReinstallWeb,
    # A second simulator for the slow train 2001 (Fort to Kandy, leaves 06:00). The seed gives 2001 the train set LH-01, so its driver
    # is DRV-03 and its cab is CAB-02 (shared/seed/service-assignments.json and cab-devices.json); DRV-02 is NOT assigned to it.
    [switch]$WithSlowTrain,
    [string]$SlowService = '2001',
    [string]$SlowDevice = 'CAB-02',
    [string]$SlowDriver = 'DRV-03'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# ---- checks before anything is opened ----
if (-not $PSBoundParameters.ContainsKey('ClockStart') -or -not $ClockStart) {
    $ClockStart = if ($WithSlowTrain) { '2026-10-20T05:55:00+05:30' } else { '2026-10-20T06:25:00+05:30' }
}
if ($ClockStart -notmatch '(Z|[+-]\d\d:\d\d)$') {
    throw "ClockStart needs an offset, for example 2026-10-20T06:25:00+05:30 (got '$ClockStart')."
}
$parsed = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse($ClockStart, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$parsed)) {
    throw "ClockStart is not a date and time: '$ClockStart'."
}

if ($WithSlowTrain -and $parsed.TimeOfDay -ge [TimeSpan]::FromHours(6)) {
    Write-Host "WARNING: the clock starts at $($parsed.ToString("HH:mm")): 2001 leaves at 06:00, so it would already be late." -ForegroundColor Yellow
}

$opsPort = 5173
$displayPort = 5174
foreach ($port in @($ApiPort, $RailBitesPort, $opsPort, $displayPort)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
        throw "Port $port is already in use. Close the old demo windows first."
    }
}

# ---- the web apps: install if needed, and build the tokens (they are generated, not committed) ----
$web = Join-Path $root 'web'
if ($ReinstallWeb -or -not (Test-Path (Join-Path $web 'node_modules'))) {
    # npm ci deletes node_modules; a dev server of this repository that is still running holds files in it. Never killed silently.
    $running = Get-CimInstance Win32_Process -Filter "Name = 'node.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($root) }
    if ($running) {
        Write-Host 'node is still running from this repository (an old dev server?):' -ForegroundColor Yellow
        $running | ForEach-Object { Write-Host ("  process {0}: {1}" -f $_.ProcessId, $_.CommandLine.Substring(0, [Math]::Min(120, $_.CommandLine.Length))) }
        throw 'Stop these node processes (close the old demo windows), then run this script again. They were not touched.'
    }
    Write-Host 'Installing the web packages (npm ci) ...'
    Push-Location $web
    try { npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' } } finally { Pop-Location }
}
Write-Host 'Building the design tokens ...'
Push-Location $web
try { npm run build -w '@smartrail/tokens'; if ($LASTEXITCODE -ne 0) { throw 'Building the tokens package failed.' } } finally { Pop-Location }

function Read-Secret([string]$Name, [string]$Prompt) {
    $existing = [Environment]::GetEnvironmentVariable($Name)
    if ($existing) { return $existing }
    $secure = Read-Host -Prompt $Prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

# Asked for first, so the rest runs without waiting for you. Both must equal the user-secrets (Seed:CabDevice:<id>:Token, Seed:DemoPassword).
$cabSecret = Read-Secret 'SMARTRAIL_CAB_SECRET' "Secret of $Device (the Seed:CabDevice:${Device}:Token value)"
$demoPassword = Read-Secret 'SMARTRAIL_DEMO_PASSWORD' 'Demo password (the Seed:DemoPassword value)'
# ---- optional slow train: its own cab secret (CAB-02 is a different tablet from CAB-01) ----
if ($WithSlowTrain) {
    $slowSecret = Read-Secret 'SMARTRAIL_CAB_SECRET_SLOW' "Secret of $SlowDevice (the Seed:CabDevice:${SlowDevice}:Token value)"
}

# ---- the catering secrets and the RailBites database (never printed) ----
$railBitesConnection = [Environment]::GetEnvironmentVariable('RAILBITES_CONNECTION')
if (-not $railBitesConnection) {
    $secrets = dotnet user-secrets list --project backend/src/SmartRail.Api 2>$null
    $line = $secrets | Where-Object { $_ -like 'ConnectionStrings:SmartRail =*' } | Select-Object -First 1
    if (-not $line) {
        throw 'No database connection for RailBites. Set RAILBITES_CONNECTION, or set ConnectionStrings:SmartRail in the API user-secrets (the code guide, section 7).'
    }
    $smartRailConnection = $line.Substring($line.IndexOf('=') + 1).Trim()
    if ($smartRailConnection -notmatch 'Database=[^;]*') { throw 'The API connection string has no Database= part to change.' }
    $railBitesConnection = $smartRailConnection -replace 'Database=[^;]*', 'Database=railbites'
}
function New-DemoSecret { -join ((1..32) | ForEach-Object { '{0:x}' -f (Get-Random -Maximum 16) }) }
$cateringSecret = New-DemoSecret
$cateringApiKey = New-DemoSecret
$railBitesUrl = "http://localhost:$RailBitesPort"

# One new window. The command uses single quotes only, so it can sit inside the double quotes of -Command.
function Open-Window([string]$Title, [string]$WorkingDirectory, [string]$Command) {
    $inner = "`$Host.UI.RawUI.WindowTitle = '$Title'; $Command"
    Start-Process -FilePath 'powershell.exe' -WorkingDirectory $WorkingDirectory -ArgumentList "-NoExit -NoProfile -Command `"$inner`""
}

$apiUrl = "http://localhost:$ApiPort"

# ---- 1. the API with the demo clock ----
# --no-launch-profile: the launch profile would open a browser; Development is set here instead (the demo clock needs it).
Write-Host "Starting the API on $apiUrl with the clock at $ClockStart ..."
$apiStarted = Get-Date
$env:Catering__ProviderUrl = $railBitesUrl
$env:Catering__SharedSecret = $cateringSecret
$env:Catering__ProviderApiKey = $cateringApiKey
try {
    Open-Window 'SmartRail API' $root ("`$env:ASPNETCORE_ENVIRONMENT = 'Development'; `$env:Clock__Start = '$ClockStart'; " +
        "dotnet run --project backend/src/SmartRail.Api --no-launch-profile --urls $apiUrl")
}
finally {
    Remove-Item Env:Catering__ProviderUrl, Env:Catering__SharedSecret, Env:Catering__ProviderApiKey -ErrorAction SilentlyContinue
}

# ---- wait for it ----
$deadline = (Get-Date).AddSeconds($ApiTimeoutSeconds)
$up = $false
while (-not $up) {
    try { $up = (Invoke-WebRequest -Uri "$apiUrl/api/v1/lines" -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 }
    catch { Start-Sleep -Seconds 2 }
    if (-not $up -and (Get-Date) -gt $deadline) {
        throw "The API did not answer $apiUrl/api/v1/lines within $ApiTimeoutSeconds s. Read the 'SmartRail API' window."
    }
}
Write-Host 'The API answers.'

# ---- 1b. RailBites on the same demo clock ----
# Its clock must agree with the API's within a few seconds (RailBites refuses a webhook whose timestamp is more than 5 minutes off).
# The API started a while ago (migrating and seeding takes time), so RailBites starts that much later on the demo clock.
$railBitesClock = $parsed.AddSeconds([Math]::Round(((Get-Date) - $apiStarted).TotalSeconds)).ToString('o')
Write-Host "Starting RailBites on $railBitesUrl ..."
$env:ConnectionStrings__RailBites = $railBitesConnection
$env:RailBites__SharedSecret = $cateringSecret
$env:RailBites__ApiKey = $cateringApiKey
try {
    Open-Window 'RailCantima (RailBites)' $root ("`$env:ASPNETCORE_ENVIRONMENT = 'Development'; `$env:Clock__Start = '$railBitesClock'; " +
        "dotnet run --project provider/RailBites --no-launch-profile --urls $railBitesUrl")
}
finally {
    Remove-Item Env:ConnectionStrings__RailBites, Env:RailBites__SharedSecret, Env:RailBites__ApiKey -ErrorAction SilentlyContinue
}
$deadline = (Get-Date).AddSeconds($ApiTimeoutSeconds)
$railBitesUp = $false
while (-not $railBitesUp) {
    try { $railBitesUp = (Invoke-WebRequest -Uri "$railBitesUrl/events" -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 }
    catch { Start-Sleep -Seconds 2 }
    if (-not $railBitesUp -and (Get-Date) -gt $deadline) {
        throw "RailBites did not answer $railBitesUrl/events within $ApiTimeoutSeconds s. Read the 'RailCantima (RailBites)' window."
    }
}
Write-Host 'RailCantima (RailBites) answers.'

# ---- the simulator is built ONCE here: two windows that both run `dotnet run` would build the same project at the same time and one fails ----
Write-Host 'Building the simulator ...'
dotnet build backend/src/SmartRail.Simulator --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Building the simulator failed.' }

# ---- 2. the simulator (the secrets reach only this window's environment) ----
Write-Host "Starting the simulator: service $Service with $Device / $Driver ..."
$env:SMARTRAIL_CAB_SECRET = $cabSecret
$env:SMARTRAIL_DEMO_PASSWORD = $demoPassword
try {
    Open-Window 'SmartRail simulator' $root ("dotnet run --no-build --project backend/src/SmartRail.Simulator -- --api $apiUrl --device $Device --driver $Driver " +
        "--service $Service --from $From --to $To")
}
finally {
    Remove-Item Env:SMARTRAIL_CAB_SECRET, Env:SMARTRAIL_DEMO_PASSWORD -ErrorAction SilentlyContinue
}

# ---- optional: the slow train 2001 (a second simulator, its own cab, driver and secret) ----
if ($WithSlowTrain) {
    Write-Host "Starting the second simulator: service $SlowService with $SlowDevice / $SlowDriver ..."
    $env:SMARTRAIL_CAB_SECRET = $slowSecret
    $env:SMARTRAIL_DEMO_PASSWORD = $demoPassword
    try {
        Open-Window 'SmartRail simulator 2001' $root ("dotnet run --no-build --project backend/src/SmartRail.Simulator -- --api $apiUrl --device $SlowDevice --driver $SlowDriver " +
            "--service $SlowService --from $From --to $To")
    }
    finally {
        Remove-Item Env:SMARTRAIL_CAB_SECRET, Env:SMARTRAIL_DEMO_PASSWORD -ErrorAction SilentlyContinue
    }
    Write-Host 'WARNING: 2001 leaves Colombo Fort at 06:00 server time. The simulator must start before 06:00 on the demo clock, so do not wait before starting it.' -ForegroundColor Yellow
}

# ---- 3 and 4. the two dev servers, pointed at this API ----
Write-Host 'Starting the ops web and the station display ...'
Open-Window 'SmartRail ops web' (Join-Path $root 'web') "`$env:BACKEND_URL = '$apiUrl'; npm run dev -w @smartrail/ops-web"
Open-Window 'SmartRail station display' (Join-Path $root 'web') "`$env:BACKEND_URL = '$apiUrl'; npm run dev -w @smartrail/station-display"

# ---- what to open ----
Write-Host ''
Write-Host 'Open (the dev servers need a few seconds):'
Write-Host "  Ops web              http://localhost:$opsPort"
Write-Host "  Departures board     http://localhost:$displayPort/board/FOT"
Write-Host "  Platform display     http://localhost:$displayPort/platform/GPH/1"
Write-Host "  RailCantima events   $railBitesUrl/events   (the catering provider: ETA and cancellation webhooks arrive here)"
Write-Host ''
Write-Host 'Passenger app (new terminal; search the date of the demo clock, not today):'
if ($ApiPort -eq 5045) {
    Write-Host '  cd mobile/apps/passenger; flutter run'
}
else {
    Write-Host "  cd mobile/apps/passenger; flutter run --dart-define=API_BASE_URL=http://10.0.2.2:$ApiPort"
}
Write-Host ''
Write-Host "The demo clock started at $ClockStart and keeps running; the simulator needs the journey to be about to leave."
Write-Host 'Stop everything by closing the windows that were opened (five, or six with -WithSlowTrain).'
