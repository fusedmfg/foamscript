# FoamScript Deployment Script
# Automates build and deployment to Ubuntu workstation

param(
    [string]$UbuntuHost = "",                    # Override config value
    [string]$UbuntuUser = "",                    # Override config value
    [string]$TargetDir = "",                     # Override config value
    [switch]$BuildOnly,                          # Only build, don't deploy
    [switch]$SkipTests,                          # Skip running tests before deploy
    [switch]$RunValidation                       # Run validation on Ubuntu after deploy
)

$ErrorActionPreference = "Stop"

# Load configuration
$ConfigPath = Join-Path $PSScriptRoot "deploy.config.ps1"
if (Test-Path $ConfigPath) {
    $Config = & $ConfigPath
    if (-not $UbuntuHost) { $UbuntuHost = $Config.UbuntuHost }
    if (-not $UbuntuUser) { $UbuntuUser = $Config.UbuntuUser }
    if (-not $TargetDir) { $TargetDir = $Config.TargetDir }
    $OpenFOAMBashrc = $Config.OpenFOAMBashrc
} else {
    Write-Host "⚠ Warning: deploy.config.ps1 not found. Using defaults." -ForegroundColor Yellow
    if (-not $TargetDir) { $TargetDir = "~/foamscript" }
    $OpenFOAMBashrc = "/opt/openfoam2512/etc/bashrc"
}

# Validate configuration
if (-not $UbuntuHost -or $UbuntuHost -eq "your-ubuntu-host") {
    Write-Host "✗ Error: Please configure UbuntuHost in deploy.config.ps1" -ForegroundColor Red
    exit 1
}
if (-not $UbuntuUser -or $UbuntuUser -eq "your-username") {
    Write-Host "✗ Error: Please configure UbuntuUser in deploy.config.ps1" -ForegroundColor Red
    exit 1
}

Write-Host "=== FoamScript Deployment ===" -ForegroundColor Cyan
Write-Host ""

# Configuration
$ProjectRoot = $PSScriptRoot
$ExcludeDirs = @("bin", "obj", ".vs", "Logs", "foamscript.Tests")

# Step 1: Run tests (unless skipped)
if (-not $SkipTests) {
    Write-Host "[1/5] Running tests..." -ForegroundColor Yellow
    dotnet test --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Tests failed! Aborting deployment." -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ All tests passed" -ForegroundColor Green
} else {
    Write-Host "[1/5] Skipping tests..." -ForegroundColor Yellow
}

# Step 2: Clean local build artifacts
Write-Host "[2/5] Cleaning build artifacts..." -ForegroundColor Yellow
dotnet clean --verbosity quiet

# Step 3: Create deployment package
Write-Host "[3/5] Creating deployment package..." -ForegroundColor Yellow
$TempDir = Join-Path $env:TEMP "foamscript-deploy"
if (Test-Path $TempDir) {
    Remove-Item -Recurse -Force $TempDir
}
New-Item -ItemType Directory -Path $TempDir | Out-Null

# Copy source files (excluding build artifacts)
Get-ChildItem -Path $ProjectRoot -Recurse | Where-Object {
    $item = $_
    $relativePath = $_.FullName.Substring($ProjectRoot.Length + 1)

    # Exclude directories
    $shouldExclude = $false
    foreach ($exclude in $ExcludeDirs) {
        if ($relativePath -like "$exclude*") {
            $shouldExclude = $true
            break
        }
    }

    # Include only source files
    if (-not $shouldExclude -and -not $_.PSIsContainer) {
        $destPath = Join-Path $TempDir $relativePath
        $destDir = Split-Path -Parent $destPath
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Copy-Item $_.FullName -Destination $destPath -Force
    }
}

Write-Host "✓ Package created at: $TempDir" -ForegroundColor Green

if ($BuildOnly) {
    Write-Host "Build-only mode. Package ready at: $TempDir" -ForegroundColor Cyan
    exit 0
}

# Step 4: Deploy to Ubuntu via SCP
Write-Host "[4/5] Deploying to Ubuntu ($UbuntuHost)..." -ForegroundColor Yellow

# Check if scp is available
$scpPath = Get-Command scp -ErrorAction SilentlyContinue
if (-not $scpPath) {
    Write-Host "✗ SCP not found. Please install OpenSSH Client:" -ForegroundColor Red
    Write-Host "  Settings > Apps > Optional Features > OpenSSH Client" -ForegroundColor Yellow
    exit 1
}

# Create target directory on Ubuntu
$sshCmd = "mkdir -p $TargetDir"
ssh "$UbuntuUser@$UbuntuHost" $sshCmd

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to connect to Ubuntu host. Check credentials." -ForegroundColor Red
    exit 1
}

# Copy files to Ubuntu
scp -r "$TempDir/*" "${UbuntuUser}@${UbuntuHost}:${TargetDir}/"

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to copy files to Ubuntu." -ForegroundColor Red
    exit 1
}

Write-Host "✓ Files deployed to Ubuntu:${TargetDir}" -ForegroundColor Green

# Step 5: Build on Ubuntu
Write-Host "[5/5] Building on Ubuntu..." -ForegroundColor Yellow

# Build the project file directly (not solution, since tests aren't deployed)
ssh "$UbuntuUser@$UbuntuHost" "cd $TargetDir && dotnet build foamscript.csproj -c Release --verbosity quiet"

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed on Ubuntu." -ForegroundColor Red
    exit 1
}

Write-Host "✓ Build successful" -ForegroundColor Green

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Green
Write-Host ""

# Optional: Run validation on Ubuntu
if ($RunValidation) {
    Write-Host "[Bonus] Running validation on Ubuntu..." -ForegroundColor Yellow
    Write-Host ""

    # Use bash -c to properly handle sourcing and command execution
    ssh "$UbuntuUser@$UbuntuHost" "bash -c 'cd $TargetDir && source $OpenFOAMBashrc && dotnet run -c Release -- validate --verbose'"
    Write-Host ""
}

Write-Host "To run on Ubuntu:" -ForegroundColor Cyan
Write-Host "  ssh $UbuntuUser@$UbuntuHost" -ForegroundColor White
Write-Host "  cd $TargetDir" -ForegroundColor White
Write-Host "  source $OpenFOAMBashrc" -ForegroundColor White
Write-Host "  dotnet run -c Release -- validate --verbose" -ForegroundColor White
Write-Host ""

# Cleanup temp directory
Remove-Item -Recurse -Force $TempDir
