# Runs the backend xUnit test suite.
# Usage: .\test.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Write-Host "Running backend tests..." -ForegroundColor Cyan
dotnet test (Join-Path $root "backend.tests") --nologo
