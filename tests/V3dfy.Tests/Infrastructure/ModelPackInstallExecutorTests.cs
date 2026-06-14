using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using V3dfy.Core.Models;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Infrastructure.Health;
using V3dfy.Infrastructure.ModelPacks;

namespace V3dfy.Tests.Infrastructure;

public sealed class ModelPackInstallExecutorTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string root = TestPaths.TempRoot(
        "model-pack-install-executor",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidModelPack_InstallsFilesIntoTempPretrainedModelsRoot()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.InstalledFiles.Count);
        Assert.Empty(result.AlreadyInstalledFiles);
        Assert.Empty(result.SkippedFiles);
        AssertTargetFile(targetRoot, "hub/checkpoints/depth_anything_metric_depth_outdoor.pt", OutdoorCheckpointBytes);
        AssertTargetFile(targetRoot, "licenses/models/depth-anything-outdoor/LICENSE.txt", LicenseBytes);
        Assert.False(File.Exists(Path.Combine(targetRoot, ModelPackManifest.FileName)));
    }

    [Fact]
    public async Task ValidModelPack_InstalledMappedOptionalCheckpointIsSelectableAfterInventoryRefresh()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);
        var health = new InternalToolsHealthChecker().CheckDetailed(PathsForTargetRoot(targetRoot));

        Assert.True(result.Success);
        var selectable = Iw3DepthModelMapper.CreateSelectableCandidates(
            health.ModelInventory.SelectionCandidates,
            useSpanish: false);
        var candidate = Assert.Single(selectable);
        Assert.Equal(Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey, candidate.MappingKey);
        Assert.Equal(Iw3DepthModelMapper.ZoeDAnyKDepthModelName, candidate.Iw3DepthModelName);
        Assert.Equal(Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorRelativePath, candidate.RelativePath);
    }

    [Fact]
    public async Task ValidModelPack_InstalledSharedSafeCheckpointExposesExpectedVariantsAfterInventoryRefresh()
    {
        var manifest = CreateValidManifest(
            checkpointPath: Iw3DepthModelMapper.DepthAnything3MonoLargeRelativePath,
            mappingKey: Iw3DepthModelMapper.DepthAnything3MonoLargeKey,
            iw3DepthModelName: Iw3DepthModelMapper.AnyV3MonoDepthModelName,
            displayName: Iw3DepthModelMapper.DepthAnything3MonoLargeEnglishName);
        var zipPath = CreateModelPackZip(manifest);
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);
        var health = new InternalToolsHealthChecker().CheckDetailed(PathsForTargetRoot(targetRoot));

        Assert.True(result.Success);
        var selectable = Iw3DepthModelMapper.CreateSelectableCandidates(
            health.ModelInventory.SelectionCandidates,
            useSpanish: false);
        Assert.Equal(2, selectable.Count);
        Assert.Contains(selectable, candidate =>
            candidate.MappingKey == Iw3DepthModelMapper.DepthAnything3MonoLargeKey &&
            candidate.Iw3DepthModelName == Iw3DepthModelMapper.AnyV3MonoDepthModelName);
        Assert.Contains(selectable, candidate =>
            candidate.MappingKey == Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey &&
            candidate.Iw3DepthModelName == Iw3DepthModelMapper.AnyV3Mono01DepthModelName);
    }

    [Fact]
    public async Task ValidModelPack_InstalledGatedSafeCheckpointIsKnownButNotSelectableAfterInventoryRefresh()
    {
        var manifest = CreateValidManifest(
            checkpointPath: Iw3DepthModelMapper.DepthProRelativePath,
            mappingKey: Iw3DepthModelMapper.DepthProKey,
            iw3DepthModelName: Iw3DepthModelMapper.DepthProDepthModelName,
            displayName: Iw3DepthModelMapper.DepthProEnglishName);
        var zipPath = CreateModelPackZip(manifest);
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);
        var health = new InternalToolsHealthChecker().CheckDetailed(PathsForTargetRoot(targetRoot));

        Assert.True(result.Success);
        Assert.Empty(Iw3DepthModelMapper.CreateSelectableCandidates(
            health.ModelInventory.SelectionCandidates,
            useSpanish: false));
        Assert.Empty(Iw3DepthModelMapper.GetUnmappedCandidates(
            health.ModelInventory.SelectionCandidates));
    }

    [Fact]
    public async Task ValidModelPack_InstalledUnmappedCompatibleCheckpointRemainsDiagnosticOnlyAfterInventoryRefresh()
    {
        var manifest = CreateValidManifest(
            checkpointPath: "hub/checkpoints/unmapped_model_pack_depth.pt");
        var zipPath = CreateModelPackZip(manifest);
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);
        var health = new InternalToolsHealthChecker().CheckDetailed(PathsForTargetRoot(targetRoot));

        Assert.True(result.Success);
        Assert.Empty(Iw3DepthModelMapper.CreateSelectableCandidates(
            health.ModelInventory.SelectionCandidates,
            useSpanish: false));
        var unmapped = Assert.Single(Iw3DepthModelMapper.GetUnmappedCandidates(
            health.ModelInventory.SelectionCandidates));
        Assert.Equal("hub/checkpoints/unmapped_model_pack_depth.pt", unmapped.RelativePath);
        Assert.Null(unmapped.MappingKey);
        Assert.Null(unmapped.Iw3DepthModelName);
    }

    [Fact]
    public async Task ValidModelPack_WithSafeDirectoryEntries_IgnoresDirectoriesAndInstallsDeclaredFilesOnly()
    {
        var zipPath = CreateModelPackZip(directoryEntries: SafeDirectoryEntries);
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.InstalledFiles.Count);
        Assert.Contains(
            result.InstalledFiles,
            file => file.RelativePath == "hub/checkpoints/depth_anything_metric_depth_outdoor.pt");
        Assert.Contains(
            result.InstalledFiles,
            file => file.RelativePath == "licenses/models/depth-anything-outdoor/LICENSE.txt");
        Assert.DoesNotContain(
            result.InstalledFiles,
            file => file.RelativePath is "hub" or "hub/checkpoints" or "licenses" or "licenses/models");
        AssertTargetFile(targetRoot, "hub/checkpoints/depth_anything_metric_depth_outdoor.pt", OutdoorCheckpointBytes);
        AssertTargetFile(targetRoot, "licenses/models/depth-anything-outdoor/LICENSE.txt", LicenseBytes);
    }

    [Fact]
    public async Task ExistingSameHashFile_IsSkippedAndNotOverwritten()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        WriteTargetFile(
            targetRoot,
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            OutdoorCheckpointBytes);
        var checkpointPath = TargetPath(targetRoot, "hub/checkpoints/depth_anything_metric_depth_outdoor.pt");
        var originalWriteTime = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(checkpointPath, originalWriteTime);

        var result = await InstallAsync(zipPath, targetRoot);

        Assert.True(result.Success);
        Assert.Single(result.InstalledFiles);
        var alreadyInstalled = Assert.Single(result.AlreadyInstalledFiles);
        var skipped = Assert.Single(result.SkippedFiles);
        Assert.Equal("hub/checkpoints/depth_anything_metric_depth_outdoor.pt", alreadyInstalled.RelativePath);
        Assert.Equal(alreadyInstalled.RelativePath, skipped.RelativePath);
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(checkpointPath));
    }

    [Fact]
    public async Task ExistingDifferentHashFile_FailsBeforeCopy()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        WriteTargetFile(
            targetRoot,
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            "different checkpoint"u8.ToArray());

        var result = await InstallAsync(zipPath, targetRoot);

        Assert.False(result.Success);
        Assert.Empty(result.InstalledFiles);
        Assert.Empty(result.RollbackFilesRemoved);
        Assert.Contains(result.Errors, error => error.Contains("different content", StringComparison.OrdinalIgnoreCase));
        AssertTargetFile(
            targetRoot,
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            "different checkpoint"u8.ToArray());
        Assert.False(File.Exists(TargetPath(targetRoot, "licenses/models/depth-anything-outdoor/LICENSE.txt")));
    }

    [Fact]
    public async Task InvalidPack_DoesNotCopyAnything()
    {
        var zipPath = CreateZipWithoutManifest();
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);

        Assert.False(result.Success);
        Assert.Empty(result.InstalledFiles);
        Assert.Empty(result.RollbackFilesRemoved);
        Assert.Contains(result.Errors, error => error.Contains(ModelPackManifest.FileName, StringComparison.Ordinal));
        Assert.False(Directory.Exists(targetRoot));
    }

    [Fact]
    public async Task ChangedZipAfterValidPlan_FailsStagedHashVerification()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        var plan = await CreatePlanAsync(zipPath, targetRoot);
        Assert.True(plan.IsValid);
        CreateModelPackZip(
            zipPath: zipPath,
            fileContentOverrides: new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["hub/checkpoints/depth_anything_metric_depth_outdoor.pt"] =
                    Enumerable.Repeat((byte)'x', OutdoorCheckpointBytes.Length).ToArray(),
            });

        var result = await new ModelPackInstallExecutor().InstallAsync(plan, StagingRoot());

        Assert.False(result.Success);
        Assert.Empty(result.InstalledFiles);
        Assert.Empty(result.RollbackFilesRemoved);
        Assert.Contains(result.Errors, error => error.Contains("Staged file SHA256 mismatch", StringComparison.Ordinal));
        Assert.False(Directory.Exists(targetRoot));
    }

    [Fact]
    public async Task Rollback_RemovesNewlyCopiedFilesAfterMidInstallFailure()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        var fileOperations = new FailingCopyFileOperations(failOnCopyCall: 2);

        var result = await InstallAsync(zipPath, targetRoot, fileOperations);

        Assert.False(result.Success);
        var rolledBack = Assert.Single(result.RollbackFilesRemoved);
        Assert.Equal("hub/checkpoints/depth_anything_metric_depth_outdoor.pt", rolledBack.RelativePath);
        Assert.False(File.Exists(TargetPath(targetRoot, "hub/checkpoints/depth_anything_metric_depth_outdoor.pt")));
        Assert.False(File.Exists(TargetPath(targetRoot, "licenses/models/depth-anything-outdoor/LICENSE.txt")));
    }

    [Fact]
    public async Task Rollback_DoesNotRemovePreExistingFiles()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        WriteTargetFile(targetRoot, "hub/checkpoints/preexisting-note.txt", "keep me"u8.ToArray());
        var fileOperations = new FailingCopyFileOperations(failOnCopyCall: 2);

        var result = await InstallAsync(zipPath, targetRoot, fileOperations);

        Assert.False(result.Success);
        AssertTargetFile(targetRoot, "hub/checkpoints/preexisting-note.txt", "keep me"u8.ToArray());
        Assert.Single(result.RollbackFilesRemoved);
    }

    [Fact]
    public async Task Rollback_DoesNotRemoveSameHashAlreadyInstalledFiles()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        WriteTargetFile(
            targetRoot,
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            OutdoorCheckpointBytes);
        var fileOperations = new FailingCopyFileOperations(failOnCopyCall: 1);

        var result = await InstallAsync(zipPath, targetRoot, fileOperations);

        Assert.False(result.Success);
        Assert.Empty(result.RollbackFilesRemoved);
        var skipped = Assert.Single(result.SkippedFiles);
        Assert.Equal("hub/checkpoints/depth_anything_metric_depth_outdoor.pt", skipped.RelativePath);
        AssertTargetFile(targetRoot, "hub/checkpoints/depth_anything_metric_depth_outdoor.pt", OutdoorCheckpointBytes);
    }

    [Fact]
    public async Task NestedLicenseFile_InstallsWhenDeclared()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);

        Assert.True(result.Success);
        Assert.Contains(
            result.InstalledFiles,
            file => file.RelativePath == "licenses/models/depth-anything-outdoor/LICENSE.txt");
        AssertTargetFile(targetRoot, "licenses/models/depth-anything-outdoor/LICENSE.txt", LicenseBytes);
    }

    [Fact]
    public async Task UnsafeZipPathTraversal_CannotBeInstalled()
    {
        var manifest = CreateValidManifest();
        manifest.Files[0].Path = "hub/../model.pt";
        manifest.Models[0].File = manifest.Files[0].Path;
        var zipPath = CreateModelPackZip(manifest);
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);

        Assert.False(result.Success);
        Assert.Empty(result.InstalledFiles);
        Assert.Contains(result.Errors, error => error.Contains("traversal", StringComparison.OrdinalIgnoreCase));
        Assert.False(Directory.Exists(targetRoot));
    }

    [Fact]
    public async Task ProtectedIw3RowFlowRuntimeDependency_CannotBeInstalled()
    {
        var manifest = CreateValidManifest(
            checkpointPath: "hub/checkpoints/iw3_row_flow_v3_20250627.pth",
            checkpointBytes: "row flow"u8.ToArray());
        var zipPath = CreateModelPackZip(manifest);
        var targetRoot = TargetRoot();

        var result = await InstallAsync(zipPath, targetRoot);

        Assert.False(result.Success);
        Assert.Empty(result.InstalledFiles);
        Assert.Contains(
            result.Errors,
            error => error.Contains("protected iw3 runtime dependency", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(TargetPath(targetRoot, "hub/checkpoints/iw3_row_flow_v3_20250627.pth")));
    }

    [Fact]
    public async Task InstallResult_ReportsInstalledAlreadyInstalledAndErrorsClearly()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        WriteTargetFile(
            targetRoot,
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            OutdoorCheckpointBytes);

        var result = await InstallAsync(zipPath, targetRoot);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Single(result.InstalledFiles);
        Assert.Single(result.AlreadyInstalledFiles);
        Assert.Single(result.SkippedFiles);
        Assert.Empty(result.RollbackFilesRemoved);
        Assert.NotNull(result.Manifest);
        Assert.False(string.IsNullOrWhiteSpace(result.StagingPath));
    }

    private async Task<ModelPackInstallResult> InstallAsync(
        string zipPath,
        string targetRoot,
        IModelPackInstallFileOperations? fileOperations = null) =>
        await new ModelPackInstallExecutor(fileOperations: fileOperations).InstallAsync(
            new ModelPackInstallRequest(
                zipPath,
                targetRoot,
                StagingRoot(),
                CurrentIw3Version: "nunif-d23721f1"));

    private static async Task<ModelPackDryRunInstallPlan> CreatePlanAsync(
        string zipPath,
        string targetRoot) =>
        await new ModelPackInstallPlanner().CreateDryRunPlanAsync(
            new ModelPackDryRunInstallRequest(
                zipPath,
                targetRoot,
                CurrentIw3Version: "nunif-d23721f1"));

    private string CreateModelPackZip(
        MutableModelPackManifest? manifest = null,
        IReadOnlyList<PackEntry>? extraEntries = null,
        IReadOnlySet<string>? omitEntryPaths = null,
        string? zipPath = null,
        IReadOnlyDictionary<string, byte[]>? fileContentOverrides = null,
        IReadOnlyList<string>? directoryEntries = null)
    {
        manifest ??= CreateValidManifest();
        Directory.CreateDirectory(root);
        zipPath ??= Path.Combine(root, $"{Guid.NewGuid():N}.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        WriteZipEntry(
            archive,
            ModelPackManifest.FileName,
            JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));

        foreach (var directoryEntry in directoryEntries ?? [])
        {
            WriteZipDirectoryEntry(archive, directoryEntry);
        }

        var fileBytes = CreateFileBytes(manifest, fileContentOverrides);
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

    private string CreateZipWithoutManifest()
    {
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        WriteZipEntry(archive, "hub/checkpoints/depth_anything_metric_depth_outdoor.pt", OutdoorCheckpointBytes);
        return zipPath;
    }

    private static Dictionary<string, byte[]> CreateFileBytes(
        MutableModelPackManifest manifest,
        IReadOnlyDictionary<string, byte[]>? fileContentOverrides)
    {
        var bytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (fileContentOverrides?.TryGetValue(file.Path, out var overrideBytes) == true)
            {
                bytes[file.Path] = overrideBytes;
                continue;
            }

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

    private string StagingRoot() =>
        Path.Combine(root, "staging");

    private InternalToolPaths PathsForTargetRoot(string targetRoot) => new(
        FfmpegExecutable: Path.Combine(root, "app", "tools", "ffmpeg", "win-x64", "ffmpeg.exe"),
        FfprobeExecutable: Path.Combine(root, "app", "tools", "ffmpeg", "win-x64", "ffprobe.exe"),
        PythonExecutable: Path.Combine(root, "app", "engine", "iw3", "python", "python.exe"),
        Iw3EngineDirectory: Path.Combine(root, "app", "engine", "iw3"),
        ModelsDirectory: targetRoot);

    private static string TargetPath(string targetRoot, string relativePath) =>
        Path.Combine([targetRoot, .. relativePath.Split('/')]);

    private static void WriteTargetFile(string targetRoot, string relativePath, byte[] bytes)
    {
        var path = TargetPath(targetRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static void AssertTargetFile(string targetRoot, string relativePath, byte[] expectedBytes)
    {
        var path = TargetPath(targetRoot, relativePath);
        Assert.True(File.Exists(path));
        Assert.Equal(expectedBytes, File.ReadAllBytes(path));
    }

    private static MutableModelPackManifest CreateValidManifest(
        string checkpointPath = "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
        byte[]? checkpointBytes = null,
        string mappingKey = "depth-anything-metric-outdoor",
        string iw3DepthModelName = "ZoeD_Any_K",
        string displayName = "Depth Anything Metric Outdoor")
    {
        checkpointBytes ??= OutdoorCheckpointBytes;
        return new MutableModelPackManifest
        {
            SchemaVersion = 1,
            PackId = mappingKey,
            PackVersion = "1.0.0",
            DisplayName = displayName,
            TargetRoot = ModelPackManifest.ExpectedTargetRoot,
            CompatibleIw3Versions = ["nunif-d23721f1"],
            MinV3dfyVersion = "0.1.0-preview.1",
            Models =
            [
                new()
                {
                    MappingKey = mappingKey,
                    Iw3DepthModelName = iw3DepthModelName,
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

    private sealed class FailingCopyFileOperations(int failOnCopyCall) : IModelPackInstallFileOperations
    {
        private readonly FileSystemModelPackInstallFileOperations inner = new();
        private int copyCalls;

        public bool FileExists(string path) =>
            inner.FileExists(path);

        public long GetFileLength(string path) =>
            inner.GetFileLength(path);

        public Stream OpenRead(string path) =>
            inner.OpenRead(path);

        public void CreateDirectory(string path) =>
            inner.CreateDirectory(path);

        public async Task CopyFileAsync(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            copyCalls++;
            if (copyCalls == failOnCopyCall)
            {
                throw new IOException("Simulated copy failure.");
            }

            await inner.CopyFileAsync(sourcePath, destinationPath, cancellationToken);
        }

        public void MoveFile(string sourcePath, string destinationPath) =>
            inner.MoveFile(sourcePath, destinationPath);

        public void DeleteFile(string path) =>
            inner.DeleteFile(path);

        public void DeleteDirectory(string path, bool recursive) =>
            inner.DeleteDirectory(path, recursive);
    }

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
