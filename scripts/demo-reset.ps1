# Demo reset: throws the local database away and starts a fresh, empty one (docker compose down -v, then up -d), and waits until
# Postgres is healthy. The API migrates and seeds the empty database by itself when it starts (demo-start.ps1).
# Run from anywhere:  powershell -File scripts/demo-reset.ps1
# WARNING: `down -v` deletes the volume, so everything in the local smartrail database is gone.
param(
    # How long to wait for Postgres to report healthy.
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)   # the repository root, where docker-compose.yml is

Write-Host 'Stopping the database and deleting its data (docker compose down -v) ...'
docker compose down -v
if ($LASTEXITCODE -ne 0) { throw 'docker compose down -v failed.' }

Write-Host 'Starting a fresh database (docker compose up -d) ...'
docker compose up -d
if ($LASTEXITCODE -ne 0) { throw 'docker compose up -d failed (is POSTGRES_PASSWORD set in .env? See .env.example).' }

$container = (docker compose ps -q postgres).Trim()
if (-not $container) { throw 'The postgres container was not found.' }

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ($true) {
    $health = (docker inspect --format '{{.State.Health.Status}}' $container).Trim()
    if ($health -eq 'healthy') { break }
    if ((Get-Date) -gt $deadline) { throw "Postgres is still '$health' after $TimeoutSeconds s." }
    Start-Sleep -Seconds 2
}

Write-Host 'Postgres is healthy. Next: scripts/demo-start.ps1 -ClockStart "2026-10-20T06:25:00+05:30"'
