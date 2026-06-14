[CmdletBinding()]
param(
    [string]$ArtifactRoot,
    [string]$OutputPath,
    [string]$V3dfyVersion = '0.1.0-preview.1',
    [string]$ModelPackVersion = '0.1.0-preview.1',
    [string]$ReleaseTag = 'v0.1.0-preview.1',
    [string]$ModelPackReleaseBaseUrl = '',
    [string]$CurrentIw3Version = 'nunif-d23721f1'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path $repoRoot 'artifacts\model-packs'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ArtifactRoot "v3dfy-model-packs-v$ModelPackVersion.json"
}

if ([string]::IsNullOrWhiteSpace($ModelPackReleaseBaseUrl)) {
    $ModelPackReleaseBaseUrl = "https://github.com/Henrriegel/v3dfy/releases/download/$ReleaseTag"
}

function Write-Step {
    param(
        [ValidateSet('OK', 'WARN', 'FAIL', 'INFO')]
        [string]$Level,
        [string]$Message
    )

    $color = switch ($Level) {
        'OK' { 'Green' }
        'WARN' { 'Yellow' }
        'FAIL' { 'Red' }
        default { 'Cyan' }
    }

    Write-Host "[$Level] $Message" -ForegroundColor $color
}

function ConvertTo-ForwardSlashPath {
    param([string]$Path)
    $Path.Replace('\', '/')
}

function Write-Utf8File {
    param(
        [string]$Path,
        [string]$Text
    )

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $utf8NoBom)
}

function Get-FileSha256 {
    param([string]$Path)
    (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Read-ZipManifest {
    param([string]$ZipPath)

    Add-Type -AssemblyName System.IO.Compression | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entry = $archive.GetEntry('MODEL_PACK.json')
        if ($null -eq $entry) {
            throw "MODEL_PACK.json missing from $ZipPath"
        }

        $reader = New-Object System.IO.StreamReader($entry.Open())
        try {
            $json = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $json | ConvertFrom-Json
    }
    finally {
        $archive.Dispose()
    }
}

function Read-ChecksumMap {
    param([string]$Path)

    $map = @{}
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Aggregate model-pack checksum file was not found: $Path"
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if ($line -notmatch '^(?<sha>[0-9a-fA-F]{64})\s+(?<path>.+)$') {
            throw "Invalid checksum line in $Path`: $line"
        }

        $relativePath = ConvertTo-ForwardSlashPath $Matches.path.Trim()
        $map[$relativePath] = $Matches.sha.ToLowerInvariant()
    }

    $map
}

function Read-BuildSummaryMap {
    param([string]$Path)

    $map = @{}
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Write-Step WARN "Build summary not found; continuing with ZIP reports and aggregate checksums: $Path"
        return $map
    }

    $summary = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    foreach ($pack in @($summary.generatedOrValidated)) {
        if (-not [string]::IsNullOrWhiteSpace($pack.packId)) {
            $map[$pack.packId] = $pack
        }
    }

    $map
}

function Get-ReportField {
    param(
        [string]$ReportPath,
        [string]$FieldName
    )

    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
        return ''
    }

    $pattern = '^- ' + [regex]::Escape($FieldName) + ':\s*(?<value>.+?)\s*$'
    foreach ($line in Get-Content -LiteralPath $ReportPath) {
        if ($line -match $pattern) {
            return $Matches.value.Trim()
        }
    }

    ''
}

function New-PackMetadata {
    param(
        [string]$PackId,
        [string]$BestUseEnglish,
        [string]$BestUseSpanish,
        [string[]]$RecommendedFor,
        [string]$SizeCategory
    )

    [ordered]@{
        packId = $PackId
        bestUseEnglish = $BestUseEnglish
        bestUseSpanish = $BestUseSpanish
        recommendedFor = $RecommendedFor
        sizeCategory = $SizeCategory
    }
}

