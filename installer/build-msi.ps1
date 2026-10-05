
<#
.SYNOPSIS
    Builds the OpenEDMShellExtension MSI installer.
    Downloads and installs WiX Toolset v3 automatically if not present.

.DESCRIPTION
    1. Checks for / installs WiX Toolset v3.11
    2. Builds the solution in Release mode
    3. Runs candle.exe + light.exe to produce OpenEDMShellExtension.msi
    4. Outputs the MSI to .\installer\output\

.NOTES
    Run from an Administrator PowerShell prompt in the project root.
    Requires internet access on first run (to download WiX).
#>

param(
    [string]$Version = "1.0.0.0"
)

$ErrorActionPreference = "Stop"

# Script lives in <root>\installer\ — go up one level to get the solution root
$projRoot     = Split-Path $PSScriptRoot -Parent
$binDir       = Join-Path $projRoot "src\OpenEDMShellExtension\bin\Release"
$installerDir = $PSScriptRoot
$outputDir    = Join-Path $installerDir "output"
$wxsFile      = Join-Path $installerDir "Product.wxs"
$msiOut       = Join-Path $outputDir "OpenEDMShellExtension.msi"
$msbuild      = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"

Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "  OpenEDM - MSI Builder" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

# ─── Step 1: Find or install WiX ────────────────────────────────────────────
Write-Host "[1/4] Locating WiX Toolset..." -ForegroundColor Cyan

$wixBin = $null
$wixCandidates = @(
    "C:\Program Files (x86)\WiX Toolset v3.11\bin",
    "C:\Program Files\WiX Toolset v3.11\bin",
    "C:\Program Files (x86)\WiX Toolset v3.14\bin"
)

foreach ($c in $wixCandidates) {
    if (Test-Path (Join-Path $c "candle.exe")) {
        $wixBin = $c
        break
    }
}

if (-not $wixBin) {
    Write-Host "   WiX not found. Installing via winget..." -ForegroundColor Yellow
    
    # Try winget first (Windows 10 21H2+ / Windows 11)
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        & winget install --id WixToolset.WixToolset --version 3.11.2 --accept-package-agreements --accept-source-agreements --silent
        Start-Sleep -Seconds 5
        foreach ($c in $wixCandidates) {
            if (Test-Path (Join-Path $c "candle.exe")) { $wixBin = $c; break }
        }
    }
    
    # Fallback: direct download
    if (-not $wixBin) {
        Write-Host "   Downloading WiX 3.11.2 installer..." -ForegroundColor Yellow
        $wixInstaller = Join-Path $env:TEMP "wix311.exe"
        $wixUrl = "https://github.com/wixtoolset/wix3/releases/download/wix3112rtm/wix311.exe"
        Invoke-WebRequest -Uri $wixUrl -OutFile $wixInstaller -UseBasicParsing
        Write-Host "   Running WiX installer (silent)..." -ForegroundColor Yellow
        Start-Process -FilePath $wixInstaller -ArgumentList "/quiet" -Wait
        Remove-Item $wixInstaller -Force -ErrorAction SilentlyContinue
        
        foreach ($c in $wixCandidates) {
            if (Test-Path (Join-Path $c "candle.exe")) { $wixBin = $c; break }
        }
    }
    
    if (-not $wixBin) {
        Write-Error "Could not install WiX automatically. Please install WiX Toolset v3.11 from https://wixtoolset.org/ and re-run this script."
        exit 1
    }
}

$candle = Join-Path $wixBin "candle.exe"
$light  = Join-Path $wixBin "light.exe"
Write-Host "   WiX found: $wixBin" -ForegroundColor Green

# ─── Step 2: Build the solution ─────────────────────────────────────────────
Write-Host "[2/4] Building solution in Release mode..." -ForegroundColor Cyan

if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild not found at: $msbuild"
    exit 1
}

& $msbuild (Join-Path $projRoot "src\OpenEDMShellExtension\OpenEDMShellExtension.csproj") /p:Configuration=Release /verbosity:minimal
& $msbuild (Join-Path $projRoot "src\OpenEDMHelper\OpenEDMHelper.csproj") /p:Configuration=Release /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Check MSBuild output above."
    exit 1
}

# ─── Step 2b: Publish OpenEDMAuditReader ─────────────────────────────────────────
Write-Host "[2b/4] Publishing OpenEDMAuditReader as Self-Contained..." -ForegroundColor Cyan
$auditReaderProj = Join-Path $projRoot "src\OpenEDMAuditReader\OpenEDMAuditReader.csproj"
$auditReaderPublishDir = Join-Path $projRoot "src\OpenEDMAuditReader\bin\Release\net10.0-windows\win-x64\publish"

& dotnet publish $auditReaderProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

# Verify required files exist
$required = @("OpenEDMShellExtension.dll", "OpenEDMHelper.exe", "SharpShell.dll", "openedm-config.txt")
foreach ($f in $required) {
    $path = Join-Path $binDir $f
    if (-not (Test-Path $path)) {
        Write-Error "Required file missing from build output: $path"
        exit 1
    }
}
if (-not (Test-Path (Join-Path $auditReaderPublishDir "OpenEDMAuditReader.exe"))) {
    Write-Error "OpenEDMAuditReader.exe not found in publish output."
    exit 1
}
Write-Host "   Build OK. All required files present." -ForegroundColor Green


# ─── Step 3: Compile WiX source ─────────────────────────────────────────────
Write-Host "[3/4] Compiling WiX installer..." -ForegroundColor Cyan

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$wixobjFile = Join-Path $outputDir "Product.wixobj"

& $candle $wxsFile `
    -dBuildOutputDir="$binDir" `
    -dAuditReaderDir="$auditReaderPublishDir" `
    -dVersion="$Version" `
    -ext WixUtilExtension `
    -out $wixobjFile `
    -arch x64

if ($LASTEXITCODE -ne 0) {
    Write-Error "candle.exe failed."
    exit 1
}

& $light $wixobjFile `
    -out $msiOut `
    -ext WixUIExtension `
    -ext WixUtilExtension `
    -spdb `
    -sice:ICE57

if ($LASTEXITCODE -ne 0) {
    Write-Error "light.exe failed."
    exit 1
}

# ─── Step 4: Done ───────────────────────────────────────────────────────────
Write-Host "[4/4] MSI ready!" -ForegroundColor Green
Write-Host ""
Write-Host "===============================================" -ForegroundColor Green
Write-Host "  Output: $msiOut" -ForegroundColor White
Write-Host "  Size:   $([Math]::Round((Get-Item $msiOut).Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Silent install command:" -ForegroundColor Cyan
Write-Host "  msiexec /i `"$msiOut`" /qn /l*v `"%TEMP%\openedm-install.log`"" -ForegroundColor White
Write-Host ""
Write-Host "Copy the MSI to your network share, then deploy via GPO." -ForegroundColor Yellow






