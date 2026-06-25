# QTO Plugin Installer
# Run as Administrator for all-users install, or without for current-user.
#
# Usage: .\Install-QTO.ps1 [-AllUsers]

param([switch]$AllUsers)

$ErrorActionPreference = "Stop"
$pluginName = "QTO.bundle"

# Determine AutoCAD bundle path
if ($AllUsers) {
    $bundlePath = "$env:ProgramData\Autodesk\ApplicationPlugins\$pluginName"
} else {
    $bundlePath = "$env:APPDATA\Autodesk\ApplicationPlugins\$pluginName"
}

Write-Host "Installing QTO Plugin to: $bundlePath" -ForegroundColor Cyan

# Remove previous version
if (Test-Path $bundlePath) {
    Write-Host "Removing previous installation…"
    Remove-Item $bundlePath -Recurse -Force
}

# Copy bundle
$sourceBundle = Join-Path $PSScriptRoot "QTO.bundle"
if (-not (Test-Path $sourceBundle)) {
    Write-Error "Bundle not found at $sourceBundle. Please build the project first."
    exit 1
}

Copy-Item $sourceBundle $bundlePath -Recurse -Force
Write-Host "Bundle copied." -ForegroundColor Green

# Verify
if (Test-Path "$bundlePath\PackageContents.xml") {
    Write-Host "Installation complete!" -ForegroundColor Green
    Write-Host "Launch AutoCAD and type 'QTO' to open the panel." -ForegroundColor Yellow
} else {
    Write-Error "Installation verification failed."
}
