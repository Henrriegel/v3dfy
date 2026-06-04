[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourceBundleRoot,
    [string]$TargetRoot,
    [switch]$CleanTarget,
    [switch]$SkipValidation,
    [switch]$StrictModels,
    [switch]$RequireCapabilities
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$script:failureCount = 0

function Write-Check {
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
    Write-Check FAIL $Message
}

function Resolve-FullPath {
    param(
        [string]$Path,
        [string]$DefaultPath
    )

    $candidate = if ([string]::IsNullOrWhiteSpace($Path)) {
        $DefaultPath
    }
    else {
        $Path
    }

    if ([System.IO.Path]::IsPathRooted($candidate)) {
        return [System.IO.Path]::GetFullPath($candidate)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $candidate))
}

function Test-SameOrNestedPath {
    param(
        [string]$FirstPath,
        [string]$SecondPath
    )

    $first = [System.IO.Path]::GetFullPath($FirstPath).TrimEnd('\', '/')
    $second = [System.IO.Path]::GetFullPath($SecondPath).TrimEnd('\', '/')

    if ($first.Equals($second, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $first.StartsWith(
            "$second\",
            [System.StringComparison]::OrdinalIgnoreCase) -or
        $second.StartsWith(
            "$first\",
            [System.StringComparison]::OrdinalIgnoreCase)
}

function Invoke-BundleValidation {
    param(
        [string]$BundleRoot,
        [string]$Description
    )

    $validatorPath = Join-Path $PSScriptRoot 'validate-iw3-bundle.ps1'
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        Write-Failure "validation script is missing: $validatorPath"
        return $false
    }

    $validationArguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $validatorPath,
        '-BundleRoot',
        $BundleRoot
    )

    if ($StrictModels.IsPresent) {
        $validationArguments += '-StrictModels'
    }

    if ($RequireCapabilities.IsPresent) {
        $validationArguments += '-RequireCapabilities'
    }

    Write-Host "Validating $Description iw3 bundle: $BundleRoot"
    $validationOutput = & powershell @validationArguments 2>&1
    foreach ($line in $validationOutput) {
        Write-Host $line
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Failure "$Description bundle validation failed."
        return $false
    }

    Write-Check OK "$Description bundle validation passed."
    return $true
}

function Copy-Bundle {
    param(
        [string]$SourceRoot,
        [string]$DestinationBundleRoot
    )

    if ($CleanTarget.IsPresent -and
        (Test-Path -LiteralPath $DestinationBundleRoot -PathType Container)) {
        if ($PSCmdlet.ShouldProcess(
                $DestinationBundleRoot,
                'remove existing staged iw3 bundle')) {
            Remove-Item -LiteralPath $DestinationBundleRoot -Recurse -Force
        }
    }

    if ((Test-Path -LiteralPath $DestinationBundleRoot) -and
        -not $CleanTarget.IsPresent) {
        Write-Failure "target iw3 bundle already exists. Use -CleanTarget to replace it: $DestinationBundleRoot"
        return
    }

    $didCopy = $false
    if ($PSCmdlet.ShouldProcess($DestinationBundleRoot, 'stage iw3 bundle')) {
        New-Item -ItemType Directory -Path $DestinationBundleRoot -Force | Out-Null

        $sourceItems = @(Get-ChildItem -LiteralPath $SourceRoot -Force)
        foreach ($sourceItem in $sourceItems) {
            Copy-Item `
                -LiteralPath $sourceItem.FullName `
                -Destination $DestinationBundleRoot `
                -Recurse `
                -Force
        }

        $didCopy = $true
    }

    if ($didCopy) {
        if (Test-Path -LiteralPath $DestinationBundleRoot -PathType Container) {
            Write-Check OK "iw3 bundle staged under: $DestinationBundleRoot"
        }
        else {
            Write-Failure "target iw3 bundle was not created: $DestinationBundleRoot"
        }
    }
    else {
        Write-Check OK "iw3 bundle staging target checked: $DestinationBundleRoot"
    }
}

$sourceDefault = Join-Path $repoRoot 'engine\iw3'
$targetDefault = Join-Path $repoRoot 'artifacts\stage\v3dfy-app'
$sourcePath = Resolve-FullPath $SourceBundleRoot $sourceDefault
$targetPath = Resolve-FullPath $TargetRoot $targetDefault
$targetBundlePath = Join-Path $targetPath 'engine\iw3'

Write-Host "Source iw3 bundle: $sourcePath"
Write-Host "Target app root: $targetPath"
Write-Host "Target iw3 bundle: $targetBundlePath"
Write-Host 'This script copies files only. It does not run Python, iw3, FFmpeg, model loading, downloads, or installs.'

if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
    Write-Failure "source iw3 bundle root is missing: $sourcePath"
}

if (Test-SameOrNestedPath $sourcePath $targetBundlePath) {
    Write-Failure 'source and target bundle paths must be separate to avoid copying into or over the source bundle.'
}

if ($script:failureCount -gt 0) {
    Write-Check FAIL "iw3 bundle staging failed with $script:failureCount required issue(s)."
    exit 1
}

if ($SkipValidation.IsPresent) {
    Write-Check WARN 'source validation was skipped by request.'
}
elseif (-not (Invoke-BundleValidation -BundleRoot $sourcePath -Description 'source')) {
    Write-Check FAIL "iw3 bundle staging failed with $script:failureCount required issue(s)."
    exit 1
}

Copy-Bundle `
    -SourceRoot $sourcePath `
    -DestinationBundleRoot $targetBundlePath

if ($script:failureCount -gt 0) {
    Write-Check FAIL "iw3 bundle staging failed with $script:failureCount required issue(s)."
    exit 1
}

if (-not (Invoke-BundleValidation -BundleRoot $targetBundlePath -Description 'staged')) {
    Write-Check FAIL "iw3 bundle staging failed with $script:failureCount required issue(s)."
    exit 1
}

Write-Check OK 'iw3 bundle staging completed.'
