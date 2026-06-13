# Builds a clean zip of the project for sharing (excludes node_modules, build output, caches).
# Usage: .\make-zip.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$name = "tictactoe"
$staging = Join-Path $env:TEMP "$name-zip-staging"
$zipPath = Join-Path $root "$name.zip"

$excludeDirs = @("node_modules", "bin", "obj", "dist", ".angular", ".vs", ".idea")
$excludeFiles = @("*.docx")

Write-Host "Staging clean copy at $staging ..." -ForegroundColor Yellow
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
# robocopy: /MIR mirrors, /XD excludes dirs, /XF excludes files, /NFL/NDL/NJH/NJS/NP silences output
robocopy $root $staging /MIR /XD $excludeDirs /XF $excludeFiles /NFL /NDL /NJH /NJS /NP | Out-Null

# robocopy exit codes 0-7 are success; 8+ are errors
if ($LASTEXITCODE -ge 8) {
    Write-Host "robocopy failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit 1
}

# Remove the zip itself from staging if a previous one got copied
Remove-Item (Join-Path $staging "$name.zip") -ErrorAction SilentlyContinue

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "Compressing to $zipPath ..." -ForegroundColor Yellow
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath -CompressionLevel Optimal

Remove-Item $staging -Recurse -Force

$sizeKB = [math]::Round((Get-Item $zipPath).Length / 1KB, 1)
Write-Host "`nDone." -ForegroundColor Green
Write-Host "  $zipPath  ($sizeKB KB)"
Write-Host "  Recipient extracts it and runs .\start.ps1 (will do npm install on first run)."
