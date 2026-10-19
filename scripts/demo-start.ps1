# Demo start: one command instead of six windows. It opens separate PowerShell windows for
#   1. the API (Development, with the demo clock Clock__Start)
#   2. the simulator (a cab tablet driving one journey)
#   3. the ops web dev server   4. the station display dev server
# and prints the addresses to open. Run it from anywhere:
#   powershell -File scripts/demo-start.ps1 -ClockStart "2026-10-20T06:25:00+05:30"
# Needs a running database (scripts/demo-reset.ps1 makes a fresh one) and the user-secrets of the code guide, section 7.
#
# Secrets: SMARTRAIL_CAB_SECRET (the tablet's secret) and SMARTRAIL_DEMO_PASSWORD are read from the environment, or asked for
# (hidden input). They are never written to a file or printed; the simulator window gets them through its environment only.
param(
    # The time the application believes it is when it starts: WITH an offset, on a weekday (1001 does not run on a Sunday).
    [string]$ClockStart = '2026-10-20T06:25:00+05:30',
    # The apps expect 5045. Another port is for rehearsals next to a running demo (the dev servers are pointed at it).
    [int]$ApiPort = 5045,
    # The journey the simulator drives, and the tablet / driver it uses (the code guide, section 4.15).
    [string]$Service = '1001',
    [string]$Device = 'CAB-01',
    [string]$Driver = 'DRV-01',
    [string]$From = 'FOT',
    [string]$To = 'KDT',
    # How long to wait for the API to answer (the first start on an empty database migrates and seeds).
    [int]$ApiTimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# ---- checks before anything is opened ----
if ($ClockStart -notmatch '(Z|[+-]\d\d:\d\d)$') {
    throw "ClockStart needs an offset, for example 2026-10-20T06:25:00+05:30 (got '$ClockStart')."
}
$parsed = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse($ClockStart, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$parsed)) {
    throw "ClockStart is not a date and time: '$ClockStart'."
}

$opsPort = 5173
$displayPort = 5174
foreach ($port in @($ApiPort, $opsPort, $displayPort)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
        throw "Port $port is already in use. Close the old demo windows first."
    }
}

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

# One new window. The command uses single quotes only, so it can sit inside the double quotes of -Command.
function Open-Window([string]$Title, [string]$WorkingDirectory, [string]$Command) {
    $inner = "`$Host.UI.RawUI.WindowTitle = '$Title'; $Command"
    Start-Process -FilePath 'powershell.exe' -WorkingDirectory $WorkingDirectory -ArgumentList "-NoExit -NoProfile -Command `"$inner`""
}

$apiUrl = "http://localhost:$ApiPort"

# ---- 1. the API with the demo clock ----
# --no-launch-profile: the launch profile would open a browser; Development is set here instead (the demo clock needs it).
Write-Host "Starting the API on $apiUrl with the clock at $ClockStart ..."
Open-Window 'SmartRail API' $root ("`$env:ASPNETCORE_ENVIRONMENT = 'Development'; `$env:Clock__Start = '$ClockStart'; " +
    "dotnet run --project backend/src/SmartRail.Api --no-launch-profile --urls $apiUrl")

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

# ---- 2. the simulator (the secrets reach only this window's environment) ----
Write-Host "Starting the simulator: service $Service with $Device / $Driver ..."
$env:SMARTRAIL_CAB_SECRET = $cabSecret
$env:SMARTRAIL_DEMO_PASSWORD = $demoPassword
try {
    Open-Window 'SmartRail simulator' $root ("dotnet run --project backend/src/SmartRail.Simulator -- --api $apiUrl --device $Device --driver $Driver " +
        "--service $Service --from $From --to $To")
}
finally {
    Remove-Item Env:SMARTRAIL_CAB_SECRET, Env:SMARTRAIL_DEMO_PASSWORD -ErrorAction SilentlyContinue
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
Write-Host 'Stop everything by closing the four windows.'
