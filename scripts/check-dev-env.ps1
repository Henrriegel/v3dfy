[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runtimeRoot = $repoRoot

$runtimeDependencyPaths = [ordered]@{
    FfmpegExecutable = 'tools\ffmpeg\win-x64\ffmpeg.exe'
    FfprobeExecutable = 'tools\ffmpeg\win-x64\ffprobe.exe'
    Iw3EngineRoot = 'engine\iw3'
    PythonExecutable = 'engine\iw3\python\python.exe'
    PythonPathFile = 'engine\iw3\python\python312._pth'
    NunifRootDirectory = 'engine\iw3\nunif'
    Iw3PackageDirectory = 'engine\iw3\nunif\iw3'
    ModelsDirectory = 'engine\iw3\nunif\iw3\pretrained_models'
    Iw3RowFlowRuntimeDependency = 'engine\iw3\nunif\iw3\pretrained_models\hub\checkpoints\iw3_row_flow_v3_20250627.pth'
    ModelCatalog = 'engine\iw3\nunif\iw3\pretrained_models\MODEL_CATALOG.json'
    Iw3CliCapabilities = 'engine\iw3\IW3_CLI_CAPABILITIES.json'
    EngineManifest = 'engine\iw3\ENGINE_MANIFEST.json'
}

function Write-Check {
    param(
        [ValidateSet('OK', 'WARN', 'ERROR')]
        [string]$Level,
        [string]$Message
    )

    $color = switch ($Level) {
        'OK' { 'Green' }
        'WARN' { 'Yellow' }
        'ERROR' { 'Red' }
    }

    Write-Host "[$Level] $Message" -ForegroundColor $color
}

function Get-RuntimePath {
    param([string]$RelativePath)

    return Join-Path $runtimeRoot $RelativePath
}

function Test-RuntimeFile {
    param(
        [string]$RelativePath,
        [string]$Description,
        [bool]$Required = $true
    )

    $path = Get-RuntimePath $RelativePath
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Write-Check OK "$Description found: $RelativePath"
    }
    elseif ($Required) {
        Write-Check WARN "$Description is not bundled yet: $RelativePath"
    }
    else {
        Write-Check WARN "Optional $Description is not bundled yet: $RelativePath"
    }
}

function Test-RuntimeDirectory {
    param(
        [string]$RelativePath,
        [string]$Description,
        [bool]$Required = $true
    )

    $path = Get-RuntimePath $RelativePath
    if (Test-Path -LiteralPath $path -PathType Container) {
        Write-Check OK "$Description found: $RelativePath"
        return $true
    }

    if ($Required) {
        Write-Check WARN "$Description is not bundled yet: $RelativePath"
    }
    else {
        Write-Check WARN "Optional $Description is not bundled yet: $RelativePath"
    }

    return $false
}

function Test-Iw3Engine {
    $enginePath = Get-RuntimePath $runtimeDependencyPaths.Iw3EngineRoot
    if (-not (Test-Path -LiteralPath $enginePath -PathType Container)) {
        Write-Check WARN "Bundled iw3 engine is not bundled yet: $($runtimeDependencyPaths.Iw3EngineRoot)"
        Write-Check WARN "Expected iw3 layout: $($runtimeDependencyPaths.EngineManifest), $($runtimeDependencyPaths.PythonExecutable), $($runtimeDependencyPaths.PythonPathFile), engine\iw3\nunif\iw3\__main__.py, $($runtimeDependencyPaths.ModelsDirectory)"
        return
    }

    $hasNonPlaceholderManifest = $false
    $manifestPath = Get-RuntimePath $runtimeDependencyPaths.EngineManifest
    if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        try {
            $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
            if ($manifest.version -and $manifest.version -ne 'placeholder') {
                $hasNonPlaceholderManifest = $true
            }
        }
        catch {
        }
    }

    $engineEntryPaths = @(
        (Join-Path $enginePath 'nunif\iw3\__main__.py')
    )
    $hasEngineEntry = @($engineEntryPaths |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }).Count -gt 0

    if ($hasNonPlaceholderManifest -and $hasEngineEntry) {
        Write-Check OK "Bundled iw3 engine found: $($runtimeDependencyPaths.Iw3EngineRoot)"
        return
    }

    if ($hasNonPlaceholderManifest) {
        Write-Check WARN 'Bundled iw3 engine is incomplete. Missing entry file: engine\iw3\nunif\iw3\__main__.py'
        return
    }

    if ($hasEngineEntry) {
        Write-Check WARN "Bundled iw3 engine is incomplete. Missing non-placeholder manifest: $($runtimeDependencyPaths.EngineManifest)"
        return
    }

    $placeholderOrContractFiles = @(
        'README.md',
        'ENGINE_MANIFEST.json',
        'ENGINE_BUNDLE_CONTRACT.md',
        'IW3_CLI_CAPABILITIES.json',
        'MODEL_CATALOG.json'
    )
    $engineFiles = @(Get-ChildItem -LiteralPath $enginePath -File -Recurse |
        Where-Object {
            $relativePath = $_.FullName.Substring($enginePath.Length).TrimStart('\', '/')
            $firstSegment = $relativePath.Split(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar
            )[0]

            $firstSegment -notin @('models', 'python') -and
                $_.Name -notin $placeholderOrContractFiles
        })

    if ($engineFiles.Count -eq 0) {
        Write-Check WARN "Bundled iw3 engine contains placeholders or contract files only: $($runtimeDependencyPaths.Iw3EngineRoot)"
    }
    else {
        Write-Check WARN "Bundled iw3 engine is incomplete. Missing non-placeholder manifest: $($runtimeDependencyPaths.EngineManifest)"
    }
}

