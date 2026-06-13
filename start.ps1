# Runs setup (if needed) and starts both backend and frontend.
# Usage: .\start.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "Tic Tac Toe - setup & run" -ForegroundColor Cyan

# --- prerequisite checks ---
function Require-Cmd($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Write-Host "ERROR: '$name' is not installed or not on PATH." -ForegroundColor Red
        exit 1
    }
}
Require-Cmd dotnet
Require-Cmd node
Require-Cmd npm

# --- backend restore ---
Write-Host "`nRestoring backend..." -ForegroundColor Yellow
Push-Location "$root\backend"
dotnet restore | Out-Host
Pop-Location

# --- frontend install ---
if (-not (Test-Path "$root\frontend\node_modules")) {
    Write-Host "`nInstalling frontend dependencies (first run, this takes a minute)..." -ForegroundColor Yellow
    Push-Location "$root\frontend"
    npm install | Out-Host
    Pop-Location
} else {
    Write-Host "`nFrontend dependencies already installed." -ForegroundColor Green
}

# --- start servers in their own PowerShell windows so you can see the logs ---
# Note: launching via "powershell -NoExit -Command ..." instead of "cmd /k ..."
# because the Angular CLI dev server (ng serve) fails to bind to its port when
# launched inside an interactive cmd /k shell on Windows.
Write-Host "`nStarting backend on http://localhost:5050 ..." -ForegroundColor Yellow
$backend = Start-Process -FilePath "powershell.exe" `
    -ArgumentList "-NoExit","-Command","dotnet run" `
    -WorkingDirectory "$root\backend" -PassThru

Start-Sleep -Seconds 3

Write-Host "Starting frontend on http://localhost:4200 ..." -ForegroundColor Yellow
$frontend = Start-Process -FilePath "powershell.exe" `
    -ArgumentList "-NoExit","-Command","npm start" `
    -WorkingDirectory "$root\frontend" -PassThru

Write-Host "`nBoth servers are starting in their own windows." -ForegroundColor Green
Write-Host "  Backend  PID: $($backend.Id)   http://localhost:5050"
Write-Host "  Frontend PID: $($frontend.Id)   http://localhost:4200 (opens automatically)"
Write-Host ""
Write-Host "The Angular dev server takes ~10-20 seconds to compile on first start."
Write-Host "If the browser opens before it's ready, just refresh."
Write-Host ""
Write-Host "To stop the servers, close their windows or run:"
Write-Host "  Stop-Process -Id $($backend.Id),$($frontend.Id)"
