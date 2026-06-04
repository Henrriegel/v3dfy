[CmdletBinding()]
param(
    [switch]$PreflightOnly,
    [switch]$RequireIw3Bundle,
    [switch]$StrictModels,
    [switch]$RequireCapabilities
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$installerScript = Join-Path $repoRoot 'packaging\inno\v3dfy.iss'
$publishRoot = Join-Path $repoRoot 'artifacts\publish\v3dfy-win-x64'
$iw3BundleRoot = Join-Path $publishRoot 'engine\iw3'
$iw3BundleValidator = Join-Path $PSScriptRoot 'validate-iw3-bundle.ps1'
$script:failureCount = 0

function Write-PackageMessage {
    param(
        [ValidateSet('OK', 'WARN', 'FAIL')]
        [string]$Level,
        [string]$Message
    )

    $color = switch ($Level) {
        'OK' { 'Green' }
        'WARN' { 'Yellow' }
        'FAIL' { 'Red' }
    }

    Write-Host "[$Level] $Message" -ForegroundColor $color
}

function Write-Failure {
    param([string]$Message)

    $script:failureCount++
    Write-PackageMessage FAIL $Message
}

function Test-RequiredDirectory {
    param(
        [string]$Path,
        [string]$Description
    )

    if (Test-Path -LiteralPath $Path -PathType Container) {
        Write-PackageMessage OK "$Description found: $Path"
        return $true
    }

    Write-Failure "$Description is missing: $Path"
    return $false
}

function Test-RequiredFile {
    param(
        [string]$Path,
        [string]$Description
    )

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Write-PackageMessage OK "$Description found: $Path"
        return $true
    }

    Write-Failure "$Description is missing: $Path"
    return $false
}

function Invoke-Iw3BundleValidation {
    if (-not (Test-Path -LiteralPath $iw3BundleValidator -PathType Leaf)) {
        Write-Failure "iw3 bundle validation script is missing: $iw3BundleValidator"
        return
    }

    $validationArguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $iw3BundleValidator,
        '-BundleRoot',
        $iw3BundleRoot
    )

    if ($StrictModels.IsPresent) {
        $validationArguments += '-StrictModels'
    }

    if ($RequireCapabilities.IsPresent) {
        $validationArguments += '-RequireCapabilities'
    }

    Write-Host "Validating published iw3 bundle before installer packaging: $iw3BundleRoot"
    $validationOutput = & powershell @validationArguments 2>&1
    foreach ($line in $validationOutput) {
        Write-Host $line
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Failure 'published iw3 bundle validation failed; Inno packaging will not run.'
        return
    }

    Write-PackageMessage OK 'published iw3 bundle validation passed.'
}

function Invoke-PackagePreflight {
    Write-Host "Publish root: $publishRoot"
    Write-Host 'Packaging preflight inspects files only. It does not run the app, Python, iw3, FFmpeg, model loading, downloads, or installs.'

    Test-RequiredDirectory $publishRoot 'publish output root' | Out-Null
    Test-RequiredFile (Join-Path $publishRoot 'V3dfy.App.exe') 'published app executable' | Out-Null
    Test-RequiredFile (Join-Path $publishRoot 'tools\ffmpeg\win-x64\ffmpeg.exe') 'bundled FFmpeg executable' | Out-Null
    Test-RequiredFile (Join-Path $publishRoot 'tools\ffmpeg\win-x64\ffprobe.exe') 'bundled FFprobe executable' | Out-Null

    if ($RequireIw3Bundle.IsPresent) {
        if (Test-RequiredDirectory $iw3BundleRoot 'published iw3 bundle root') {
            Invoke-Iw3BundleValidation
        }

        return
    }

    Write-PackageMessage WARN 'iw3 bundle validation not required for this package run. Use -RequireIw3Bundle for full offline installer preflight.'
}

Invoke-PackagePreflight

if ($script:failureCount -gt 0) {
    Write-PackageMessage FAIL "Installer packaging preflight failed with $script:failureCount required issue(s). Inno packaging skipped."
    exit 1
}

Write-PackageMessage OK 'Installer packaging preflight passed.'

if ($PreflightOnly.IsPresent) {
    Write-PackageMessage OK 'Preflight-only mode completed. Inno packaging skipped by request.'
    exit 0
}

$compilerCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ }

if (-not (Test-Path -LiteralPath $installerScript)) {
    Write-PackageMessage WARN "Inno Setup script is missing: $installerScript"
    return
}

$compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $compiler) {
    $compiler = $compilerCandidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if (-not $compiler) {
    Write-PackageMessage WARN 'Inno Setup Compiler was not found. Install Inno Setup before packaging.'
    return
}

$compilerPath = if ($compiler.Source) { $compiler.Source } else { $compiler }
& $compilerPath $installerScript
if ($LASTEXITCODE -ne 0) {
    Write-PackageMessage WARN "Inno Setup Compiler exited with code $LASTEXITCODE."
    return
}

Write-PackageMessage OK 'Inno Setup packaging completed.'
