using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using V3dfy.Core.Models;
using V3dfy.Infrastructure.ModelPacks;
using V3dfy.SetupHelper;

namespace V3dfy.Tests.Packaging;

public sealed class InstallerModelPackImportServiceTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string root = TestPaths.TempRoot(
        "installer-model-pack-import",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TargetPath_IsDerivedFromInstallRootAndStagingFromWorkRoot()
    {
        var installRoot = Path.Combine(root, "install");
        var workRoot = Path.Combine(root, "work");

        var targetRoot = InstallerModelPackImportService.GetDefaultTargetPretrainedModelsRoot(installRoot);
        var stagingRoot = InstallerModelPackImportService.GetDefaultStagingRoot(workRoot);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(
                installRoot,
                "engine",
                "iw3",
                "nunif",
                "iw3",
                "pretrained_models")),
            targetRoot);
        Assert.EndsWith(
            Path.Combine("engine", "iw3", "nunif", "iw3", "pretrained_models"),
            targetRoot,
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFullPath(workRoot), stagingRoot, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("model-pack-import-staging", stagingRoot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Program Files", targetRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_ValidSyntheticPackInstallsFilesIntoDerivedTargetRoot()
    {
        var manifest = CreateValidManifest("valid", "hub/checkpoints/valid.pt");
        var zipPath = CreateModelPackZip(manifest);
        var acquiredFile = CreateAcquiredFile("valid", zipPath);
        var installRoot = InstallRoot();
        var targetRoot = InstallerModelPackImportService.GetDefaultTargetPretrainedModelsRoot(installRoot);

        var result = await ImportAsync([acquiredFile], installRoot);

        var imported = Assert.Single(result.ImportedPacks);
        Assert.Empty(result.Failures);
        Assert.Equal("valid", imported.PackId);
        Assert.True(imported.InstallResult.Success);
        AssertTargetFile(targetRoot, "hub/checkpoints/valid.pt", CheckpointBytes);
        AssertTargetFile(targetRoot, "licenses/models/valid/LICENSE.txt", LicenseBytes);
    }

    [Fact]
    public async Task ImportAsync_WithOptionalOverallTrackerReportsCurrentAndOverallInstallProgress()
    {
        var manifest = CreateValidManifest("tracked-install", "hub/checkpoints/tracked-install.pt");
        var zipPath = CreateModelPackZip(manifest);
        var acquiredFile = CreateAcquiredFile("tracked-install", zipPath);
        var progress = new RecordingSetupProgress();
        var trackedProgress = new SetupOptionalModelPackOverallProgressTracker(
            progress,
            [new SetupOptionalModelPackProgressItem(acquiredFile.AssetFileName, acquiredFile.ZipSizeBytes)]);

        var result = await new InstallerModelPackImportService().ImportAsync(
            [acquiredFile],
            InstallRoot(),
            WorkRoot(),
            CurrentIw3Version,
            CurrentV3dfyVersion,
            new RecordingSetupLog(),
            CancellationToken.None,
            trackedProgress);

        Assert.Single(result.ImportedPacks);
        Assert.Empty(result.Failures);
        Assert.Contains(progress.Events, e =>
            e.Phase == SetupProgressPhase.ValidatingModelPack &&
            e.CurrentFile == acquiredFile.AssetFileName &&
            e.OverallPercent is not null);
        Assert.Contains(progress.Events, e =>
            e.Phase == SetupProgressPhase.InstallingModelPack &&
            e.CurrentFile == acquiredFile.AssetFileName &&
            e.Percent is not null &&
            e.OverallPercent is not null);
    }

    [Fact]
    public async Task ImportAsync_InvalidManifestFailsPackAndDoesNotCreateTarget()
    {
        var zipPath = CreateZipWithoutManifest();
        var installRoot = InstallRoot();
        var targetRoot = InstallerModelPackImportService.GetDefaultTargetPretrainedModelsRoot(installRoot);

        var result = await ImportAsync([CreateAcquiredFile("invalid", zipPath)], installRoot);

        Assert.Empty(result.ImportedPacks);
        var failure = Assert.Single(result.Failures);
        Assert.Contains(ModelPackManifest.FileName, failure.Reason, StringComparison.Ordinal);
        Assert.False(Directory.Exists(targetRoot));
    }

    [Fact]
    public async Task ImportAsync_BadInnerHashFailsPack()
    {
        var manifest = CreateValidManifest("bad-hash", "hub/checkpoints/bad-hash.pt");
        manifest.Files[0].Sha256 = Sha256("wrong hash"u8.ToArray());
        manifest.Models[0].Sha256 = manifest.Files[0].Sha256;
        var zipPath = CreateModelPackZip(manifest);

        var result = await ImportAsync([CreateAcquiredFile("bad-hash", zipPath)], InstallRoot());

        Assert.Empty(result.ImportedPacks);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("SHA256 mismatch", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_PathTraversalFailsPackAndDoesNotWriteOutsideTarget()
    {
        var manifest = CreateValidManifest("traversal", "../outside.pt");
        var zipPath = CreateModelPackZip(manifest);
        var installRoot = InstallRoot();
        var escapedPath = Path.GetFullPath(Path.Combine(
            InstallerModelPackImportService.GetDefaultTargetPretrainedModelsRoot(installRoot),
            "..",
            "outside.pt"));

        var result = await ImportAsync([CreateAcquiredFile("traversal", zipPath)], installRoot);

        Assert.Empty(result.ImportedPacks);
        Assert.Single(result.Failures);
        Assert.False(File.Exists(escapedPath));
    }

    [Fact]
    public async Task ImportAsync_ExistingDifferentContentConflictFailsPackAndPreservesExistingFile()
    {
        var manifest = CreateValidManifest("conflict", "hub/checkpoints/conflict.pt");
        var zipPath = CreateModelPackZip(manifest);
        var installRoot = InstallRoot();
        var targetRoot = InstallerModelPackImportService.GetDefaultTargetPretrainedModelsRoot(installRoot);
        WriteTargetFile(targetRoot, "hub/checkpoints/conflict.pt", "existing different bytes"u8.ToArray());

        var result = await ImportAsync([CreateAcquiredFile("conflict", zipPath)], installRoot);

        Assert.Empty(result.ImportedPacks);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("different content", failure.Reason, StringComparison.OrdinalIgnoreCase);
        AssertTargetFile(targetRoot, "hub/checkpoints/conflict.pt", "existing different bytes"u8.ToArray());
        Assert.False(File.Exists(TargetPath(targetRoot, "licenses/models/conflict/LICENSE.txt")));
    }

    [Fact]
    public async Task ImportAsync_AlreadyInstalledSameHashIsTreatedAsSuccess()
    {
        var manifest = CreateValidManifest("already", "hub/checkpoints/already.pt");
        var zipPath = CreateModelPackZip(manifest);
        var installRoot = InstallRoot();
        var targetRoot = InstallerModelPackImportService.GetDefaultTargetPretrainedModelsRoot(installRoot);
        WriteTargetFile(targetRoot, "hub/checkpoints/already.pt", CheckpointBytes);

        var result = await ImportAsync([CreateAcquiredFile("already", zipPath)], installRoot);

        var imported = Assert.Single(result.ImportedPacks);
        Assert.Empty(result.Failures);
        Assert.True(imported.InstallResult.Success);
        Assert.Single(imported.InstallResult.AlreadyInstalledFiles);
        AssertTargetFile(targetRoot, "hub/checkpoints/already.pt", CheckpointBytes);
        AssertTargetFile(targetRoot, "licenses/models/already/LICENSE.txt", LicenseBytes);
    }

    [Fact]
    public async Task ImportAsync_RollbackRemovesCopiedPackFilesButKeepsUnrelatedFiles()
    {
        var manifest = CreateValidManifest("rollback", "hub/checkpoints/rollback.pt");
        var zipPath = CreateModelPackZip(manifest);
        var installRoot = InstallRoot();
        var targetRoot = InstallerModelPackImportService.GetDefaultTargetPretrainedModelsRoot(installRoot);
        WriteTargetFile(targetRoot, "hub/checkpoints/unrelated.txt", "keep me"u8.ToArray());
        var service = new InstallerModelPackImportService(
            executor: new ModelPackInstallExecutor(
                fileOperations: new FailingCopyFileOperations(failOnCopyCall: 2)));

        var result = await service.ImportAsync(
            [CreateAcquiredFile("rollback", zipPath)],
            installRoot,
            WorkRoot(),
            CurrentIw3Version,
            CurrentV3dfyVersion,
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.ImportedPacks);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("Simulated copy failure", failure.Reason, StringComparison.Ordinal);
        AssertTargetFile(targetRoot, "hub/checkpoints/unrelated.txt", "keep me"u8.ToArray());
        Assert.False(File.Exists(TargetPath(targetRoot, "hub/checkpoints/rollback.pt")));
        Assert.False(File.Exists(TargetPath(targetRoot, "licenses/models/rollback/LICENSE.txt")));
    }

    [Fact]
    public async Task ImportAsync_PartialSuccessContinuesAfterFailure()
    {
        var validManifest = CreateValidManifest("valid-partial", "hub/checkpoints/valid-partial.pt");
        var invalidManifest = CreateValidManifest("invalid-partial", "hub/checkpoints/invalid-partial.pt");
        invalidManifest.SchemaVersion = 999;
        var validZip = CreateModelPackZip(validManifest);
        var invalidZip = CreateModelPackZip(invalidManifest);
        var installRoot = InstallRoot();
        var targetRoot = InstallerModelPackImportService.GetDefaultTargetPretrainedModelsRoot(installRoot);

        var result = await ImportAsync(
            [
                CreateAcquiredFile("valid-partial", validZip),
                CreateAcquiredFile("invalid-partial", invalidZip),
            ],
            installRoot);

        var imported = Assert.Single(result.ImportedPacks);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("valid-partial", imported.PackId);
        Assert.Equal("invalid-partial", failure.PackId);
        Assert.True(result.HasFailures);
        AssertTargetFile(targetRoot, "hub/checkpoints/valid-partial.pt", CheckpointBytes);
    }

    [Fact]
    public async Task ImportAsync_MissingIw3VersionFailsOptionalPackWithoutCopying()
    {
        var manifest = CreateValidManifest("missing-version", "hub/checkpoints/missing-version.pt");
        var zipPath = CreateModelPackZip(manifest);
        var installRoot = InstallRoot();

        var result = await new InstallerModelPackImportService().ImportAsync(
            [CreateAcquiredFile("missing-version", zipPath)],
            installRoot,
            WorkRoot(),
            currentIw3Version: "",
            CurrentV3dfyVersion,
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.ImportedPacks);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("Current bundled iw3 version is required", failure.Reason, StringComparison.Ordinal);
        Assert.False(Directory.Exists(InstallerModelPackImportService.GetDefaultTargetPretrainedModelsRoot(installRoot)));
    }

    private async Task<InstallerModelPackImportResult> ImportAsync(
        IReadOnlyList<InstallerModelPackAcquiredFile> acquiredFiles,
        string installRoot) =>
        await new InstallerModelPackImportService().ImportAsync(
            acquiredFiles,
            installRoot,
            WorkRoot(),
            CurrentIw3Version,
            CurrentV3dfyVersion,
            new RecordingSetupLog(),
            CancellationToken.None);

    private string CreateModelPackZip(MutableModelPackManifest manifest)
    {
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, $"{manifest.PackId}-{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        WriteZipEntry(
            archive,
            ModelPackManifest.FileName,
            JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));
        foreach (var (relativePath, bytes) in CreateFileBytes(manifest))
        {
            WriteZipEntry(archive, relativePath, bytes);
        }

        return zipPath;
    }

    private string CreateZipWithoutManifest()
    {
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, $"invalid-{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        WriteZipEntry(archive, "hub/checkpoints/missing-manifest.pt", CheckpointBytes);
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
                _ => CheckpointBytes,
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

    private InstallerModelPackAcquiredFile CreateAcquiredFile(string packId, string zipPath)
    {
        var fileInfo = new FileInfo(zipPath);
        return new InstallerModelPackAcquiredFile(
            packId,
            packId,
            Path.GetFileName(zipPath),
            InstallerModelPackSourceKind.OfflineLocalZip,
            zipPath,
            Sha256(File.ReadAllBytes(zipPath)),
            fileInfo.Length);
    }

    private string InstallRoot() => Path.Combine(root, "installed-app");

    private string WorkRoot() => Path.Combine(root, "work");

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
        string packId,
        string checkpointPath) => new()
        {
            SchemaVersion = 1,
            PackId = packId,
            PackVersion = "1.0.0",
            DisplayName = packId,
            TargetRoot = ModelPackManifest.ExpectedTargetRoot,
            CompatibleIw3Versions = [CurrentIw3Version],
            MinV3dfyVersion = CurrentV3dfyVersion,
            Models =
            [
                new()
                {
                    MappingKey = packId,
                    Iw3DepthModelName = "Any_Test",
                    File = checkpointPath,
                    Sha256 = Sha256(CheckpointBytes),
                    SizeBytes = CheckpointBytes.Length,
                    Category = "test",
                },
            ],
            Files =
            [
                new()
                {
                    Path = checkpointPath,
                    Sha256 = Sha256(CheckpointBytes),
                    SizeBytes = CheckpointBytes.Length,
                    Role = "checkpoint",
                },
                new()
                {
                    Path = $"licenses/models/{packId}/LICENSE.txt",
                    Sha256 = Sha256(LicenseBytes),
                    SizeBytes = LicenseBytes.Length,
                    Role = "license",
                },
            ],
            Licenses = [$"licenses/models/{packId}/LICENSE.txt"],
        };

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private const string CurrentIw3Version = "nunif-d23721f1";

    private const string CurrentV3dfyVersion = "0.1.0-preview.1";

    private static readonly byte[] CheckpointBytes =
        "synthetic checkpoint bytes"u8.ToArray();

    private static readonly byte[] LicenseBytes =
        "synthetic license bytes"u8.ToArray();

    private sealed class RecordingSetupLog : ISetupLog
    {
        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Error(string message)
        {
        }
    }

    private sealed class RecordingSetupProgress : ISetupProgress
    {
        public List<SetupProgressEvent> Events { get; } = [];

        public void Report(SetupProgressEvent progress) => Events.Add(progress);
    }

    private sealed class FailingCopyFileOperations(int failOnCopyCall) : IModelPackInstallFileOperations
    {
        private readonly FileSystemModelPackInstallFileOperations inner = new();
        private int copyCalls;

        public bool FileExists(string path) => inner.FileExists(path);

        public long GetFileLength(string path) => inner.GetFileLength(path);

        public Stream OpenRead(string path) => inner.OpenRead(path);

        public void CreateDirectory(string path) => inner.CreateDirectory(path);

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

        public void DeleteFile(string path) => inner.DeleteFile(path);

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