$approvedPacks = @(
    New-PackMetadata `
        -PackId 'depth-anything-v2-small' `
        -BestUseEnglish 'General movies, anime, mixed scenes, and quick tests.' `
        -BestUseSpanish 'Peliculas generales, anime, escenas mixtas y pruebas rapidas.' `
        -RecommendedFor @('general', 'anime', 'mixed-scenes', 'quick-tests') `
        -SizeCategory 'small'
    New-PackMetadata `
        -PackId 'depth-anything-small' `
        -BestUseEnglish 'Lightweight conversions when size and speed matter.' `
        -BestUseSpanish 'Conversiones ligeras cuando importan el tamano y la velocidad.' `
        -RecommendedFor @('quick-tests', 'low-storage', 'older-machines') `
        -SizeCategory 'small'
    New-PackMetadata `
        -PackId 'distill-any-depth-small' `
        -BestUseEnglish 'Small distilled model for quick comparisons and experiments.' `
        -BestUseSpanish 'Modelo destilado pequeno para comparaciones rapidas y pruebas.' `
        -RecommendedFor @('experimental', 'quick-tests', 'general') `
        -SizeCategory 'small'
    New-PackMetadata `
        -PackId 'depth-anything-v2-metric-hypersim-small' `
        -BestUseEnglish 'Indoor metric scenes such as rooms, corridors, offices, and CG interiors.' `
        -BestUseSpanish 'Escenas metricas interiores como habitaciones, pasillos, oficinas e interiores CG.' `
        -RecommendedFor @('metric', 'indoor', 'cg-interiors') `
        -SizeCategory 'small'
    New-PackMetadata `
        -PackId 'depth-anything-v2-metric-vkitti-small' `
        -BestUseEnglish 'Outdoor metric scenes such as roads, cars, city streets, sea, and boats.' `
        -BestUseSpanish 'Escenas metricas exteriores como carreteras, autos, calles, mar y barcos.' `
        -RecommendedFor @('metric', 'outdoor', 'roads', 'sea') `
        -SizeCategory 'small'
    New-PackMetadata `
        -PackId 'depth-anything-base' `
        -BestUseEnglish 'Balanced Depth Anything v1 model for movies, animation, and mixed scenes.' `
        -BestUseSpanish 'Modelo Depth Anything v1 equilibrado para peliculas, animacion y escenas mixtas.' `
        -RecommendedFor @('general', 'animation', 'mixed-scenes') `
        -SizeCategory 'base'
    New-PackMetadata `
        -PackId 'depth-anything-v2-metric-hypersim-base' `
        -BestUseEnglish 'Indoor metric base model for detailed interiors, rooms, hallways, and CG scenes.' `
        -BestUseSpanish 'Modelo metrico interior base para interiores detallados, habitaciones, pasillos y escenas CG.' `
        -RecommendedFor @('metric', 'indoor', 'quality') `
        -SizeCategory 'base'
    New-PackMetadata `
        -PackId 'depth-anything-v2-metric-vkitti-base' `
        -BestUseEnglish 'Outdoor metric base model for roads, landscapes, action, sea, and boats.' `
        -BestUseSpanish 'Modelo metrico exterior base para carreteras, paisajes, accion, mar y barcos.' `
        -RecommendedFor @('metric', 'outdoor', 'quality') `
        -SizeCategory 'base'
    New-PackMetadata `
        -PackId 'depth-anything-3-mono-large' `
        -BestUseEnglish 'Experimental large model for detailed movies, animation, and mixed scenes.' `
        -BestUseSpanish 'Modelo grande experimental para peliculas detalladas, animacion y escenas mixtas.' `
        -RecommendedFor @('experimental', 'quality', 'mixed-scenes', '3d-tv-variant') `
        -SizeCategory 'large'
    New-PackMetadata `
        -PackId 'depth-anything-large' `
        -BestUseEnglish 'Quality-focused Depth Anything v1 model for detailed movies and complex scenes.' `
        -BestUseSpanish 'Modelo Depth Anything v1 orientado a calidad para peliculas detalladas y escenas complejas.' `
        -RecommendedFor @('quality', 'detailed-scenes', 'general') `
        -SizeCategory 'large'
    New-PackMetadata `
        -PackId 'depth-anything-metric-indoor' `
        -BestUseEnglish 'Rooms, people indoors, movie interiors, offices, and dialogue scenes.' `
        -BestUseSpanish 'Habitaciones, personas en interiores, interiores de peliculas, oficinas y escenas de dialogo.' `
        -RecommendedFor @('metric', 'indoor', 'rooms', 'dialogue-scenes') `
        -SizeCategory 'large'
    New-PackMetadata `
        -PackId 'depth-anything-metric-outdoor' `
        -BestUseEnglish 'Streets, landscapes, beaches, sea, boats, and wide outdoor shots.' `
        -BestUseSpanish 'Calles, paisajes, playas, mar, barcos y planos exteriores amplios.' `
        -RecommendedFor @('metric', 'outdoor', 'landscapes', 'sea') `
        -SizeCategory 'large'
)

