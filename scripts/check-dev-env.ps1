[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

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

function Test-OptionalPath {
    param(
        [string]$RelativePath,
        [string]$Description
    )

    $path = Join-Path $repoRoot $RelativePath
    if (Test-Path -LiteralPath $path) {
        Write-Check OK "$Description found: $RelativePath"
    }
    else {
        Write-Check WARN "$Description is not bundled yet: $RelativePath"
    }
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

Test-OptionalPath 'tools\ffmpeg\win-x64\ffmpeg.exe' 'Bundled FFmpeg'
Test-OptionalPath 'tools\ffmpeg\win-x64\ffprobe.exe' 'Bundled FFprobe'
Test-OptionalPath 'engine\iw3\python\python.exe' 'Embedded Python'
Test-OptionalPath 'engine\iw3\models' 'iw3 models directory'

if ($hasErrors) {
    exit 1
}

Write-Check OK 'Developer environment check completed.'

