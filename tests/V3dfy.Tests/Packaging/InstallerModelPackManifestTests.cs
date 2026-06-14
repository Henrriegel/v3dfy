using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace V3dfy.Tests.Packaging;

public sealed class InstallerModelPackManifestTests : IDisposable
{
    private const string ModelPackVersion = "0.1.0-preview.1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string root = TestPaths.TempRoot(
        "installer-model-pack-manifest",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ManifestGeneratorScript_DefinesInstallerContractAndApprovedPacks()
    {
        var script = ReadRepoFile("scripts", "build-installer-model-pack-manifest.ps1");

        Assert.Contains("schemaVersion = 1", script);
        Assert.Contains("v3dfyVersion", script);
        Assert.Contains("modelPackVersion", script);
        Assert.Contains("releaseTag", script);
        Assert.Contains("modelPackReleaseBaseUrl", script);
        Assert.Contains("currentIw3Version", script);
        Assert.Contains("generatedUtc", script);
        Assert.Contains("packs = @($packs.ToArray())", script);

        foreach (var pack in ExpectedPacks)
        {
            Assert.Contains($"-PackId '{pack.PackId}'", script);
        }

        Assert.Contains("bestUseEnglish", script);
        Assert.Contains("bestUseSpanish", script);
        Assert.Contains("assetFileName", script);
        Assert.Contains("relativeArtifactPath", script);
        Assert.Contains("zipSha256", script);
        Assert.Contains("checkpointSha256", script);
        Assert.Contains("installerSelectable", script);
        Assert.Contains("defaultSelected", script);
        Assert.Contains("sourceUrl", script);
        Assert.Contains("modelCardUrl", script);
        Assert.Contains("recommendedFor", script);
        Assert.Contains("sizeCategory", script);

        Assert.Contains("'depth-pro'", script);
        Assert.Contains("'video-depth-anything-small'", script);
        Assert.Contains("'metric-video-depth-anything-small'", script);
        Assert.DoesNotContain("Invoke-WebRequest", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Start-BitsTransfer", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestGenerator_CreatesInstallerJsonFromExistingArtifacts()
    {
        var artifactRoot = Path.Combine(root, "model-packs");
        var outputPath = Path.Combine(root, "v3dfy-model-packs-v0.1.0-preview.1.json");
        CreateSyntheticArtifacts(artifactRoot);

        var result = RunManifestScript(
            artifactRoot,
            outputPath,
            releaseTag: "vtest",
            releaseBaseUrl: "https://example.invalid/releases/download/vtest");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(outputPath), result.Output);

        var json = File.ReadAllText(outputPath);
        using var document = JsonDocument.Parse(json);
        var rootElement = document.RootElement;

        Assert.Equal(1, rootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("0.1.0-preview.1", rootElement.GetProperty("v3dfyVersion").GetString());
        Assert.Equal(ModelPackVersion, rootElement.GetProperty("modelPackVersion").GetString());
        Assert.Equal("vtest", rootElement.GetProperty("releaseTag").GetString());
        Assert.Equal(
            "https://example.invalid/releases/download/vtest",
            rootElement.GetProperty("modelPackReleaseBaseUrl").GetString());
        Assert.Equal("nunif-d23721f1", rootElement.GetProperty("currentIw3Version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(rootElement.GetProperty("generatedUtc").GetString()));

        var packs = rootElement.GetProperty("packs").EnumerateArray().ToArray();
        Assert.Equal(12, packs.Length);
        Assert.Equal(ExpectedPacks.Select(static pack => pack.PackId), packs.Select(static pack => pack.GetProperty("packId").GetString()));

        var seenAssetFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pack in packs)
        {
            var packId = pack.GetProperty("packId").GetString();
            var expected = ExpectedPacks.Single(expected => expected.PackId == packId);
            var assetFileName = RequireString(pack, "assetFileName");
            var relativeArtifactPath = RequireString(pack, "relativeArtifactPath");
            var url = RequireString(pack, "url");

            Assert.False(Path.IsPathRooted(assetFileName));
            Assert.False(assetFileName.Contains('/') || assetFileName.Contains('\\'));
            Assert.False(Path.IsPathRooted(relativeArtifactPath));
            Assert.DoesNotContain('\\', relativeArtifactPath);
            Assert.EndsWith(assetFileName, relativeArtifactPath, StringComparison.Ordinal);
            Assert.StartsWith("https://example.invalid/releases/download/vtest/", url, StringComparison.Ordinal);
            Assert.EndsWith(assetFileName, url, StringComparison.Ordinal);
            Assert.True(seenAssetFileNames.Add(assetFileName));

            Assert.False(pack.GetProperty("defaultSelected").GetBoolean());
            Assert.True(pack.GetProperty("installerSelectable").GetBoolean());
            Assert.Equal("SAFE_WITH_NOTICE / Apache-2.0", RequireString(pack, "license"));
            Assert.False(string.IsNullOrWhiteSpace(RequireString(pack, "bestUseEnglish")));
            Assert.False(string.IsNullOrWhiteSpace(RequireString(pack, "bestUseSpanish")));
            Assert.False(string.IsNullOrWhiteSpace(RequireString(pack, "displayName")));
            Assert.False(string.IsNullOrWhiteSpace(RequireString(pack, "sourceUrl")));
            Assert.False(string.IsNullOrWhiteSpace(RequireString(pack, "modelCardUrl")));
            Assert.False(string.IsNullOrWhiteSpace(RequireString(pack, "checkpointPath")));
            Assert.False(string.IsNullOrWhiteSpace(RequireString(pack, "checkpointSha256")));
            Assert.True(pack.GetProperty("checkpointSizeBytes").GetInt64() > 0);
            Assert.False(string.IsNullOrWhiteSpace(RequireString(pack, "zipSha256")));
            Assert.True(pack.GetProperty("zipSizeBytes").GetInt64() > 0);
            Assert.False(string.IsNullOrWhiteSpace(RequireString(pack, "sizeCategory")));
            Assert.NotEmpty(pack.GetProperty("recommendedFor").EnumerateArray());
            Assert.Equal(expected.Iw3DepthModelNames, pack.GetProperty("iw3DepthModelNames").EnumerateArray().Select(static value => value.GetString()));
            Assert.Equal(expected.MappingKeys, pack.GetProperty("mappingKeys").EnumerateArray().Select(static value => value.GetString()));
        }

        var packIds = packs.Select(static pack => pack.GetProperty("packId").GetString()).ToArray();
        Assert.DoesNotContain("depth-pro", packIds);
        Assert.DoesNotContain("video-depth-anything-small", packIds);
        Assert.DoesNotContain("metric-video-depth-anything-small", packIds);
        Assert.DoesNotContain("zoedepth-nyu", packIds);
        Assert.DoesNotContain(root, json, StringComparison.OrdinalIgnoreCase);
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName).GetString();
        Assert.False(string.IsNullOrWhiteSpace(value), propertyName);
        return value;
    }

    private static ScriptResult RunManifestScript(
        string artifactRoot,
        string outputPath,
        string releaseTag,
        string releaseBaseUrl)
    {
        var scriptPath = Path.Combine(RepositoryRoot(), "scripts", "build-installer-model-pack-manifest.ps1");
        var startInfo = new ProcessStartInfo("powershell")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-ArtifactRoot");
        startInfo.ArgumentList.Add(artifactRoot);
        startInfo.ArgumentList.Add("-OutputPath");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("-ReleaseTag");
        startInfo.ArgumentList.Add(releaseTag);
        startInfo.ArgumentList.Add("-ModelPackReleaseBaseUrl");
        startInfo.ArgumentList.Add(releaseBaseUrl);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ScriptResult(process.ExitCode, stdout + stderr);
    }

    private static void CreateSyntheticArtifacts(string artifactRoot)
    {
        Directory.CreateDirectory(artifactRoot);
        var checksumLines = new List<string>();
        var summaryEntries = new List<object>();

        foreach (var pack in ExpectedPacks)
        {
            var packRoot = Path.Combine(artifactRoot, pack.PackId);
            Directory.CreateDirectory(packRoot);

            var assetFileName = $"v3dfy-modelpack-{pack.PackId}-v{ModelPackVersion}.zip";
            var relativeArtifactPath = $"{pack.PackId}/{assetFileName}";
            var zipPath = Path.Combine(packRoot, assetFileName);
            var checkpointBytes = Encoding.UTF8.GetBytes($"{pack.PackId} synthetic checkpoint");
            var checkpointSha = Sha256(checkpointBytes);

            CreateSyntheticModelPackZip(zipPath, pack, checkpointBytes, checkpointSha);
            var zipBytes = File.ReadAllBytes(zipPath);
            var zipSha = Sha256(zipBytes);
            checksumLines.Add($"{zipSha}  {relativeArtifactPath}");

            File.WriteAllText(
                Path.Combine(packRoot, "PACK_BUILD_REPORT.md"),
                $"""
                # {pack.DisplayName} Model Pack Build Report

                - Pack id: {pack.PackId}
                - Pack version: {ModelPackVersion}
                - Display name: {pack.DisplayName}
                - Checkpoint source: https://example.invalid/checkpoints/{pack.PackId}.bin
                - Model card: https://example.invalid/models/{pack.PackId}
                - Upstream repository: https://example.invalid/repositories/{pack.PackId}
                - License conclusion: SAFE_WITH_NOTICE / Apache-2.0
                - Checkpoint relative path: {pack.CheckpointPath}
                - Checkpoint size bytes: {checkpointBytes.Length}
                - Checkpoint SHA256: {checkpointSha}
                - ZIP path: {zipPath}
                - ZIP size bytes: {zipBytes.Length}
                - ZIP SHA256: {zipSha}
                """);

            summaryEntries.Add(new
            {
                packId = pack.PackId,
                displayName = pack.DisplayName,
                iw3DepthModelNames = pack.Iw3DepthModelNames,
                mappingKeys = pack.MappingKeys,
                checkpointSourceUrl = $"https://example.invalid/checkpoints/{pack.PackId}.bin",
                checkpointPath = pack.CheckpointPath,
                checkpointSizeBytes = checkpointBytes.Length,
                checkpointSha256 = checkpointSha,
                zipPath,
                zipSizeBytes = zipBytes.Length,
                zipSha256 = zipSha,
                validationResult = "passed",
            });
        }

        File.WriteAllLines(
            Path.Combine(artifactRoot, "SHA256SUMS.model-packs.txt"),
            checksumLines);
        File.WriteAllText(
            Path.Combine(artifactRoot, "MODEL_PACK_BUILD_SUMMARY.json"),
            JsonSerializer.Serialize(new
            {
                generatedOrValidated = summaryEntries,
                skipped = Array.Empty<object>(),
            }, JsonOptions));
    }

    private static void CreateSyntheticModelPackZip(
        string zipPath,
        ExpectedPack pack,
        byte[] checkpointBytes,
        string checkpointSha)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        var licensePath = $"licenses/models/{pack.PackId}/LICENSE-Synthetic-Apache-2.0.txt";
        var licenseBytes = Encoding.UTF8.GetBytes($"{pack.PackId} synthetic license");
        var manifest = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["packId"] = pack.PackId,
            ["packVersion"] = ModelPackVersion,
            ["displayName"] = pack.DisplayName,
            ["targetRoot"] = "iw3-pretrained-models",
            ["compatibleIw3Versions"] = new[] { "nunif-d23721f1" },
            ["minV3dfyVersion"] = "0.1.0",
            ["models"] = pack.MappingKeys
                .Select((mappingKey, index) => new Dictionary<string, object?>
                {
                    ["mappingKey"] = mappingKey,
                    ["iw3DepthModelName"] = pack.Iw3DepthModelNames[index],
                    ["file"] = pack.CheckpointPath,
                    ["sha256"] = checkpointSha,
                    ["sizeBytes"] = checkpointBytes.Length,
                    ["category"] = "synthetic",
                })
                .ToArray(),
            ["files"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["path"] = pack.CheckpointPath,
                    ["sha256"] = checkpointSha,
                    ["sizeBytes"] = checkpointBytes.Length,
                    ["role"] = "checkpoint",
                },
                new Dictionary<string, object?>
                {
                    ["path"] = licensePath,
                    ["sha256"] = Sha256(licenseBytes),
                    ["sizeBytes"] = licenseBytes.Length,
                    ["role"] = "license",
                },
            },
            ["licenses"] = new[] { licensePath },
        };