$excludedPackIds = @(
    'depth-pro',
    'depth-pro-small-resolution',
    'video-depth-anything-small',
    'video-depth-anything-stream-small',
    'metric-video-depth-anything-small',
    'metric-video-depth-anything-stream-small',
    'zoedepth-nyu',
    'zoedepth-kitti',
    'zoedepth-nyu-kitti'
)

function Assert-NotAbsolutePath {
    param(
        [string]$Value,
        [string]$FieldName
    )

    if ([System.IO.Path]::IsPathRooted($Value)) {
        throw "$FieldName must not be absolute: $Value"
    }

    if ($Value -match '^[A-Za-z]:') {
        throw "$FieldName must not include a drive root: $Value"
    }
}

if (-not (Test-Path -LiteralPath $ArtifactRoot -PathType Container)) {
    throw "Model-pack artifact root was not found: $ArtifactRoot"
}

$artifactRootFullPath = [System.IO.Path]::GetFullPath($ArtifactRoot)
$checksumPath = Join-Path $artifactRootFullPath 'SHA256SUMS.model-packs.txt'
$buildSummaryPath = Join-Path $artifactRootFullPath 'MODEL_PACK_BUILD_SUMMARY.json'
$checksumMap = Read-ChecksumMap $checksumPath
$buildSummaryMap = Read-BuildSummaryMap $buildSummaryPath
$baseUrl = $ModelPackReleaseBaseUrl.TrimEnd('/')

$packs = New-Object System.Collections.Generic.List[object]

