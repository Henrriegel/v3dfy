[CmdletBinding()]
param(
    [string]$Version = '0.1.0-preview.1',
    [string]$ReleaseBaseUrl = 'https://github.com/Henrriegel/v3dfy/releases/download/v0.1.0-preview.1',
    [string]$PayloadPartsDir = 'artifacts\release\split',
    [string]$OutputDir = 'artifacts\installer',
    [switch]$SkipCompile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$bootstrapScript = Join-Path $repoRoot 'packaging\inno\v3dfy-payload-bootstrap.iss'
$helperProject = Join-Path $repoRoot 'src\V3dfy.SetupHelper\V3dfy.SetupHelper.csproj'
$portableZipFileName = "v3dfy-v$Version-win-x64-portable.zip"
$webInstallerName = "v3dfy-v$Version-web-setup.exe"
$offlineInstallerName = "v3dfy-v$Version-offline-setup.exe"

function Write-InstallerMessage {
    param(
        [ValidateSet('OK', 'WARN', 'FAIL', 'INFO')]
        [string]$Level,
        [string]$Message
    )

    $color = switch ($Level) {
        'OK' { 'Green' }
        'WARN' { 'Yellow' }
        'FAIL' { 'Red' }
        'INFO' { 'Cyan' }
    }

    Write-Host "[$Level] $Message" -ForegroundColor $color
}

function Resolve-RepositoryPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Assert-RepositoryChildPath {
    param(
        [string]$Path,
        [string]$Description
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootWithSeparator = if ($repoRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $repoRoot
    }
    else {
        $repoRoot + [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $fullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must stay inside the repository: $fullPath"
    }
}

function Get-InnoCompiler {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compiler) {
        return $compiler.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ }

    return $candidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

function Get-RequiredFile {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description is missing: $Path"
    }

    return Get-Item -LiteralPath $Path
}

function Get-PayloadPartFiles {
    param([string]$PartsDirectory)

    $parts = foreach ($index in 1..3) {
        $partName = '{0}.part{1:D2}' -f $portableZipFileName, $index
        Get-RequiredFile (Join-Path $PartsDirectory $partName) "Payload part $index"
    }

    return @($parts)
}

function Get-FinalZipHash {
    param(
        [string]$PartsDirectory,
        [string]$ZipFileName
    )

    $releaseDirectory = Split-Path -Parent $PartsDirectory
    $zipPath = Join-Path $releaseDirectory $ZipFileName
    if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
        Write-InstallerMessage INFO "Computing final ZIP SHA256: $zipPath"
        return (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
    }

    $checksumPath = Join-Path $PartsDirectory 'SHA256SUMS.txt'
    if (Test-Path -LiteralPath $checksumPath -PathType Leaf) {
        $hashLine = Get-Content -LiteralPath $checksumPath |
            Where-Object { $_ -match 'Hash\s*:\s*([A-Fa-f0-9]{64})' } |
            Select-Object -First 1

        if ($hashLine -match '([A-Fa-f0-9]{64})') {
            Write-InstallerMessage WARN "Using final ZIP SHA256 from checksum file because the ZIP was not found: $checksumPath"
            return $Matches[1].ToUpperInvariant()
        }
    }

    throw "Could not determine final ZIP SHA256. Expected $zipPath or $checksumPath."
}

function New-PayloadManifest {
    param(
        [System.IO.FileInfo[]]$PartFiles,
        [string]$ZipSha256,
        [string]$ManifestPath
    )

    $partEntries = foreach ($partFile in $PartFiles) {
        Write-InstallerMessage INFO "Computing payload part SHA256: $($partFile.Name)"
        $partHash = (Get-FileHash -LiteralPath $partFile.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        [ordered]@{
            fileName = $partFile.Name
            sha256 = $partHash
            sizeBytes = $partFile.Length
            url = ('{0}/{1}' -f $ReleaseBaseUrl.TrimEnd('/'), $partFile.Name)
        }
    }

    $zipSizeBytes = ($PartFiles | Measure-Object -Property Length -Sum).Sum
    $manifest = [ordered]@{
        productName = 'v3dfy'
        version = $Version
        releaseBaseUrl = $ReleaseBaseUrl.TrimEnd('/')
        zipFileName = $portableZipFileName
        zipSha256 = $ZipSha256
        zipSizeBytes = $zipSizeBytes
        parts = @($partEntries)
        requiredInstalledPaths = @(
            'V3dfy.App.exe',
            'engine\iw3',
            'engine\iw3\python\python.exe',
            'engine\iw3\nunif',
            'engine\iw3\nunif\iw3\pretrained_models',
            'tools\ffmpeg\win-x64\ffmpeg.exe',
            'tools\ffmpeg\win-x64\ffprobe.exe',
            'licenses'
        )
    }

    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
    Write-InstallerMessage OK "Generated payload manifest: $ManifestPath"
}

function Write-InstallerReadmes {
    param([string]$InstallerOutputDirectory)

    $webReadmePath = Join-Path $InstallerOutputDirectory 'README_WEB_INSTALLER.txt'
    $offlineReadmePath = Join-Path $InstallerOutputDirectory 'README_OFFLINE_INSTALLER.txt'

    @"
v3dfy v$Version Web Installer

Download:
$webInstallerName

This is the recommended installer for most users. The user downloads only the
setup EXE, then runs it on a Windows x64 PC.

Internet is required during installation. The installer downloads the shared
payload parts from:
$($ReleaseBaseUrl.TrimEnd('/'))

The installer verifies SHA256 for each part, rebuilds the portable ZIP,
verifies the final ZIP SHA256, installs v3dfy into Program Files, creates a
Start Menu shortcut, optionally creates a Desktop shortcut, and removes its
temporary downloaded and rebuilt files after a successful install.

During installation, a classic installer-style v3dfy setup progress window
shows live download progress, SHA256 verification, ZIP rebuild/extraction
status, percent/bytes progress, and a large scrolling setup log.

After installation, v3dfy works offline. The installed app includes the
self-contained .NET runtime, FFmpeg/FFprobe, embedded Python, local iw3 engine,
models, configuration, and licenses from the release payload.
"@ | Set-Content -LiteralPath $webReadmePath -Encoding UTF8

    @"
v3dfy v$Version Offline Installer

Download or copy all of these files into the same folder:
$offlineInstallerName
v3dfy-v$Version-win-x64-portable.zip.part01
v3dfy-v$Version-win-x64-portable.zip.part02
v3dfy-v$Version-win-x64-portable.zip.part03

Double-click $offlineInstallerName from that folder.

No internet and no PowerShell are required during installation. The installer
finds the payload parts beside the setup EXE, verifies SHA256 for each part,
rebuilds the portable ZIP, verifies the final ZIP SHA256, installs v3dfy into
Program Files, creates a Start Menu shortcut, optionally creates a Desktop
shortcut, and removes its temporary rebuilt ZIP after a successful install.

During installation, a classic installer-style v3dfy setup progress window
shows live verification, ZIP rebuild/extraction status, install status,
percent/bytes progress, and a large scrolling setup log.

After installation, v3dfy works offline. The installed app includes the
self-contained .NET runtime, FFmpeg/FFprobe, embedded Python, local iw3 engine,
models, configuration, and licenses from the release payload.
"@ | Set-Content -LiteralPath $offlineReadmePath -Encoding UTF8

    Write-InstallerMessage OK "Generated installer README files in $InstallerOutputDirectory"
}

function Publish-SetupHelper {
    param([string]$InstallerOutputDirectory)

    $helperPublishDirectory = Join-Path $InstallerOutputDirectory 'helper\win-x64'
    Assert-RepositoryChildPath $helperPublishDirectory 'Setup helper publish directory'
    if (Test-Path -LiteralPath $helperPublishDirectory -PathType Container) {
        Remove-Item -LiteralPath $helperPublishDirectory -Recurse -Force
    }

    Write-InstallerMessage INFO 'Publishing self-contained setup helper.'
    $publishOutput = & dotnet publish $helperProject `
        --configuration Release `
        --framework net10.0-windows `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        --output $helperPublishDirectory 2>&1

    $publishExitCode = $LASTEXITCODE
    foreach ($line in $publishOutput) {
        Write-Host $line
    }

    if ($publishExitCode -ne 0) {
        throw "dotnet publish for setup helper failed with exit code $publishExitCode."
    }

    $helperExe = Join-Path $helperPublishDirectory 'V3dfy.SetupHelper.exe'
    Get-RequiredFile $helperExe 'Published setup helper' | Out-Null
    return $helperExe
}

function Invoke-InnoCompiler {
    param(
        [string]$CompilerPath,
        [string]$InstallerOutputDirectory,
        [string]$HelperExe,
        [string]$ManifestPath,
        [string]$Flavor,
        [string]$OutputBaseFilename
    )

    $defines = @(
        "/DMyAppVersion=$Version",
        "/DOutputDir=$InstallerOutputDirectory",
        "/DOutputBaseFilename=$OutputBaseFilename",
        "/DHelperExe=$HelperExe",
        "/DManifestFile=$ManifestPath"
    )

    if ($Flavor -eq 'offline') {
        $defines += '/DOfflineInstaller=1'
    }
    else {
        $defines += '/DWebInstaller=1'
    }

    Write-InstallerMessage INFO "Compiling $Flavor installer with Inno Setup."
    & $CompilerPath @defines $bootstrapScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed for $Flavor installer with exit code $LASTEXITCODE."
    }
}

function Write-InstallerChecksums {
    param(
        [string]$InstallerOutputDirectory,
        [string[]]$InstallerNames
    )

    $checksumPath = Join-Path $InstallerOutputDirectory 'SHA256SUMS.installers.txt'
    $lines = foreach ($installerName in $InstallerNames) {
        $installerPath = Join-Path $InstallerOutputDirectory $installerName
        if (Test-Path -LiteralPath $installerPath -PathType Leaf) {
            $hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToUpperInvariant()
            "$hash  $installerName"
        }
    }

    if (-not $lines) {
        throw 'No installer EXE files were found for checksum generation.'
    }

    $lines | Set-Content -LiteralPath $checksumPath -Encoding ASCII
    Write-InstallerMessage OK "Generated installer checksums: $checksumPath"
}

function Write-InstallerSummary {
    param(
        [string]$InstallerOutputDirectory,
        [string[]]$InstallerNames
    )

    foreach ($installerName in $InstallerNames) {
        $installerPath = Join-Path $InstallerOutputDirectory $installerName
        if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
            Write-InstallerMessage WARN "Installer was not generated: $installerPath"
            continue
        }

        $file = Get-Item -LiteralPath $installerPath
        $sizeMiB = [Math]::Round($file.Length / 1MB, 2)
        $isUnderLimit = $file.Length -lt 2GB
        Write-InstallerMessage OK "$($file.Name): $sizeMiB MiB; under 2 GiB: $isUnderLimit"
    }
}

if (-not (Test-Path -LiteralPath $bootstrapScript -PathType Leaf)) {
    throw "Inno bootstrap script is missing: $bootstrapScript"
}

if (-not (Test-Path -LiteralPath $helperProject -PathType Leaf)) {
    throw "Setup helper project is missing: $helperProject"
}

$partsDirectory = Resolve-RepositoryPath $PayloadPartsDir
$installerOutputDirectory = Resolve-RepositoryPath $OutputDir
Assert-RepositoryChildPath $installerOutputDirectory 'Installer output directory'
New-Item -ItemType Directory -Force -Path $installerOutputDirectory | Out-Null

Write-InstallerMessage INFO "Version: $Version"
Write-InstallerMessage INFO "Release base URL: $($ReleaseBaseUrl.TrimEnd('/'))"
Write-InstallerMessage INFO "Payload parts directory: $partsDirectory"
Write-InstallerMessage INFO "Installer output directory: $installerOutputDirectory"

if (-not $SkipCompile.IsPresent) {
    $compilerPath = Get-InnoCompiler
    if (-not $compilerPath) {
        throw 'Inno Setup Compiler was not found. Install Inno Setup 6 or put ISCC.exe on PATH before building release installers.'
    }
}

$partFiles = Get-PayloadPartFiles $partsDirectory
$zipSha256 = Get-FinalZipHash $partsDirectory $portableZipFileName
$manifestPath = Join-Path $installerOutputDirectory "payload-manifest-v$Version.json"
New-PayloadManifest -PartFiles $partFiles -ZipSha256 $zipSha256 -ManifestPath $manifestPath
Write-InstallerReadmes $installerOutputDirectory

if ($SkipCompile.IsPresent) {
    Write-InstallerMessage WARN 'Skipping helper publish and Inno compilation by request.'
    exit 0
}

$helperExe = Publish-SetupHelper $installerOutputDirectory
Invoke-InnoCompiler `
    -CompilerPath $compilerPath `
    -InstallerOutputDirectory $installerOutputDirectory `
    -HelperExe $helperExe `
    -ManifestPath $manifestPath `
    -Flavor 'web' `
    -OutputBaseFilename ($webInstallerName -replace '\.exe$', '')

Invoke-InnoCompiler `
    -CompilerPath $compilerPath `
    -InstallerOutputDirectory $installerOutputDirectory `
    -HelperExe $helperExe `
    -ManifestPath $manifestPath `
    -Flavor 'offline' `
    -OutputBaseFilename ($offlineInstallerName -replace '\.exe$', '')

Write-InstallerChecksums $installerOutputDirectory @($webInstallerName, $offlineInstallerName)
Write-InstallerSummary $installerOutputDirectory @($webInstallerName, $offlineInstallerName)
Write-InstallerMessage OK 'Release installer packaging completed.'