        WriteZipEntry(
            archive,
            "MODEL_PACK.json",
            JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));
        WriteZipEntry(archive, pack.CheckpointPath, checkpointBytes);
        WriteZipEntry(archive, licensePath, licenseBytes);
    }

    private static void WriteZipEntry(ZipArchive archive, string path, byte[] bytes)
    {
        var entry = archive.CreateEntry(path.Replace('\\', '/'), CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string ReadRepoFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. segments]));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "v3dfy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed record ScriptResult(int ExitCode, string Output);

    private sealed record ExpectedPack(
        string PackId,
        string DisplayName,
        string CheckpointPath,
        string[] MappingKeys,
        string[] Iw3DepthModelNames);

    private static readonly ExpectedPack[] ExpectedPacks =
    [
        new(
            "depth-anything-v2-small",
            "Depth Anything V2 Small",
            "hub/checkpoints/depth_anything_v2_vits.pth",
            ["depth-anything-v2-small"],
            ["Any_V2_S"]),
        new(
            "depth-anything-small",
            "Depth Anything Small",
            "hub/checkpoints/depth_anything_vits14.pth",
            ["depth-anything-small"],
            ["Any_S"]),
        new(
            "distill-any-depth-small",
            "Distill Any Depth Small",
            "hub/checkpoints/distill_any_depth_vits.safetensors",
            ["distill-any-depth-small"],
            ["Distill_Any_S"]),
        new(
            "depth-anything-v2-metric-hypersim-small",
            "Depth Anything V2 Metric Hypersim Small",
            "hub/checkpoints/depth_anything_v2_metric_hypersim_vits.pth",
            ["depth-anything-v2-metric-hypersim-small"],
            ["Any_V2_N_S"]),
        new(
            "depth-anything-v2-metric-vkitti-small",
            "Depth Anything V2 Metric VKITTI Small",
            "hub/checkpoints/depth_anything_v2_metric_vkitti_vits.pth",
            ["depth-anything-v2-metric-vkitti-small"],
            ["Any_V2_K_S"]),
        new(
            "depth-anything-base",
            "Depth Anything Base",
            "hub/checkpoints/depth_anything_vitb14.pth",
            ["depth-anything-base"],
            ["Any_B"]),
        new(
            "depth-anything-v2-metric-hypersim-base",
            "Depth Anything V2 Metric Hypersim Base",
            "hub/checkpoints/depth_anything_v2_metric_hypersim_vitb.pth",
            ["depth-anything-v2-metric-hypersim-base"],
            ["Any_V2_N_B"]),
        new(
            "depth-anything-v2-metric-vkitti-base",
            "Depth Anything V2 Metric VKITTI Base",
            "hub/checkpoints/depth_anything_v2_metric_vkitti_vitb.pth",
            ["depth-anything-v2-metric-vkitti-base"],
            ["Any_V2_K_B"]),
        new(
            "depth-anything-3-mono-large",
            "Depth Anything 3 Mono Large",
            "hub/checkpoints/da3mono-large.safetensors",
            ["depth-anything-3-mono-large", "depth-anything-3-mono-large-3d-tv"],
            ["Any_V3_Mono", "Any_V3_Mono_01"]),
        new(
            "depth-anything-large",
            "Depth Anything Large",
            "hub/checkpoints/depth_anything_vitl14.pth",
            ["depth-anything-large"],
            ["Any_L"]),
        new(
            "depth-anything-metric-indoor",
            "Depth Anything Metric Indoor",
            "hub/checkpoints/depth_anything_metric_depth_indoor.pt",
            ["depth-anything-metric-indoor"],
            ["ZoeD_Any_N"]),
        new(
            "depth-anything-metric-outdoor",
            "Depth Anything Metric Outdoor",
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            ["depth-anything-metric-outdoor"],
            ["ZoeD_Any_K"]),
    ];
}