function Test-Iw3Models {
    $modelsPath = Get-RuntimePath $runtimeDependencyPaths.ModelsDirectory
    $modelExtensions = @('.pth', '.pt', '.onnx', '.safetensors', '.ckpt', '.bin')
    $modelFiles = @()

    if (Test-Path -LiteralPath $modelsPath -PathType Container) {
        $modelFiles = @(Get-ChildItem -LiteralPath $modelsPath -File -Recurse |
            Where-Object { $_.Extension -in $modelExtensions })
    }

    if ($modelFiles.Count -gt 0) {
        Write-Check OK "iw3 models found: $($runtimeDependencyPaths.ModelsDirectory)"
    }
    else {
        Write-Check WARN "iw3 models are not bundled yet: $($runtimeDependencyPaths.ModelsDirectory)"
    }
}

function Test-RuntimeDependencyLayout {
    Write-Host "Runtime dependency root: $runtimeRoot"
    Write-Host 'Runtime dependency checks inspect local files only. They do not run the app, Python, iw3, FFmpeg, model loading, downloads, or installs.'

    Test-RuntimeFile $runtimeDependencyPaths.FfmpegExecutable 'Bundled FFmpeg executable'
    Test-RuntimeFile $runtimeDependencyPaths.FfprobeExecutable 'Bundled FFprobe executable'
    Test-RuntimeDirectory $runtimeDependencyPaths.Iw3EngineRoot 'Bundled iw3 engine root' | Out-Null
    Test-RuntimeFile $runtimeDependencyPaths.PythonExecutable 'Embedded Python executable'
    Test-RuntimeFile $runtimeDependencyPaths.PythonPathFile 'Embedded Python path file'
    Test-RuntimeDirectory $runtimeDependencyPaths.NunifRootDirectory 'nunif root directory' | Out-Null
    Test-RuntimeDirectory $runtimeDependencyPaths.Iw3PackageDirectory 'iw3 package directory' | Out-Null
    Test-RuntimeDirectory $runtimeDependencyPaths.ModelsDirectory 'iw3 pretrained models directory' | Out-Null
    Test-RuntimeFile $runtimeDependencyPaths.Iw3RowFlowRuntimeDependency 'iw3 row_flow_v3 runtime dependency'
    Test-Iw3Engine
    Test-Iw3Models

    Test-RuntimeFile $runtimeDependencyPaths.ModelCatalog 'MODEL_CATALOG.json metadata' $false
    Test-RuntimeFile $runtimeDependencyPaths.Iw3CliCapabilities 'IW3_CLI_CAPABILITIES.json metadata' $false
    Write-Check WARN 'Optional metadata files are diagnostic only and do not make the iw3 engine ready by themselves.'
}

$hasErrors = $false

if ($IsWindows -or $env:OS -eq 'Windows_NT') {
    Write-Check OK 'Windows environment detected.'
}
else {
    Write-Check ERROR 'v3dfy development requires Windows.'
    $hasErrors = $true
}

if ([Environment]::Is64BitOperatingSystem) {
    Write-Check OK 'Operating system architecture is x64.'
}
else {
    Write-Check ERROR 'v3dfy requires a Windows x64 environment.'
    $hasErrors = $true
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    Write-Check OK "dotnet found: $($dotnet.Source)"

    $sdkVersions = @(& dotnet --list-sdks)
    if ($sdkVersions.Count -gt 0) {
        Write-Check OK "dotnet SDK available: $($sdkVersions -join ', ')"
    }
    else {
        Write-Check ERROR 'No dotnet SDK was reported by dotnet --list-sdks.'
        $hasErrors = $true
    }

    $wpfTemplates = @(& dotnet new list wpf 2>$null)
    if ($LASTEXITCODE -eq 0 -and $wpfTemplates.Count -gt 0) {
        Write-Check OK 'WPF project template is available.'
    }
    else {
        Write-Check ERROR 'WPF project template is not available.'
        $hasErrors = $true
    }
}
else {
    Write-Check ERROR 'dotnet is not available.'
    $hasErrors = $true
}

$git = Get-Command git -ErrorAction SilentlyContinue
if ($git) {
    Write-Check OK "git found: $($git.Source)"
}
else {
    Write-Check ERROR 'git is not available.'
    $hasErrors = $true
}

if (Test-Path -LiteralPath (Join-Path $repoRoot 'v3dfy.slnx')) {
    Write-Check OK 'v3dfy.slnx found.'
}
else {
    Write-Check ERROR 'v3dfy.slnx is missing.'
    $hasErrors = $true
}

Test-RuntimeDependencyLayout

if ($hasErrors) {
    exit 1
}

Write-Check OK 'Developer environment check completed.'

