[CmdletBinding()]
param(
    [string]$PackVersion = '0.1.0-preview.1',
    [string]$MinV3dfyVersion = '0.1.0',
    [string]$CurrentIw3Version = 'nunif-d23721f1'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts\model-packs'
$setupHelperDll = Join-Path $repoRoot 'src\V3dfy.SetupHelper\bin\Debug\net10.0\V3dfy.SetupHelper.dll'

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

function Get-RelativePath {
    param(
        [string]$Root,
        [string]$Path
    )

    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes root. Root: $rootPath Path: $fullPath"
    }

    ConvertTo-ForwardSlashPath $fullPath.Substring($rootPath.Length).TrimStart('\', '/')
}

function Get-FileSha256 {
    param([string]$Path)
    (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Save-Url {
    param(
        [string]$Url,
        [string]$DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath -PathType Leaf) {
        Write-Step INFO "Using existing download: $DestinationPath"
        return
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $DestinationPath) | Out-Null
    $partialPath = "$DestinationPath.partial"
    Write-Step INFO "Downloading $Url"
    Invoke-WebRequest -Uri $Url -OutFile $partialPath -UseBasicParsing
    Move-Item -LiteralPath $partialPath -Destination $DestinationPath -Force
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

function Get-PackCitation {
    param([string]$CitationKind)

    switch ($CitationKind) {
        'depth-anything-v1' {
            @'
Official Depth Anything citation from the upstream README/model card:

@inproceedings{depth_anything_v1,
  title={Depth Anything: Unleashing the Power of Large-Scale Unlabeled Data},
  author={Yang, Lihe and Kang, Bingyi and Huang, Zilong and Xu, Xiaogang and Feng, Jiashi and Zhao, Hengshuang},
  booktitle={CVPR},
  year={2024}
}
'@
        }
        'depth-anything-v2' {
            @'
Official Depth Anything V2 citations from the upstream model card/README:

@article{depth_anything_v2,
  title={Depth Anything V2},
  author={Yang, Lihe and Kang, Bingyi and Huang, Zilong and Zhao, Zhen and Xu, Xiaogang and Feng, Jiashi and Zhao, Hengshuang},
  journal={arXiv:2406.09414},
  year={2024}
}

@inproceedings{depth_anything_v1,
  title={Depth Anything: Unleashing the Power of Large-Scale Unlabeled Data},
  author={Yang, Lihe and Kang, Bingyi and Huang, Zilong and Xu, Xiaogang and Feng, Jiashi and Zhao, Hengshuang},
  booktitle={CVPR},
  year={2024}
}
'@
        }
        'distill-any-depth' {
            @'
Official Distill Any Depth citation from the upstream README:

@article{he2025distill,
  title = {Distill Any Depth: Distillation Creates a Stronger Monocular Depth Estimator},
  author = {Xiankang He and Dongyan Guo and Hongji Li and Ruibo Li and Ying Cui and Chi Zhang},
  year = {2025},
  journal = {arXiv preprint arXiv: 2502.19204}
}
'@
        }
        'depth-anything-3' {
            @'
Official Depth Anything 3 citation from the DA3MONO-LARGE Hugging Face model card:

@article{depthanything3,
  title={Depth Anything 3: Recovering the visual space from any views},
  author={Haotong Lin and Sili Chen and Jun Hao Liew and Donny Y. Chen and Zhenyu Li and Guang Shi and Jiashi Feng and Bingyi Kang},
  journal={arXiv preprint arXiv:2511.10647},
  year={2025}
}
'@
        }
        default {
            "No citation template configured for $CitationKind."
        }
    }
}

function New-ManifestFileEntry {
    param(
        [string]$StagingRoot,
        [string]$RelativePath,
        [string]$Role
    )

    $fullPath = Join-Path $StagingRoot ($RelativePath -replace '/', '\')
    [ordered]@{
        path = $RelativePath
        sha256 = Get-FileSha256 $fullPath
        sizeBytes = (Get-Item -LiteralPath $fullPath).Length
        role = $Role
    }
}

function Add-ZipEntry {
    param(
        [System.IO.Compression.ZipArchive]$Archive,
        [string]$SourcePath,
        [string]$EntryName
    )

    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $Archive,
        $SourcePath,
        $EntryName,
        [System.IO.Compression.CompressionLevel]::NoCompression) | Out-Null
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

function Test-ZipContents {
    param(
        [string]$ZipPath,
        [string[]]$ExpectedEntries
    )

    Add-Type -AssemblyName System.IO.Compression | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null

    $errors = New-Object System.Collections.Generic.List[string]
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entries = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName } | Sort-Object)
        $expected = @($ExpectedEntries | Sort-Object)
        foreach ($entry in $expected) {
            if ($entries -notcontains $entry) {
                $errors.Add("Missing ZIP entry: $entry")
            }
        }

        foreach ($entry in $entries) {
            if ($expected -notcontains $entry) {
                $errors.Add("Unexpected ZIP entry: $entry")
            }
        }

        $manifest = Read-ZipManifest $ZipPath
        foreach ($file in $manifest.files) {
            $entry = $archive.GetEntry($file.path)
            if ($null -eq $entry) {
                $errors.Add("Manifest file missing in ZIP: $($file.path)")
                continue
            }

            if ($entry.Length -ne [int64]$file.sizeBytes) {
                $errors.Add("Size mismatch for $($file.path)")
                continue
            }

            $stream = $entry.Open()
            try {
                $sha = [System.BitConverter]::ToString(
                    [System.Security.Cryptography.SHA256]::Create().ComputeHash($stream)).
                    Replace('-', '').
                    ToLowerInvariant()
            }
            finally {
                $stream.Dispose()
            }

            if ($sha -ne $file.sha256) {
                $errors.Add("SHA256 mismatch for $($file.path)")
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    [ordered]@{
        success = $errors.Count -eq 0
        errors = @($errors)
        zipPath = $ZipPath
        entryCount = @($ExpectedEntries).Count
        entries = @($ExpectedEntries | Sort-Object)
    }
}

function Ensure-InventoryValidator {
    $validatorRoot = Join-Path $artifactRoot '_validation\InventoryValidation'
    New-Item -ItemType Directory -Force -Path $validatorRoot | Out-Null

    $projectPath = Join-Path $validatorRoot 'InventoryValidation.csproj'
    $programPath = Join-Path $validatorRoot 'Program.cs'

    Write-Utf8File $projectPath @'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\src\V3dfy.Core\V3dfy.Core.csproj" />
    <ProjectReference Include="..\..\..\..\src\V3dfy.Infrastructure\V3dfy.Infrastructure.csproj" />
    <ProjectReference Include="..\..\..\..\src\V3dfy.Engine.Iw3\V3dfy.Engine.Iw3.csproj" />
  </ItemGroup>
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseAppHost>false</UseAppHost>
  </PropertyGroup>
</Project>
'@

    Write-Utf8File $programPath @'
using System.Text.Json;
using V3dfy.Core.Models;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Infrastructure.Health;

if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: InventoryValidation <target-root> <expected-relative-path> <mapping-keys-pipe> <iw3-names-pipe>");
    return 2;
}

var targetRoot = Path.GetFullPath(args[0]);
var expectedRelativePath = args[1].Replace('\\', '/');
var expectedKeys = args[2].Split('|', StringSplitOptions.RemoveEmptyEntries);
var expectedDepthNames = args[3].Split('|', StringSplitOptions.RemoveEmptyEntries);
var appRoot = Path.GetFullPath(Path.Combine(targetRoot, "..", "..", "..", "..", ".."));
var paths = new InternalToolPaths(
    FfmpegExecutable: Path.Combine(appRoot, "tools", "ffmpeg", "win-x64", "ffmpeg.exe"),
    FfprobeExecutable: Path.Combine(appRoot, "tools", "ffmpeg", "win-x64", "ffprobe.exe"),
    PythonExecutable: Path.Combine(appRoot, "engine", "iw3", "python", "python.exe"),
    Iw3EngineDirectory: Path.Combine(appRoot, "engine", "iw3"),
    ModelsDirectory: targetRoot);

var errors = new List<string>();
var checkpointPath = Path.Combine(targetRoot, Path.Combine(expectedRelativePath.Split('/')));
if (!File.Exists(checkpointPath))
{
    errors.Add("Expected checkpoint is missing from temp install root.");
}

foreach (var expectedKey in expectedKeys)
{
    var entry = Iw3DepthModelMapper.RegistryEntries.SingleOrDefault(e => e.Key == expectedKey);
    if (entry is null)
    {
        errors.Add($"Registry entry missing: {expectedKey}");
        continue;
    }

    if (!entry.IsReadySelectable) errors.Add($"Registry entry is not ready-selectable: {expectedKey}");
    if (!entry.IsUserVisibleInSelector) errors.Add($"Registry entry is not user-visible: {expectedKey}");
    if (!entry.IsPublicPackEligible) errors.Add($"Registry entry is not public-pack eligible: {expectedKey}");
    if (!entry.ExpectedRelativePaths.Contains(expectedRelativePath, StringComparer.OrdinalIgnoreCase))
    {
        errors.Add($"Registry entry has wrong expected relative path: {expectedKey}");
    }
}

var health = new InternalToolsHealthChecker().CheckDetailed(paths);
var selectable = Iw3DepthModelMapper.CreateSelectableCandidates(
    health.ModelInventory.SelectionCandidates,
    useSpanish: false);
var unmapped = Iw3DepthModelMapper.GetUnmappedCandidates(
    health.ModelInventory.SelectionCandidates);

if (health.ModelInventory.CompatibleModelCount != 1)
{
    errors.Add($"Expected one compatible model file, found {health.ModelInventory.CompatibleModelCount}.");
}

if (health.ModelInventory.SelectionCandidates.Count != 1)
{
    errors.Add($"Expected one raw selection candidate, found {health.ModelInventory.SelectionCandidates.Count}.");
}

if (selectable.Count != expectedKeys.Length)
{
    errors.Add($"Expected {expectedKeys.Length} selectable mapped candidate(s), found {selectable.Count}.");
}

if (unmapped.Count != 0)
{
    errors.Add($"Expected no unmapped compatible candidates, found {unmapped.Count}.");
}

foreach (var expectedKey in expectedKeys)
{
    if (!selectable.Any(candidate => string.Equals(candidate.MappingKey, expectedKey, StringComparison.OrdinalIgnoreCase)))
    {
        errors.Add($"Expected selectable mapping key was not found: {expectedKey}");
    }
}

foreach (var expectedDepthName in expectedDepthNames)
{
    if (!selectable.Any(candidate => string.Equals(candidate.Iw3DepthModelName, expectedDepthName, StringComparison.OrdinalIgnoreCase)))
    {
        errors.Add($"Expected iw3 depth model was not found: {expectedDepthName}");
    }
}

var unexpected = selectable
    .Where(candidate => candidate.MappingKey is null || !expectedKeys.Contains(candidate.MappingKey, StringComparer.OrdinalIgnoreCase))
    .Select(candidate => candidate.MappingKey ?? candidate.Id)
    .ToArray();
if (unexpected.Length > 0)
{
    errors.Add("Unexpected selectable model(s): " + string.Join(", ", unexpected));
}

var summary = new
{
    success = errors.Count == 0,
    errors,
    targetRoot,
    checkpointExists = File.Exists(checkpointPath),
    compatibleModelCount = health.ModelInventory.CompatibleModelCount,
    rawSelectionCandidateCount = health.ModelInventory.SelectionCandidates.Count,
    selectableCount = selectable.Count,
    unmappedCount = unmapped.Count,
    mappings = selectable.Select(candidate => new
    {
        candidate.MappingKey,
        candidate.Iw3DepthModelName,
        candidate.RelativePath,
    }).ToArray()
};

Console.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
return errors.Count == 0 ? 0 : 1;
'@

    $projectPath
}

function New-PackDefinition {
    param(
        [string]$PackId,
        [string]$DisplayName,
        [string[]]$MappingKeys,
        [string[]]$Iw3DepthModelNames,
        [string]$CheckpointFile,
        [string]$CheckpointUrl,
        [string]$UpstreamFileName,
        [string]$ModelCardUrl,
        [string]$ModelCardRawUrl,
        [string]$RepositoryUrl,
        [string]$RepositoryReadmeUrl,
        [string]$LicenseUrl,
        [string]$LicenseFileName,
        [string]$CitationKind,
        [string]$Family,
        [string]$Purpose,
        [string]$Category,
        [bool]$RenamedForIw3 = $false
    )

    [ordered]@{
        packId = $PackId
        displayName = $DisplayName
        mappingKeys = $MappingKeys
        iw3DepthModelNames = $Iw3DepthModelNames
        checkpointFile = $CheckpointFile
        expectedRelativePath = "hub/checkpoints/$CheckpointFile"
        checkpointUrl = $CheckpointUrl
        upstreamFileName = $UpstreamFileName
        modelCardUrl = $ModelCardUrl
        modelCardRawUrl = $ModelCardRawUrl
        repositoryUrl = $RepositoryUrl
        repositoryReadmeUrl = $RepositoryReadmeUrl
        licenseUrl = $LicenseUrl
        licenseFileName = $LicenseFileName
        citationKind = $CitationKind
        family = $Family
        purpose = $Purpose
        category = $Category
        renamedForIw3 = $RenamedForIw3
    }
}

$depthAnythingSpace = 'https://huggingface.co/spaces/LiheYoung/Depth-Anything'
$depthAnythingV2Repo = 'https://github.com/DepthAnything/Depth-Anything-V2'

$packs = @(
    New-PackDefinition `
        -PackId 'depth-anything-metric-indoor' `
        -DisplayName 'Depth Anything Metric Indoor' `
        -MappingKeys @('depth-anything-metric-indoor') `
        -Iw3DepthModelNames @('ZoeD_Any_N') `
        -CheckpointFile 'depth_anything_metric_depth_indoor.pt' `
        -CheckpointUrl "$depthAnythingSpace/resolve/main/checkpoints_metric_depth/depth_anything_metric_depth_indoor.pt" `
        -UpstreamFileName 'depth_anything_metric_depth_indoor.pt' `
        -ModelCardUrl $depthAnythingSpace `
        -ModelCardRawUrl "$depthAnythingSpace/raw/main/README.md" `
        -RepositoryUrl 'https://github.com/LiheYoung/Depth-Anything' `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-Apache-2.0.txt' `
        -CitationKind 'depth-anything-v1' `
        -Family 'Depth Anything metric' `
        -Purpose 'Metric indoor model for rooms, people indoors, and movie interiors.' `
        -Category 'metric-indoor'
    New-PackDefinition `
        -PackId 'depth-anything-metric-outdoor' `
        -DisplayName 'Depth Anything Metric Outdoor' `
        -MappingKeys @('depth-anything-metric-outdoor') `
        -Iw3DepthModelNames @('ZoeD_Any_K') `
        -CheckpointFile 'depth_anything_metric_depth_outdoor.pt' `
        -CheckpointUrl "$depthAnythingSpace/resolve/main/checkpoints_metric_depth/depth_anything_metric_depth_outdoor.pt" `
        -UpstreamFileName 'depth_anything_metric_depth_outdoor.pt' `
        -ModelCardUrl $depthAnythingSpace `
        -ModelCardRawUrl "$depthAnythingSpace/raw/main/README.md" `
        -RepositoryUrl 'https://github.com/LiheYoung/Depth-Anything' `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-Apache-2.0.txt' `
        -CitationKind 'depth-anything-v1' `
        -Family 'Depth Anything metric' `
        -Purpose 'Metric outdoor model for road, landscape, and outdoor scenes.' `
        -Category 'metric-outdoor'
    New-PackDefinition `
        -PackId 'depth-anything-small' `
        -DisplayName 'Depth Anything Small' `
        -MappingKeys @('depth-anything-small') `
        -Iw3DepthModelNames @('Any_S') `
        -CheckpointFile 'depth_anything_vits14.pth' `
        -CheckpointUrl "$depthAnythingSpace/resolve/main/checkpoints/depth_anything_vits14.pth" `
        -UpstreamFileName 'depth_anything_vits14.pth' `
        -ModelCardUrl $depthAnythingSpace `
        -ModelCardRawUrl "$depthAnythingSpace/raw/main/README.md" `
        -RepositoryUrl 'https://github.com/LiheYoung/Depth-Anything' `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-Apache-2.0.txt' `
        -CitationKind 'depth-anything-v1' `
        -Family 'Depth Anything v1' `
        -Purpose 'Lightweight general relative-depth model.' `
        -Category 'relative-depth'
    New-PackDefinition `
        -PackId 'depth-anything-base' `
        -DisplayName 'Depth Anything Base' `
        -MappingKeys @('depth-anything-base') `
        -Iw3DepthModelNames @('Any_B') `
        -CheckpointFile 'depth_anything_vitb14.pth' `
        -CheckpointUrl "$depthAnythingSpace/resolve/main/checkpoints/depth_anything_vitb14.pth" `
        -UpstreamFileName 'depth_anything_vitb14.pth' `
        -ModelCardUrl $depthAnythingSpace `
        -ModelCardRawUrl "$depthAnythingSpace/raw/main/README.md" `
        -RepositoryUrl 'https://github.com/LiheYoung/Depth-Anything' `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-Apache-2.0.txt' `
        -CitationKind 'depth-anything-v1' `
        -Family 'Depth Anything v1' `
        -Purpose 'Balanced general relative-depth model.' `
        -Category 'relative-depth'
    New-PackDefinition `
        -PackId 'depth-anything-large' `
        -DisplayName 'Depth Anything Large' `
        -MappingKeys @('depth-anything-large') `
        -Iw3DepthModelNames @('Any_L') `
        -CheckpointFile 'depth_anything_vitl14.pth' `
        -CheckpointUrl "$depthAnythingSpace/resolve/main/checkpoints/depth_anything_vitl14.pth" `
        -UpstreamFileName 'depth_anything_vitl14.pth' `
        -ModelCardUrl $depthAnythingSpace `
        -ModelCardRawUrl "$depthAnythingSpace/raw/main/README.md" `
        -RepositoryUrl 'https://github.com/LiheYoung/Depth-Anything' `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-Apache-2.0.txt' `
        -CitationKind 'depth-anything-v1' `
        -Family 'Depth Anything v1' `
        -Purpose 'Large general relative-depth model for quality-focused conversions.' `
        -Category 'relative-depth'
    New-PackDefinition `
        -PackId 'depth-anything-v2-metric-hypersim-small' `
        -DisplayName 'Depth Anything V2 Metric Hypersim Small' `
        -MappingKeys @('depth-anything-v2-metric-hypersim-small') `
        -Iw3DepthModelNames @('Any_V2_N_S') `
        -CheckpointFile 'depth_anything_v2_metric_hypersim_vits.pth' `
        -CheckpointUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-Hypersim-Small/resolve/main/depth_anything_v2_metric_hypersim_vits.pth' `
        -UpstreamFileName 'depth_anything_v2_metric_hypersim_vits.pth' `
        -ModelCardUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-Hypersim-Small' `
        -ModelCardRawUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-Hypersim-Small/raw/main/README.md' `
        -RepositoryUrl $depthAnythingV2Repo `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/metric_depth/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-V2-Apache-2.0.txt' `
        -CitationKind 'depth-anything-v2' `
        -Family 'Depth Anything V2 metric' `
        -Purpose 'Small metric model tuned for indoor-like Hypersim scenes.' `
        -Category 'metric-indoor'
    New-PackDefinition `
        -PackId 'depth-anything-v2-metric-hypersim-base' `
        -DisplayName 'Depth Anything V2 Metric Hypersim Base' `
        -MappingKeys @('depth-anything-v2-metric-hypersim-base') `
        -Iw3DepthModelNames @('Any_V2_N_B') `
        -CheckpointFile 'depth_anything_v2_metric_hypersim_vitb.pth' `
        -CheckpointUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-Hypersim-Base/resolve/main/depth_anything_v2_metric_hypersim_vitb.pth' `
        -UpstreamFileName 'depth_anything_v2_metric_hypersim_vitb.pth' `
        -ModelCardUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-Hypersim-Base' `
        -ModelCardRawUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-Hypersim-Base/raw/main/README.md' `
        -RepositoryUrl $depthAnythingV2Repo `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/metric_depth/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-V2-Apache-2.0.txt' `
        -CitationKind 'depth-anything-v2' `
        -Family 'Depth Anything V2 metric' `
        -Purpose 'Base metric model tuned for indoor-like Hypersim scenes.' `
        -Category 'metric-indoor'
    New-PackDefinition `
        -PackId 'depth-anything-v2-metric-vkitti-small' `
        -DisplayName 'Depth Anything V2 Metric VKITTI Small' `
        -MappingKeys @('depth-anything-v2-metric-vkitti-small') `
        -Iw3DepthModelNames @('Any_V2_K_S') `
        -CheckpointFile 'depth_anything_v2_metric_vkitti_vits.pth' `
        -CheckpointUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-VKITTI-Small/resolve/main/depth_anything_v2_metric_vkitti_vits.pth' `
        -UpstreamFileName 'depth_anything_v2_metric_vkitti_vits.pth' `
        -ModelCardUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-VKITTI-Small' `
        -ModelCardRawUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-VKITTI-Small/raw/main/README.md' `
        -RepositoryUrl $depthAnythingV2Repo `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/metric_depth/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-V2-Apache-2.0.txt' `
        -CitationKind 'depth-anything-v2' `
        -Family 'Depth Anything V2 metric' `
        -Purpose 'Small metric model tuned for outdoor VKITTI scenes.' `
        -Category 'metric-outdoor'
    New-PackDefinition `
        -PackId 'depth-anything-v2-metric-vkitti-base' `
        -DisplayName 'Depth Anything V2 Metric VKITTI Base' `
        -MappingKeys @('depth-anything-v2-metric-vkitti-base') `
        -Iw3DepthModelNames @('Any_V2_K_B') `
        -CheckpointFile 'depth_anything_v2_metric_vkitti_vitb.pth' `
        -CheckpointUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-VKITTI-Base/resolve/main/depth_anything_v2_metric_vkitti_vitb.pth' `
        -UpstreamFileName 'depth_anything_v2_metric_vkitti_vitb.pth' `
        -ModelCardUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-VKITTI-Base' `
        -ModelCardRawUrl 'https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-VKITTI-Base/raw/main/README.md' `
        -RepositoryUrl $depthAnythingV2Repo `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/metric_depth/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-V2-Apache-2.0.txt' `
        -CitationKind 'depth-anything-v2' `
        -Family 'Depth Anything V2 metric' `
        -Purpose 'Base metric model tuned for outdoor VKITTI scenes.' `
        -Category 'metric-outdoor'
    New-PackDefinition `
        -PackId 'distill-any-depth-small' `
        -DisplayName 'Distill Any Depth Small' `
        -MappingKeys @('distill-any-depth-small') `
        -Iw3DepthModelNames @('Distill_Any_S') `
        -CheckpointFile 'distill_any_depth_vits.safetensors' `
        -CheckpointUrl 'https://huggingface.co/xingyang1/Distill-Any-Depth/resolve/main/small/model.safetensors' `
        -UpstreamFileName 'small/model.safetensors' `
        -ModelCardUrl 'https://huggingface.co/xingyang1/Distill-Any-Depth' `
        -ModelCardRawUrl 'https://huggingface.co/xingyang1/Distill-Any-Depth/raw/main/README.md' `
        -RepositoryUrl 'https://github.com/Westlake-AGI-Lab/Distill-Any-Depth' `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/Westlake-AGI-Lab/Distill-Any-Depth/main/README.md' `
        -LicenseUrl 'https://www.apache.org/licenses/LICENSE-2.0.txt' `
        -LicenseFileName 'LICENSE-Distill-Any-Depth-Apache-2.0.txt' `
        -CitationKind 'distill-any-depth' `
        -Family 'Distill Any Depth' `
        -Purpose 'Small distilled general depth model.' `
        -Category 'relative-depth' `
        -RenamedForIw3 $true
    New-PackDefinition `
        -PackId 'depth-anything-3-mono-large' `
        -DisplayName 'Depth Anything 3 Mono Large' `
        -MappingKeys @('depth-anything-3-mono-large', 'depth-anything-3-mono-large-3d-tv') `
        -Iw3DepthModelNames @('Any_V3_Mono', 'Any_V3_Mono_01') `
        -CheckpointFile 'da3mono-large.safetensors' `
        -CheckpointUrl 'https://huggingface.co/depth-anything/DA3MONO-LARGE/resolve/main/model.safetensors' `
        -UpstreamFileName 'model.safetensors' `
        -ModelCardUrl 'https://huggingface.co/depth-anything/DA3MONO-LARGE' `
        -ModelCardRawUrl 'https://huggingface.co/depth-anything/DA3MONO-LARGE/raw/main/README.md' `
        -RepositoryUrl 'https://github.com/ByteDance-Seed/Depth-Anything-3' `
        -RepositoryReadmeUrl 'https://raw.githubusercontent.com/ByteDance-Seed/Depth-Anything-3/main/README.md' `
        -LicenseUrl 'https://raw.githubusercontent.com/ByteDance-Seed/Depth-Anything-3/main/LICENSE' `
        -LicenseFileName 'LICENSE-Depth-Anything-3-Apache-2.0.txt' `
        -CitationKind 'depth-anything-3' `
        -Family 'Depth Anything 3 monocular' `
        -Purpose 'Large Depth Anything 3 monocular relative-depth model with two iw3 scaler variants.' `
        -Category 'relative-depth' `
        -RenamedForIw3 $true
)

function Build-ModelPack {
    param([hashtable]$Pack)

    $packId = $Pack.packId
    $packRoot = Join-Path $artifactRoot $packId
    $downloadsRoot = Join-Path $packRoot 'downloads'
    $stagingRoot = Join-Path $packRoot 'staging'
    $validationRoot = Join-Path $packRoot 'validation'
    $licenseRelativeRoot = "licenses/models/$packId"
    $checkpointRelativePath = $Pack.expectedRelativePath
    $checkpointDownloadPath = Join-Path $downloadsRoot $Pack.checkpointFile
    $zipFileName = "v3dfy-modelpack-$packId-v$PackVersion.zip"
    $zipPath = Join-Path $packRoot $zipFileName
    $builtThisRun = $false
    $skipReason = ''

    New-Item -ItemType Directory -Force -Path $downloadsRoot, $stagingRoot, $validationRoot | Out-Null

    if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
        Write-Step WARN "Final ZIP already exists; validating without rebuilding: $zipPath"
        $skipReason = 'existing zip left intact'
    }
    else {
        Save-Url $Pack.checkpointUrl $checkpointDownloadPath
        Save-Url $Pack.modelCardRawUrl (Join-Path $downloadsRoot 'OFFICIAL_MODEL_CARD.md')
        Save-Url $Pack.repositoryReadmeUrl (Join-Path $downloadsRoot 'OFFICIAL_REPOSITORY_README.md')
        Save-Url $Pack.licenseUrl (Join-Path $downloadsRoot $Pack.licenseFileName)

        $checkpointStagingPath = Join-Path $stagingRoot ($checkpointRelativePath -replace '/', '\')
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $checkpointStagingPath) | Out-Null
        Copy-Item -LiteralPath $checkpointDownloadPath -Destination $checkpointStagingPath -Force

        $licenseRelativePath = "$licenseRelativeRoot/$($Pack.licenseFileName)"
        $licenseStagingPath = Join-Path $stagingRoot ($licenseRelativePath -replace '/', '\')
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $licenseStagingPath) | Out-Null
        Copy-Item -LiteralPath (Join-Path $downloadsRoot $Pack.licenseFileName) -Destination $licenseStagingPath -Force

        $checkpointHash = Get-FileSha256 $checkpointStagingPath
        $checkpointSize = (Get-Item -LiteralPath $checkpointStagingPath).Length
        $retrievedUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        $renameNote = if ($Pack.renamedForIw3) {
            "Yes. The upstream file '$($Pack.upstreamFileName)' is packaged as '$($Pack.checkpointFile)' because iw3 expects that filename."
        }
        else {
            "No. The upstream filename already matches the iw3 expected filename."
        }
        $shortNames = [string]::Join(', ', $Pack.iw3DepthModelNames)
        $mappingKeys = [string]::Join(', ', $Pack.mappingKeys)

        Write-Utf8File (Join-Path $stagingRoot "$licenseRelativeRoot/MODEL_CARD.md") @"
# $($Pack.displayName) Model Card Summary

This file is a concise v3dfy model-pack summary of the official upstream model card.

Official model card: $($Pack.modelCardUrl)
Official raw model card retrieved: $($Pack.modelCardRawUrl)
Official repository: $($Pack.repositoryUrl)
Official repository README retrieved: $($Pack.repositoryReadmeUrl)
Retrieval time UTC: $retrievedUtc

## Model

- Name: $($Pack.displayName)
- Family: $($Pack.family)
- v3dfy pack id: $packId
- v3dfy mapping key(s): $mappingKeys
- iw3 short name(s): $shortNames
- v3dfy expected checkpoint path: $checkpointRelativePath
- Checkpoint filename packaged for iw3: $($Pack.checkpointFile)
- Upstream checkpoint filename: $($Pack.upstreamFileName)

## License

The local P3C-2B audit classifies this checkpoint as SAFE_WITH_NOTICE. This pack includes the upstream license text, source record, model-card summary, citation file, and notice.

## Practical Use In v3dfy

$($Pack.purpose)
"@

        Write-Utf8File (Join-Path $stagingRoot "$licenseRelativeRoot/SOURCE.txt") @"
$($Pack.displayName) v3dfy model pack source record

Official checkpoint URL:
$($Pack.checkpointUrl)

Official model card URL:
$($Pack.modelCardUrl)

Official raw model card URL:
$($Pack.modelCardRawUrl)

Official upstream repository URL:
$($Pack.repositoryUrl)

Official upstream repository README URL:
$($Pack.repositoryReadmeUrl)

Official upstream license URL:
$($Pack.licenseUrl)

Exact upstream filename:
$($Pack.upstreamFileName)

Exact iw3 expected filename:
$($Pack.checkpointFile)

Model-pack relative checkpoint path under iw3 pretrained_models:
$checkpointRelativePath

Expected installed path inside v3dfy:
engine/iw3/nunif/iw3/pretrained_models/$checkpointRelativePath

Retrieval time UTC:
$retrievedUtc

Checkpoint SHA256:
$checkpointHash

Checkpoint size in bytes:
$checkpointSize

License conclusion:
SAFE_WITH_NOTICE

Renamed for iw3 compatibility:
$renameNote

Scope note:
This pack contains only $($Pack.displayName). It does not contain gated, blocked, or non-commercial model checkpoints.
"@

        Write-Utf8File (Join-Path $stagingRoot "$licenseRelativeRoot/CITATION.txt") (Get-PackCitation $Pack.citationKind)

        Write-Utf8File (Join-Path $stagingRoot "$licenseRelativeRoot/NOTICE.txt") @"
This v3dfy model pack includes the $($Pack.displayName) checkpoint from the named official upstream source.

v3dfy is not affiliated with or endorsed by the upstream authors or organizations.

This pack is intended for local/offline v3dfy 2D to 3D conversion.

Required license/model-card/source/citation files are included in this pack.

This pack contains only the checkpoint named in SOURCE.txt and does not include gated, blocked, or non-commercial model checkpoints.
"@

        $fileEntries = @(
            New-ManifestFileEntry $stagingRoot $checkpointRelativePath 'checkpoint'
            New-ManifestFileEntry $stagingRoot $licenseRelativePath 'license'
            New-ManifestFileEntry $stagingRoot "$licenseRelativeRoot/MODEL_CARD.md" 'model-card'
            New-ManifestFileEntry $stagingRoot "$licenseRelativeRoot/SOURCE.txt" 'source'
            New-ManifestFileEntry $stagingRoot "$licenseRelativeRoot/CITATION.txt" 'citation'
            New-ManifestFileEntry $stagingRoot "$licenseRelativeRoot/NOTICE.txt" 'notice'
        )
        $checkpointEntry = $fileEntries | Where-Object { $_.path -eq $checkpointRelativePath } | Select-Object -First 1

        $models = @()
        for ($index = 0; $index -lt $Pack.mappingKeys.Count; $index++) {
            $models += [ordered]@{
                mappingKey = $Pack.mappingKeys[$index]
                iw3DepthModelName = $Pack.iw3DepthModelNames[$index]
                file = $checkpointRelativePath
                sha256 = $checkpointEntry.sha256
                sizeBytes = $checkpointEntry.sizeBytes
                category = $Pack.category
            }
        }

        $manifest = [ordered]@{
            schemaVersion = 1
            packId = $packId
            packVersion = $PackVersion
            displayName = $Pack.displayName
            targetRoot = 'iw3-pretrained-models'
            compatibleIw3Versions = @($CurrentIw3Version)
            minV3dfyVersion = $MinV3dfyVersion
            models = $models
            files = $fileEntries
            licenses = @(
                $licenseRelativePath,
                "$licenseRelativeRoot/MODEL_CARD.md",
                "$licenseRelativeRoot/SOURCE.txt",
                "$licenseRelativeRoot/CITATION.txt",
                "$licenseRelativeRoot/NOTICE.txt"
            )
        }
        $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $stagingRoot 'MODEL_PACK.json') -Encoding UTF8

        $expectedEntries = @('MODEL_PACK.json') + @($fileEntries | ForEach-Object { $_.path })
        Add-Type -AssemblyName System.IO.Compression | Out-Null
        Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
        $archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($entry in $expectedEntries) {
                $sourcePath = Join-Path $stagingRoot ($entry -replace '/', '\')
                Add-ZipEntry $archive $sourcePath $entry
            }
        }
        finally {
            $archive.Dispose()
        }
        $builtThisRun = $true
    }

    $manifest = Read-ZipManifest $zipPath
    $expectedZipEntries = @('MODEL_PACK.json') + @($manifest.files | ForEach-Object { $_.path })
    $zipValidation = Test-ZipContents $zipPath $expectedZipEntries
    $zipValidation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $validationRoot 'zip-validation-result.json') -Encoding UTF8
    if (-not $zipValidation.success) {
        throw "ZIP validation failed for $packId"
    }

    $runId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + ([Guid]::NewGuid().ToString('N').Substring(0, 8))
    $runRoot = Join-Path $validationRoot $runId
    $targetRoot = Join-Path $runRoot 'app\engine\iw3\nunif\iw3\pretrained_models'
    $stagingInstallRoot = Join-Path $runRoot 'staging'
    $installResultPath = Join-Path $runRoot 'install-result.json'
    New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

    & dotnet $setupHelperDll model-pack install `
        --zip $zipPath `
        --target-root $targetRoot `
        --staging-root $stagingInstallRoot `
        --current-iw3-version $CurrentIw3Version `
        --current-v3dfy-version $MinV3dfyVersion `
        --result $installResultPath
    if ($LASTEXITCODE -ne 0) {
        throw "SetupHelper import validation failed for $packId with exit code $LASTEXITCODE"
    }

    $installResult = Get-Content -LiteralPath $installResultPath -Raw | ConvertFrom-Json
    if (-not $installResult.success) {
        throw "SetupHelper import validation failed for $packId`: $($installResult.errors -join '; ')"
    }

    $validatorProject = Ensure-InventoryValidator
    $inventoryResultPath = Join-Path $runRoot 'inventory-validation-result.json'
    $mappingKeysArg = [string]::Join('|', $Pack.mappingKeys)
    $depthNamesArg = [string]::Join('|', $Pack.iw3DepthModelNames)
    $inventoryOutput = & dotnet run --project $validatorProject -- $targetRoot $Pack.expectedRelativePath $mappingKeysArg $depthNamesArg
    if ($LASTEXITCODE -ne 0) {
        $inventoryOutput | Set-Content -LiteralPath $inventoryResultPath -Encoding UTF8
        throw "Inventory validation failed for $packId"
    }

    $inventoryOutput | Set-Content -LiteralPath $inventoryResultPath -Encoding UTF8
    $inventoryResult = $inventoryOutput | ConvertFrom-Json
    if (-not $inventoryResult.success) {
        throw "Inventory validation failed for $packId`: $($inventoryResult.errors -join '; ')"
    }

    $zipHash = Get-FileSha256 $zipPath
    $zipSize = (Get-Item -LiteralPath $zipPath).Length
    $checkpointFileEntry = $manifest.files | Where-Object { $_.role -eq 'checkpoint' } | Select-Object -First 1
    $checksumLines = @()
    $checksumLines += "$zipHash  $zipFileName"
    foreach ($file in $manifest.files) {
        $checksumLines += "$($file.sha256)  staging/$($file.path)"
    }
    $checksumLines += "$(Get-FileSha256 (Join-Path $stagingRoot 'MODEL_PACK.json'))  staging/MODEL_PACK.json"
    $checksumLines | Set-Content -LiteralPath (Join-Path $packRoot 'SHA256SUMS.txt') -Encoding ASCII

    $reportPath = Join-Path $packRoot 'PACK_BUILD_REPORT.md'
    $modelRows = @($manifest.models | ForEach-Object {
        "- ``{0}`` / ``{1}``" -f $_.mappingKey, $_.iw3DepthModelName
    }) -join [Environment]::NewLine
    $entryRows = @($expectedZipEntries | Sort-Object | ForEach-Object { "- $_" }) -join [Environment]::NewLine
    $renamedText = if ($Pack.renamedForIw3) { 'yes' } else { 'no' }

    Write-Utf8File $reportPath @"
# $($Pack.displayName) Model Pack Build Report

- Build time UTC: $((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))
- Pack id: $packId
- Pack version: $($manifest.packVersion)
- Display name: $($manifest.displayName)
- iw3 short name(s): $([string]::Join(', ', $Pack.iw3DepthModelNames))
- Mapping key(s): $([string]::Join(', ', $Pack.mappingKeys))
- Checkpoint source: $($Pack.checkpointUrl)
- Model card: $($Pack.modelCardUrl)
- Upstream repository: $($Pack.repositoryUrl)
- License conclusion: SAFE_WITH_NOTICE
- Checkpoint relative path: $($checkpointFileEntry.path)
- Upstream filename: $($Pack.upstreamFileName)
- Renamed for iw3: $renamedText
- Checkpoint size bytes: $($checkpointFileEntry.sizeBytes)
- Checkpoint SHA256: $($checkpointFileEntry.sha256)
- ZIP path: $zipPath
- ZIP size bytes: $zipSize
- ZIP SHA256: $zipHash
- Built this run: $builtThisRun
- Skip reason: $skipReason

## Manifest Models

$modelRows

## ZIP Contents

$entryRows

## Validation

- ZIP content validation: $($zipValidation.success)
- SetupHelper import validation: $($installResult.success)
- Installed file count: $($installResult.installedFiles.Count)
- Import warnings: $($installResult.warnings.Count)
- Import errors: $($installResult.errors.Count)
- Inventory validation: $($inventoryResult.success)
- Compatible model count after temp import: $($inventoryResult.compatibleModelCount)
- Selectable model count after temp import: $($inventoryResult.selectableCount)
- Unmapped model count after temp import: $($inventoryResult.unmappedCount)

Validation run folder:

- validation/$runId/
"@

    [ordered]@{
        packId = $packId
        displayName = $Pack.displayName
        iw3DepthModelNames = $Pack.iw3DepthModelNames
        mappingKeys = $Pack.mappingKeys
        checkpointSourceUrl = $Pack.checkpointUrl
        checkpointPath = $checkpointFileEntry.path
        checkpointSizeBytes = [int64]$checkpointFileEntry.sizeBytes
        checkpointSha256 = $checkpointFileEntry.sha256
        zipPath = $zipPath
        zipSizeBytes = $zipSize
        zipSha256 = $zipHash
        validationResult = 'passed'
        builtThisRun = $builtThisRun
        skippedReason = $skipReason
    }
}

function Update-AggregateIndex {
    $packRows = New-Object System.Collections.Generic.List[object]
    $checksumLines = New-Object System.Collections.Generic.List[string]

    Get-ChildItem -LiteralPath $artifactRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne '_validation' } |
        Sort-Object Name |
        ForEach-Object {
            $zip = Get-ChildItem -LiteralPath $_.FullName -Filter 'v3dfy-modelpack-*.zip' -File -ErrorAction SilentlyContinue |
                Sort-Object Name |
                Select-Object -First 1
            if ($null -eq $zip) {
                return
            }

            $manifest = Read-ZipManifest $zip.FullName
            $zipHash = Get-FileSha256 $zip.FullName
            $zipSize = $zip.Length
            $checkpoint = $manifest.files | Where-Object { $_.role -eq 'checkpoint' } | Select-Object -First 1
            $shortNames = [string]::Join(', ', @($manifest.models | ForEach-Object { $_.iw3DepthModelName }))
            $mappingKeys = [string]::Join(', ', @($manifest.models | ForEach-Object { $_.mappingKey }))
            $relativeZip = ConvertTo-ForwardSlashPath (Join-Path $_.Name $zip.Name)

            $packRows.Add([pscustomobject][ordered]@{
                packId = $manifest.packId
                displayName = $manifest.displayName
                iw3ShortNames = $shortNames
                mappingKeys = $mappingKeys
                checkpointPath = $checkpoint.path
                checkpointSizeBytes = $checkpoint.sizeBytes
                checkpointSha256 = $checkpoint.sha256
                zipPath = $relativeZip
                zipSizeBytes = $zipSize
                zipSha256 = $zipHash
            })
            $checksumLines.Add("$zipHash  $relativeZip")
        }

    $indexPath = Join-Path $artifactRoot 'MODEL_PACK_INDEX.md'
    $checksumsPath = Join-Path $artifactRoot 'SHA256SUMS.model-packs.txt'
    $tableRows = @($packRows | ForEach-Object {
        "| ``$($_.packId)`` | $($_.displayName) | ``$($_.iw3ShortNames)`` | ``$($_.checkpointPath)`` | $($_.checkpointSizeBytes) | ``$($_.checkpointSha256)`` | ``$($_.zipPath)`` | $($_.zipSizeBytes) | ``$($_.zipSha256)`` |"
    }) -join [Environment]::NewLine

    Write-Utf8File $indexPath @"
# v3dfy Model Pack Index

Generated UTC: $((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))

This index covers real model-pack ZIP artifacts present under artifacts/model-packs.
It does not publish or upload release assets.

| pack id | display name | iw3 short name(s) | checkpoint path | checkpoint bytes | checkpoint SHA256 | ZIP path | ZIP bytes | ZIP SHA256 |
| --- | --- | --- | --- | ---: | --- | --- | ---: | --- |
$tableRows
"@

    $checksumLines | Set-Content -LiteralPath $checksumsPath -Encoding ASCII

    [ordered]@{
        indexPath = $indexPath
        checksumsPath = $checksumsPath
        packCount = $packRows.Count
    }
}

