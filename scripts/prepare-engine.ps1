[CmdletBinding()]
param(
    [switch]$SkipDownloads,
    [string]$UseExistingFfmpegPath,
    [string]$UseExistingIw3Path,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$ffmpegDirectory = Join-Path $repoRoot 'tools\ffmpeg\win-x64'
$iw3Directory = Join-Path $repoRoot 'engine\iw3'
$modelsDirectory = Join-Path $iw3Directory 'models'
$pythonDirectory = Join-Path $iw3Directory 'python'
$licensesDirectory = Join-Path $repoRoot 'licenses'
$licenseDirectories = @('ffmpeg', 'python', 'pytorch', 'nunif', 'models') |
    ForEach-Object { Join-Path $licensesDirectory $_ }
$manifestPath = Join-Path $iw3Directory 'ENGINE_MANIFEST.json'

foreach ($directory in @(
    $ffmpegDirectory,
    $iw3Directory,
    $modelsDirectory,
    $pythonDirectory,
    $licensesDirectory,
    $licenseDirectories
)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

function Copy-RequiredFile {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required file not found: $Source"
    }

    if ((Test-Path -LiteralPath $Destination) -and -not $Force) {
        throw "Destination already exists: $Destination. Use -Force to overwrite it."
    }

    Copy-Item -LiteralPath $Source -Destination $Destination -Force:$Force
}

if ($UseExistingFfmpegPath) {
    $existingFfmpegDirectory = (Resolve-Path -LiteralPath $UseExistingFfmpegPath).Path
    Copy-RequiredFile `
        (Join-Path $existingFfmpegDirectory 'ffmpeg.exe') `
        (Join-Path $ffmpegDirectory 'ffmpeg.exe')
    Copy-RequiredFile `
        (Join-Path $existingFfmpegDirectory 'ffprobe.exe') `
        (Join-Path $ffmpegDirectory 'ffprobe.exe')
}

if ($UseExistingIw3Path) {
    $existingIw3Directory = (Resolve-Path -LiteralPath $UseExistingIw3Path).Path
    if ($existingIw3Directory -eq (Resolve-Path -LiteralPath $iw3Directory).Path) {
        throw 'The source iw3 directory must differ from the bundled destination directory.'
    }

    $existingDestinationItems = @(Get-ChildItem -LiteralPath $iw3Directory -Force)
    if ($existingDestinationItems.Count -gt 0 -and -not $Force) {
        throw 'The bundled iw3 directory is not empty. Use -Force to merge existing content.'
    }

    Copy-Item -Path (Join-Path $existingIw3Directory '*') `
        -Destination $iw3Directory `
        -Recurse `
        -Force:$Force
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
    @{
        engine = 'iw3'
        version = 'placeholder'
        runtime = 'embedded-python-placeholder'
        models = @('placeholder')
        createdAt = [DateTime]::UtcNow.ToString('o')
    } |
        ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath $manifestPath -Encoding utf8
}

if (-not $SkipDownloads) {
    Write-Warning 'Downloads are intentionally disabled. Supply existing local paths when components are available.'
}

Write-Host 'Engine support structure is ready. No internet access was used.' -ForegroundColor Green
