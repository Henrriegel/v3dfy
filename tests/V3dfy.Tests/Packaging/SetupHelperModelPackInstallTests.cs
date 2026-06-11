using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using V3dfy.Core.Models;
using V3dfy.Infrastructure.ModelPacks;
using V3dfy.SetupHelper;

namespace V3dfy.Tests.Packaging;

public sealed class SetupHelperModelPackInstallTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string root = TestPaths.TempRoot(
        "setup-helper-model-pack",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ModelPackInstallMode_SucceedsAgainstTempPretrainedModelsRoot()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        var resultPath = ResultPath();

        var exitCode = await RunHelperAsync(zipPath, targetRoot, resultPath);

        Assert.Equal(0, exitCode);
        var result = await ReadResultAsync(resultPath);
        Assert.True(result.Success);
        Assert.Equal(2, result.InstalledFiles.Count);
        Assert.Empty(result.Errors);
        AssertTargetFile(targetRoot, "hub/checkpoints/depth_anything_metric_depth_outdoor.pt", OutdoorCheckpointBytes);
        AssertTargetFile(targetRoot, "licenses/models/depth-anything-outdoor/LICENSE.txt", LicenseBytes);
        Assert.False(File.Exists(Path.Combine(targetRoot, ModelPackManifest.FileName)));
    }

    [Fact]
    public async Task ModelPackInstallMode_AcceptsSafeDirectoryEntries()
    {
        var zipPath = CreateModelPackZip(directoryEntries: SafeDirectoryEntries);
        var targetRoot = TargetRoot();
        var resultPath = ResultPath();

        var exitCode = await RunHelperAsync(zipPath, targetRoot, resultPath);

        Assert.Equal(0, exitCode);
        var result = await ReadResultAsync(resultPath);
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
    public async Task ModelPackInstallMode_FailsForInvalidPackAndWritesErrors()
    {
        var zipPath = CreateZipWithoutManifest();
        var resultPath = ResultPath();

        var exitCode = await RunHelperAsync(zipPath, TargetRoot(), resultPath);

        Assert.NotEqual(0, exitCode);
        var result = await ReadResultAsync(resultPath);
        Assert.False(result.Success);
        Assert.Contains(
            result.Errors,
            error => error.Contains(ModelPackManifest.FileName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ModelPackInstallMode_WritesMachineReadableResultToLogPath()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        var logPath = ResultPath();
        var args = ModelPackHelperContract.CreateInstallArguments(new ModelPackHelperInstallCommand(
            zipPath,
            targetRoot,
            StagingRoot(),
            "nunif-d23721f1",
            LogPath: logPath));

        var exitCode = await SetupHelperProgram.RunAsync([.. args], CancellationToken.None);

        Assert.Equal(0, exitCode);
        var result = await ReadResultAsync(logPath);
        Assert.True(result.Success);
        Assert.Equal(2, result.InstalledFiles.Count);
    }

    [Fact]
    public async Task ModelPackInstallMode_DoesNotWriteOutsideTargetRoot()
    {
        var manifest = CreateValidManifest(
            checkpointPath: "../outside.pt",
            checkpointBytes: "outside"u8.ToArray());
        var zipPath = CreateModelPackZip(manifest);
        var targetRoot = TargetRoot();
        var escapedPath = Path.GetFullPath(Path.Combine(targetRoot, "..", "outside.pt"));

        var exitCode = await RunHelperAsync(zipPath, targetRoot, ResultPath());

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(escapedPath));
        Assert.False(Directory.Exists(targetRoot));
    }

    [Fact]
    public async Task ModelPackInstallMode_PreservesAlreadyInstalledSameHashFiles()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        WriteTargetFile(
            targetRoot,
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            OutdoorCheckpointBytes);
        var checkpointPath = TargetPath(targetRoot, "hub/checkpoints/depth_anything_metric_depth_outdoor.pt");
        var originalWriteTime = new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(checkpointPath, originalWriteTime);
        var resultPath = ResultPath();

        var exitCode = await RunHelperAsync(zipPath, targetRoot, resultPath);

        Assert.Equal(0, exitCode);
        var result = await ReadResultAsync(resultPath);
        Assert.Single(result.AlreadyInstalledFiles);
        Assert.Single(result.SkippedFiles);
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(checkpointPath));
    }

    [Fact]
    public async Task ModelPackInstallMode_RollsBackOnSimulatedMidInstallFailure()
    {
        var zipPath = CreateModelPackZip();
        var targetRoot = TargetRoot();
        var resultPath = ResultPath();
        var executor = new ModelPackInstallExecutor(
            fileOperations: new FailingCopyFileOperations(failOnCopyCall: 2));

        var exitCode = await RunHelperAsync(zipPath, targetRoot, resultPath, executor);

        Assert.NotEqual(0, exitCode);
        var result = await ReadResultAsync(resultPath);
        var rolledBack = Assert.Single(result.RollbackFilesRemoved);
        Assert.Equal("hub/checkpoints/depth_anything_metric_depth_outdoor.pt", rolledBack.RelativePath);
        Assert.False(File.Exists(TargetPath(targetRoot, "hub/checkpoints/depth_anything_metric_depth_outdoor.pt")));
        Assert.False(File.Exists(TargetPath(targetRoot, "licenses/models/depth-anything-outdoor/LICENSE.txt")));
    }

    private async Task<int> RunHelperAsync(
        string zipPath,
        string targetRoot,
        string resultPath,
        ModelPackInstallExecutor? executor = null)
    {
        var args = ModelPackHelperContract.CreateInstallArguments(new ModelPackHelperInstallCommand(
            zipPath,
            targetRoot,
            StagingRoot(),
            "nunif-d23721f1",
            ResultPath: resultPath));

        return await SetupHelperProgram.RunAsync([.. args], CancellationToken.None, executor);
    }

    private static async Task<ModelPackInstallResult> ReadResultAsync(string resultPath)
    {
        await using var stream = File.OpenRead(resultPath);
        var result = await JsonSerializer.DeserializeAsync<ModelPackInstallResult>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        return result;
    }

    private string CreateModelPackZip(
        MutableModelPackManifest? manifest = null,
        string? zipPath = null,
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

        foreach (var (relativePath, bytes) in CreateFileBytes(manifest))
        {
            WriteZipEntry(archive, relativePath, bytes);
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

    private string StagingRoot() =>
        Path.Combine(root, "staging");

    private string ResultPath() =>
        Path.Combine(root, "results", $"{Guid.NewGuid():N}.json");

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