if (-not (Test-Path -LiteralPath $setupHelperDll -PathType Leaf)) {
    throw "SetupHelper DLL was not found. Run dotnet build --no-restore first: $setupHelperDll"
}

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$results = New-Object System.Collections.Generic.List[object]
$skipped = New-Object System.Collections.Generic.List[object]

foreach ($pack in $packs) {
    Write-Step INFO "Processing pack: $($pack.packId)"
    try {
        $result = Build-ModelPack $pack
        $results.Add($result)
        Write-Step OK "Validated pack: $($pack.packId)"
    }
    catch {
        $skipped.Add([ordered]@{
            packId = $pack.packId
            displayName = $pack.displayName
            reason = $_.Exception.Message
        })
        Write-Step FAIL "Skipped pack $($pack.packId): $($_.Exception.Message)"
    }
}

$aggregate = Update-AggregateIndex

$summary = [ordered]@{
    generatedOrValidated = $results
    skipped = $skipped
    aggregate = $aggregate
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $artifactRoot 'MODEL_PACK_BUILD_SUMMARY.json') -Encoding UTF8

Write-Step OK "Aggregate index: $($aggregate.indexPath)"
Write-Step OK "Aggregate checksums: $($aggregate.checksumsPath)"

if ($skipped.Count -gt 0) {
    Write-Step WARN "$($skipped.Count) pack(s) skipped. See MODEL_PACK_BUILD_SUMMARY.json."
    exit 1
}
