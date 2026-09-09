# =========================================================================
# Table Lamp - Inno Setup Build Automation Script
# Packages Table Lamp WinUI 3 desktop application into an installer .exe
# =========================================================================

param(
    [switch]$SkipPublish = $false,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$ProjectFile = Join-Path $ProjectDir "TableLamp.csproj"
$IssFile = Join-Path $ScriptDir "TableLampSetup.iss"

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host " Building Table Lamp Installer (Inno Setup)      " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. Publish self-contained executable bundle
if (-not $SkipPublish) {
    Write-Host "`n[1/2] Publishing application for win-x64 ($Configuration)..." -ForegroundColor Yellow
    & dotnet publish "$ProjectFile" -c $Configuration -r win-x64 --self-contained true
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    Write-Host "Publish completed successfully." -ForegroundColor Green
} else {
    Write-Host "`n[1/2] Skipping publish step as requested." -ForegroundColor Gray
}

# 2. Locate Inno Setup Compiler (ISCC.exe)
Write-Host "`n[2/2] Locating Inno Setup Compiler (ISCC.exe)..." -ForegroundColor Yellow
$CandidatePaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
)

$IsccPath = $null
foreach ($path in $CandidatePaths) {
    if (Test-Path $path) {
        $IsccPath = $path
        break
    }
}

if (-not $IsccPath) {
    $cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($cmd) {
        $IsccPath = $cmd.Source
    }
}

if ($IsccPath) {
    # Extract version dynamically from TableLamp.csproj
    [xml]$csprojXml = Get-Content "$ProjectFile"
    $AppVersion = $csprojXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $AppVersion) { $AppVersion = "0.0.7.5" }

    Write-Host "Found Inno Setup compiler at: $IsccPath" -ForegroundColor Green
    Write-Host "App Version: $AppVersion" -ForegroundColor Cyan
    Write-Host "Compiling installer script: $IssFile" -ForegroundColor Yellow
    & $IsccPath "/DMyAppVersion=$AppVersion" "$IssFile"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Inno Setup compiler failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    Write-Host "`n[SUCCESS] Installer built successfully!" -ForegroundColor Green
    $outputExe = Join-Path $ScriptDir "Output\TableLamp-Setup-v$AppVersion.exe"
    if (Test-Path $outputExe) {
        Write-Host "Installer package: $outputExe" -ForegroundColor Cyan
    }
} else {
    Write-Host "`n[NOTE] Inno Setup compiler (ISCC.exe) was not found on this machine." -ForegroundColor Yellow
    Write-Host "You can install Inno Setup using winget:" -ForegroundColor White
    Write-Host "    winget install JRSoftware.InnoSetup" -ForegroundColor Cyan
    Write-Host "`nOnce installed, you can re-run this script or compile directly in the Inno Setup GUI:" -ForegroundColor White
    Write-Host "    $IssFile" -ForegroundColor Cyan
}