foreach ($metadata in $approvedPacks) {
    $packId = $metadata.packId
    $packRoot = Join-Path $artifactRootFullPath $packId
    if (-not (Test-Path -LiteralPath $packRoot -PathType Container)) {
        throw "Expected approved model-pack folder is missing: $packId"
    }

    $assetFileName = "v3dfy-modelpack-$packId-v$ModelPackVersion.zip"
    Assert-NotAbsolutePath $assetFileName 'assetFileName'

    $relativeArtifactPath = ConvertTo-ForwardSlashPath (Join-Path $packId $assetFileName)
    Assert-NotAbsolutePath $relativeArtifactPath 'relativeArtifactPath'

    $zipPath = Join-Path $packRoot $assetFileName
    if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
        throw "Expected approved model-pack ZIP is missing: $relativeArtifactPath"
    }

    $manifest = Read-ZipManifest $zipPath
    if ($manifest.schemaVersion -ne 1) {
        throw "Unsupported MODEL_PACK.json schemaVersion for ${packId}: $($manifest.schemaVersion)"
    }

    if ($manifest.packId -ne $packId) {
        throw "MODEL_PACK.json packId mismatch for ${packId}: $($manifest.packId)"
    }

    if ($manifest.packVersion -ne $ModelPackVersion) {
        throw "MODEL_PACK.json packVersion mismatch for ${packId}: $($manifest.packVersion)"
    }

    $checkpoint = @($manifest.files | Where-Object { $_.role -eq 'checkpoint' }) | Select-Object -First 1
    if ($null -eq $checkpoint) {
        throw "MODEL_PACK.json has no checkpoint file entry for $packId"
    }

    $models = @($manifest.models)
    if ($models.Count -lt 1) {
        throw "MODEL_PACK.json has no model entries for $packId"
    }

    $zipHash = Get-FileSha256 $zipPath
    $zipSize = (Get-Item -LiteralPath $zipPath).Length
    $expectedChecksum = $checksumMap[$relativeArtifactPath]
    if ([string]::IsNullOrWhiteSpace($expectedChecksum)) {
        throw "Aggregate checksum entry missing for $relativeArtifactPath"
    }

    if ($expectedChecksum -ne $zipHash) {
        throw "Aggregate checksum mismatch for $relativeArtifactPath"
    }

    if ($buildSummaryMap.ContainsKey($packId)) {
        $summaryEntry = $buildSummaryMap[$packId]
        if ($summaryEntry.zipSha256 -and $summaryEntry.zipSha256.ToLowerInvariant() -ne $zipHash) {
            throw "Build summary ZIP SHA256 mismatch for $packId"
        }

        if ($summaryEntry.zipSizeBytes -and [int64]$summaryEntry.zipSizeBytes -ne $zipSize) {
            throw "Build summary ZIP size mismatch for $packId"
        }

        if ($summaryEntry.checkpointSha256 -and $summaryEntry.checkpointSha256.ToLowerInvariant() -ne $checkpoint.sha256) {
            throw "Build summary checkpoint SHA256 mismatch for $packId"
        }

        if ($summaryEntry.checkpointSizeBytes -and [int64]$summaryEntry.checkpointSizeBytes -ne [int64]$checkpoint.sizeBytes) {
            throw "Build summary checkpoint size mismatch for $packId"
        }
    }

    $reportPath = Join-Path $packRoot 'PACK_BUILD_REPORT.md'
    $sourceUrl = Get-ReportField $reportPath 'Checkpoint source'
    if ([string]::IsNullOrWhiteSpace($sourceUrl) -and $buildSummaryMap.ContainsKey($packId)) {
        $sourceUrl = $buildSummaryMap[$packId].checkpointSourceUrl
    }

    if ([string]::IsNullOrWhiteSpace($sourceUrl)) {
        throw "Source URL was not found in report or build summary for $packId"
    }

    $modelCardUrl = Get-ReportField $reportPath 'Model card'
    $license = Get-ReportField $reportPath 'License conclusion'
    $licenseEntry = @($manifest.files | Where-Object { $_.role -eq 'license' }) | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($license)) {
        $license = if ($licenseEntry -and $licenseEntry.path -match 'Apache-2\.0') {
            'SAFE_WITH_NOTICE / Apache-2.0'
        }
        else {
            'SAFE_WITH_NOTICE'
        }
    }
    elseif ($license -notmatch 'Apache-2\.0' -and $licenseEntry -and $licenseEntry.path -match 'Apache-2\.0') {
        $license = "$license / Apache-2.0"
    }

    $assetUrl = "$baseUrl/$assetFileName"
    $packEntry = [ordered]@{
        packId = $packId
        displayName = $manifest.displayName
        bestUseEnglish = $metadata.bestUseEnglish
        bestUseSpanish = $metadata.bestUseSpanish
        assetFileName = $assetFileName
        relativeArtifactPath = $relativeArtifactPath
        url = $assetUrl
        zipSha256 = $zipHash
        zipSizeBytes = $zipSize
        checkpointPath = $checkpoint.path
        checkpointSha256 = $checkpoint.sha256
        checkpointSizeBytes = [int64]$checkpoint.sizeBytes
        iw3DepthModelNames = @($models | ForEach-Object { $_.iw3DepthModelName })
        mappingKeys = @($models | ForEach-Object { $_.mappingKey })
        installerSelectable = $true
        defaultSelected = $false
        license = $license
        sourceUrl = $sourceUrl
        modelCardUrl = $modelCardUrl
        recommendedFor = @($metadata.recommendedFor)
        sizeCategory = $metadata.sizeCategory
    }

    $packs.Add($packEntry)
    Write-Step OK "Validated installer manifest pack: $packId"
}

foreach ($excludedPackId in $excludedPackIds) {
    if ($packs | Where-Object { $_.packId -eq $excludedPackId }) {
        throw "Excluded model pack was included: $excludedPackId"
    }
}

$manifestDocument = [ordered]@{
    schemaVersion = 1
    v3dfyVersion = $V3dfyVersion
    modelPackVersion = $ModelPackVersion
    releaseTag = $ReleaseTag
    modelPackReleaseBaseUrl = $baseUrl
    currentIw3Version = $CurrentIw3Version
    generatedUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    packs = @($packs.ToArray())
}

$json = $manifestDocument | ConvertTo-Json -Depth 16
Write-Utf8File $OutputPath $json

Write-Step OK "Installer model-pack manifest: $OutputPath"
Write-Step OK "Installer-selectable pack count: $($packs.Count)"
