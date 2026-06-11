using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using V3dfy.Core.Models;
using V3dfy.Infrastructure.ModelPacks;

namespace V3dfy.Tests.Infrastructure;

public sealed class ModelPackInstallPlannerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string root = TestPaths.TempRoot(
        "model-pack-install-planner",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ValidModelPackManifestJson_Parses()
    {
        var manifest = CreateValidManifest();

        var parsed = JsonSerializer.Deserialize<ModelPackManifest>(
            JsonSerializer.Serialize(manifest, JsonOptions),
            JsonOptions);

        Assert.NotNull(parsed);
        Assert.Equal(1, parsed.SchemaVersion);
        Assert.Equal("depth-anything-metric-outdoor", parsed.PackId);
        Assert.Equal("ZoeD_Any_K", Assert.Single(parsed.Models).Iw3DepthModelName);
        Assert.Equal(2, parsed.Files.Count);
    }

    [Fact]
    public async Task ValidZip_ProducesDryRunInstallPlan()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();

        var plan = await CreatePlanAsync(zipPath, targetRoot);

        Assert.True(plan.IsValid);
        Assert.Empty(plan.ValidationErrors);
        Assert.NotNull(plan.Manifest);
        Assert.Equal("depth-anything-metric-outdoor", plan.Manifest.PackId);
        Assert.Equal(2, plan.FilesToInstall.Count);
        Assert.Empty(plan.AlreadyInstalledFiles);
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public async Task DryRun_AcceptsSafeDirectoryEntriesWithoutManifestDeclarations()
    {
        var zipPath = CreateModelPackZip(directoryEntries: SafeDirectoryEntries);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.True(plan.IsValid);
        Assert.Empty(plan.ValidationErrors);
        Assert.Equal(2, plan.FilesToInstall.Count);
        Assert.Contains(
            plan.FilesToInstall,
            file => file.RelativePath == "hub/checkpoints/depth_anything_metric_depth_outdoor.pt");
        Assert.Contains(
            plan.FilesToInstall,
            file => file.RelativePath == "licenses/models/depth-anything-outdoor/LICENSE.txt");
        Assert.DoesNotContain(
            plan.ValidationErrors,
            error => error.Contains("not declared", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            plan.ValidationErrors,
            error => error.Contains("directory separator", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("hub/../", "traversal")]
    [InlineData("/hub/", "Absolute")]
    [InlineData("C:hub/", "rooted")]
    [InlineData("//server/share/", "UNC")]
    public async Task DryRun_RejectsUnsafeDirectoryEntries(
        string directoryEntry,
        string expectedErrorFragment)
    {
        var zipPath = CreateModelPackZip(directoryEntries: [directoryEntry]);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.ValidationErrors,
            error =>
                error.Contains("ZIP entry", StringComparison.OrdinalIgnoreCase) &&
                error.Contains(expectedErrorFragment, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DryRunPlan_ResolvesPathsUnderTargetPretrainedModelsRoot()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();

        var plan = await CreatePlanAsync(zipPath, targetRoot);

        Assert.True(plan.IsValid);
        Assert.All(
            plan.FilesToInstall,
            file => Assert.StartsWith(
                Path.GetFullPath(targetRoot),
                file.DestinationPath,
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            plan.FilesToInstall,
            file => file.DestinationPath == Path.Combine(
                targetRoot,
                "hub",
                "checkpoints",
                "depth_anything_metric_depth_outdoor.pt"));
    }

    [Fact]
    public async Task ManifestFile_WithRootedPath_IsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.Files[0].Path = @"C:\unsafe\model.pt";
        manifest.Models[0].File = manifest.Files[0].Path;
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(plan.ValidationErrors, error => error.Contains("rooted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ManifestFile_WithUncPath_IsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.Files[0].Path = @"\\server\share\model.pt";
        manifest.Models[0].File = manifest.Files[0].Path;
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(plan.ValidationErrors, error => error.Contains("UNC", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ManifestFile_WithTraversalPath_IsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.Files[0].Path = "hub/../model.pt";
        manifest.Models[0].File = manifest.Files[0].Path;
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(plan.ValidationErrors, error => error.Contains("traversal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ZipEntryNotDeclaredInManifest_IsRejected()
    {
        var zipPath = CreateModelPackZip(extraEntries:
        [
            new PackEntry("hub/checkpoints/extra.pt", "extra"u8.ToArray()),
        ]);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.ValidationErrors,
            error => error.Contains("not declared", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeclaredManifestFileMissingFromZip_IsRejected()
    {
        var zipPath = CreateModelPackZip(omitEntryPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
        });

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.ValidationErrors,
            error => error.Contains("missing from ZIP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sha256Mismatch_IsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.Files[0].Sha256 = new string('0', 64);
        manifest.Models[0].Sha256 = manifest.Files[0].Sha256;
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.ValidationErrors,
            error => error.Contains("SHA256 mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SizeMismatch_IsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.Files[0].SizeBytes += 1;
        manifest.Models[0].SizeBytes = manifest.Files[0].SizeBytes;
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.ValidationErrors,
            error => error.Contains("Size mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExistingTargetWithSameHash_IsReportedAsAlreadyInstalled()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        WriteTargetFile(
            targetRoot,
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            OutdoorCheckpointBytes);

        var plan = await CreatePlanAsync(zipPath, targetRoot);

        Assert.True(plan.IsValid);
        var installed = Assert.Single(plan.AlreadyInstalledFiles);
        Assert.Equal("hub/checkpoints/depth_anything_metric_depth_outdoor.pt", installed.RelativePath);
        Assert.DoesNotContain(
            plan.FilesToInstall,
            file => file.RelativePath == installed.RelativePath);
    }

    [Fact]
    public async Task ExistingTargetWithDifferentHash_IsConflictAndFailure()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        WriteTargetFile(
            targetRoot,
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            "different checkpoint"u8.ToArray());

        var plan = await CreatePlanAsync(zipPath, targetRoot);

        Assert.False(plan.IsValid);
        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal("hub/checkpoints/depth_anything_metric_depth_outdoor.pt", conflict.RelativePath);
    }

    [Fact]
    public async Task ProtectedIw3RowFlowRuntimeDependency_IsRejected()
    {
        var manifest = CreateValidManifest(
            checkpointPath: "hub/checkpoints/iw3_row_flow_v3_20250627.pth",
            checkpointBytes: "row-flow"u8.ToArray());
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.ValidationErrors,
            error => error.Contains("protected iw3 runtime dependency", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnsupportedSchemaVersion_IsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.SchemaVersion = 2;
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(plan.ValidationErrors, error => error.Contains("schemaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WrongTargetRoot_IsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.TargetRoot = "pretrained_models";
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(plan.ValidationErrors, error => error.Contains("targetRoot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IncompatibleIw3Version_IsRejected()
    {
        var manifest = CreateValidManifest();
        manifest.CompatibleIw3Versions = ["other-iw3-version"];
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(
            zipPath,
            TargetRoot(),
            currentIw3Version: "nunif-d23721f1");

        Assert.False(plan.IsValid);
        Assert.Contains(plan.ValidationErrors, error => error.Contains("not compatible", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingRequiredFields_AreRejected()
    {
        var manifest = CreateValidManifest();
        manifest.PackId = string.Empty;
        manifest.DisplayName = string.Empty;
        manifest.MinV3dfyVersion = string.Empty;
        var zipPath = CreateModelPackZip(manifest);

        var plan = await CreatePlanAsync(zipPath, TargetRoot());

        Assert.False(plan.IsValid);
        Assert.Contains(plan.ValidationErrors, error => error.Contains("packId", StringComparison.Ordinal));
        Assert.Contains(plan.ValidationErrors, error => error.Contains("displayName", StringComparison.Ordinal));
        Assert.Contains(plan.ValidationErrors, error => error.Contains("minV3dfyVersion", StringComparison.Ordinal));
    }

    private async Task<ModelPackDryRunInstallPlan> CreatePlanAsync(
        string zipPath,
        string targetRoot,
        string? currentIw3Version = "nunif-d23721f1") =>
        await new ModelPackInstallPlanner().CreateDryRunPlanAsync(new ModelPackDryRunInstallRequest(
            zipPath,
            targetRoot,
            currentIw3Version));

    private string CreateModelPackZip(
        MutableModelPackManifest? manifest = null,
        IReadOnlyList<PackEntry>? extraEntries = null,
        IReadOnlySet<string>? omitEntryPaths = null,
        IReadOnlyList<string>? directoryEntries = null)
    {
        manifest ??= CreateValidManifest();
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        WriteZipEntry(
            archive,
            ModelPackManifest.FileName,
            JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));

        foreach (var directoryEntry in directoryEntries ?? [])
        {
            WriteZipDirectoryEntry(archive, directoryEntry);
        }

        var fileBytes = CreateFileBytes(manifest);
        foreach (var (relativePath, bytes) in fileBytes)
        {
            if (omitEntryPaths?.Contains(relativePath) == true)
            {
                continue;
            }

            WriteZipEntry(archive, relativePath, bytes);
        }

        foreach (var extraEntry in extraEntries ?? [])
        {
            WriteZipEntry(archive, extraEntry.Path, extraEntry.Bytes);
        }

        return zipPath;
    }

    private static Dictionary<string, byte[]> CreateFileBytes(MutableModelPackManifest manifest)
    {
        var bytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            bytes[file.Path] = file.Role switch
            {
                "license" => LicenseBytes,
                _ => OutdoorCheckpointBytes,
            };
        }

        return bytes;
    }

    private static void WriteZipEntry(ZipArchive archive, string relativePath, byte[] bytes)
    {
        var entry = archive.CreateEntry(relativePath.Replace('\\', '/'), CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static void WriteZipDirectoryEntry(ZipArchive archive, string relativePath)
    {
        archive.CreateEntry(relativePath, CompressionLevel.NoCompression);
    }

    private string TargetRoot() =>
        Path.Combine(root, "app", "engine", "iw3", "nunif", "iw3", "pretrained_models");

    private static void WriteTargetFile(string targetRoot, string relativePath, byte[] bytes)
    {
        var path = Path.Combine([targetRoot, .. relativePath.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static MutableModelPackManifest CreateValidManifest(
        string checkpointPath = "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
        byte[]? checkpointBytes = null)
    {
        checkpointBytes ??= OutdoorCheckpointBytes;
        return new MutableModelPackManifest
        {
            SchemaVersion = 1,
            PackId = "depth-anything-metric-outdoor",
            PackVersion = "1.0.0",
            DisplayName = "Depth Anything Metric Outdoor",
            TargetRoot = ModelPackManifest.ExpectedTargetRoot,
            CompatibleIw3Versions = ["nunif-d23721f1"],
            MinV3dfyVersion = "0.1.0-preview.1",
            Models =
            [
                new()
                {
                    MappingKey = "depth-anything-metric-outdoor",
                    Iw3DepthModelName = "ZoeD_Any_K",
                    File = checkpointPath,
                    Sha256 = Sha256(checkpointBytes),
                    SizeBytes = checkpointBytes.Length,
                    Category = "outdoor",
                },
            ],
            Files =
            [
                new()
                {
                    Path = checkpointPath,
                    Sha256 = Sha256(checkpointBytes),
                    SizeBytes = checkpointBytes.Length,
                    Role = "checkpoint",
                },
                new()
                {
                    Path = "licenses/models/depth-anything-outdoor/LICENSE.txt",
                    Sha256 = Sha256(LicenseBytes),
                    SizeBytes = LicenseBytes.Length,
                    Role = "license",
                },
            ],
            Licenses = ["licenses/models/depth-anything-outdoor/LICENSE.txt"],
        };
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static readonly byte[] OutdoorCheckpointBytes =
        "synthetic outdoor checkpoint"u8.ToArray();

    private static readonly byte[] LicenseBytes =
        "synthetic license"u8.ToArray();

    private static readonly string[] SafeDirectoryEntries =
    [
        "hub/",
        "hub/checkpoints/",
        "licenses/",
        "licenses/models/",
        "licenses\\models\\depth-anything-outdoor\\",
    ];

    private sealed record PackEntry(string Path, byte[] Bytes);

    private sealed class MutableModelPackManifest
    {
        public int SchemaVersion { get; set; }

        public string PackId { get; set; } = string.Empty;

        public string PackVersion { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string TargetRoot { get; set; } = string.Empty;

        public List<string> CompatibleIw3Versions { get; set; } = [];

        public string MinV3dfyVersion { get; set; } = string.Empty;

        public List<MutableModelPackModel> Models { get; set; } = [];

        public List<MutableModelPackFile> Files { get; set; } = [];

        public List<string> Licenses { get; set; } = [];
    }

    private sealed class MutableModelPackModel
    {
        public string MappingKey { get; set; } = string.Empty;

        public string Iw3DepthModelName { get; set; } = string.Empty;

        public string File { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public string Category { get; set; } = string.Empty;
    }

    private sealed class MutableModelPackFile
    {
        public string Path { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public string Role { get; set; } = string.Empty;
    }
}
