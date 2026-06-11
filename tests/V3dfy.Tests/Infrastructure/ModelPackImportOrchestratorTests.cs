using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using V3dfy.Core.Models;
using V3dfy.Infrastructure.ModelPacks;
using V3dfy.Infrastructure.Paths;

namespace V3dfy.Tests.Infrastructure;

public sealed class ModelPackImportOrchestratorTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string root = TestPaths.TempRoot(
        "model-pack-import-orchestrator",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareImport_ResolvesTargetPretrainedModelsRootFromRuntimeRoot()
    {
        var runtimeRoot = RuntimeRoot();
        var zipPath = CreateModelPackZip();
        var helperPath = CreateHelperExecutable();

        var result = await CreateOrchestrator().PrepareImportAsync(CreateRequest(
            runtimeRoot,
            zipPath,
            helperPath));

        var expectedTargetRoot = new InternalToolPathResolver(runtimeRoot)
            .Resolve()
            .ModelsDirectory;
        Assert.Equal(Path.GetFullPath(expectedTargetRoot), result.Preparation.TargetPretrainedModelsRoot);
    }

    [Fact]
    public async Task PrepareImport_ValidatesGoodModelPackAndReturnsPlanAndElevationFlag()
    {
        var zipPath = CreateModelPackZip();

        var result = await CreateOrchestrator().PrepareImportAsync(CreateRequest(
            RuntimeRoot(),
            zipPath,
            CreateHelperExecutable()));

        Assert.True(result.Preparation.IsValid);
        Assert.True(result.CanLaunch);
        Assert.NotNull(result.Preparation.Manifest);
        Assert.Equal(2, result.Preparation.FilesToInstall.Count);
        Assert.Empty(result.Preparation.AlreadyInstalledFiles);
        Assert.Empty(result.Preparation.Conflicts);
        Assert.Empty(result.Preparation.ValidationErrors);
        Assert.False(result.Preparation.ElevationRequired);
    }

    [Fact]
    public async Task PrepareImport_InvalidModelPackReturnsErrorsAndDoesNotBuildLaunchRequest()
    {
        var result = await CreateOrchestrator().PrepareImportAsync(CreateRequest(
            RuntimeRoot(),
            CreateZipWithoutManifest(),
            CreateHelperExecutable()));

        Assert.False(result.Preparation.IsValid);
        Assert.False(result.CanLaunch);
        Assert.Null(result.LaunchRequest);
        Assert.Null(result.StartInfo);
        Assert.Contains(
            result.Preparation.ValidationErrors,
            error => error.Contains(ModelPackManifest.FileName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareImport_InvalidModelPackWithMissingHelperReturnsPackErrorsNotHelperError()
    {
        var result = await CreateOrchestrator().PrepareImportAsync(CreateRequest(
            RuntimeRoot(),
            CreateZipWithoutManifest(),
            Path.Combine(root, "missing-helper.exe")));

        Assert.False(result.Preparation.IsValid);
        Assert.False(result.CanLaunch);
        Assert.Contains(
            result.Preparation.ValidationErrors,
            error => error.Contains(ModelPackManifest.FileName, StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Preparation.ValidationErrors,
            error => error.Contains("helper executable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PrepareImport_ValidModelPackWithMissingHelperReturnsClearHelperError()
    {
        var result = await CreateOrchestrator().PrepareImportAsync(CreateRequest(
            RuntimeRoot(),
            CreateModelPackZip(),
            Path.Combine(root, "missing-helper.exe")));

        Assert.False(result.Preparation.IsValid);
        Assert.False(result.CanLaunch);
        Assert.Null(result.LaunchRequest);
        Assert.Null(result.StartInfo);
        Assert.Contains(
            result.Preparation.ValidationErrors,
            error => error.Contains("helper executable was not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PrepareImport_ConflictsPreventLaunchRequest()
    {
        var runtimeRoot = RuntimeRoot();
        var targetRoot = new InternalToolPathResolver(runtimeRoot).Resolve().ModelsDirectory;
        WriteTargetFile(
            targetRoot,
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            "different checkpoint"u8.ToArray());

        var result = await CreateOrchestrator().PrepareImportAsync(CreateRequest(
            runtimeRoot,
            CreateModelPackZip(),
            CreateHelperExecutable()));

        Assert.False(result.Preparation.IsValid);
        Assert.NotEmpty(result.Preparation.Conflicts);
        Assert.False(result.CanLaunch);
        Assert.Null(result.LaunchRequest);
        Assert.Null(result.StartInfo);
    }

    [Fact]
    public async Task PrepareImport_CreatesTempPathsUnderWorkRootNotTargetRoot()
    {
        var workRoot = WorkRoot();
        var provider = new FixedWorkPathProvider(workRoot);

        var result = await CreateOrchestrator(provider).PrepareImportAsync(CreateRequest(
            RuntimeRoot(),
            CreateModelPackZip(),
            CreateHelperExecutable()));

        Assert.True(result.Preparation.IsValid);
        AssertPathUnder(workRoot, result.Preparation.WorkPaths.StagingRoot);
        AssertPathUnder(workRoot, result.Preparation.WorkPaths.ResultPath);
        AssertPathUnder(workRoot, result.Preparation.WorkPaths.LogPath!);
        AssertPathNotUnderProgramFiles(result.Preparation.WorkPaths.StagingRoot);
        AssertPathNotUnderProgramFiles(result.Preparation.WorkPaths.ResultPath);
        AssertPathNotUnderProgramFiles(result.Preparation.WorkPaths.LogPath!);
        Assert.DoesNotContain(
            result.Preparation.TargetPretrainedModelsRoot,
            result.Preparation.WorkPaths.StagingRoot,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(result.Preparation.WorkPaths.StagingRoot));
    }

    [Fact]
    public async Task PrepareImport_LaunchRequestIncludesHelperZipTargetStagingVersionAndResultPaths()
    {
        var request = CreateRequest(
            RuntimeRoot(),
            CreateModelPackZip(),
            CreateHelperExecutable());

        var result = await CreateOrchestrator().PrepareImportAsync(request);

        Assert.True(result.CanLaunch);
        Assert.NotNull(result.LaunchRequest);
        Assert.Equal(Path.GetFullPath(request.HelperExecutablePath), result.LaunchRequest.HelperExecutablePath);
        Assert.Equal(Path.GetFullPath(request.ModelPackZipPath), result.LaunchRequest.ModelPackZipPath);
        Assert.Equal(result.Preparation.TargetPretrainedModelsRoot, result.LaunchRequest.TargetPretrainedModelsRoot);
        Assert.Equal(result.Preparation.WorkPaths.StagingRoot, result.LaunchRequest.StagingRoot);
        Assert.Equal(request.CurrentIw3Version, result.LaunchRequest.CurrentIw3Version);
        Assert.Equal(result.Preparation.WorkPaths.ResultPath, result.LaunchRequest.ResultPath);
        Assert.Equal(result.Preparation.WorkPaths.LogPath, result.LaunchRequest.LogPath);

        Assert.NotNull(result.StartInfo);
        Assert.Contains(result.LaunchRequest.ModelPackZipPath, result.StartInfo.ArgumentList);
        Assert.Contains(result.LaunchRequest.TargetPretrainedModelsRoot, result.StartInfo.ArgumentList);
        Assert.Contains(result.LaunchRequest.StagingRoot, result.StartInfo.ArgumentList);
        Assert.Contains(result.LaunchRequest.ResultPath!, result.StartInfo.ArgumentList);
        Assert.Contains(result.LaunchRequest.LogPath!, result.StartInfo.ArgumentList);
    }

    [Fact]
    public async Task PrepareImport_PathsWithSpacesSurviveLaunchRequest()
    {
        var runtimeRoot = Path.Combine(root, "runtime root with spaces");
        var zipPath = CreateModelPackZip(
            zipPath: Path.Combine(root, "model pack with spaces.zip"));
        var helperPath = CreateHelperExecutable(Path.Combine(root, "helper dir with spaces"));
        var workRoot = Path.Combine(root, "work root with spaces");

        var result = await CreateOrchestrator(new FixedWorkPathProvider(workRoot)).PrepareImportAsync(
            CreateRequest(runtimeRoot, zipPath, helperPath));

        Assert.True(result.CanLaunch);
        Assert.NotNull(result.StartInfo);
        Assert.Contains(" ", result.LaunchRequest!.HelperExecutablePath);
        Assert.Contains(" ", result.LaunchRequest.ModelPackZipPath);
        Assert.Contains(" ", result.LaunchRequest.TargetPretrainedModelsRoot);
        Assert.Contains(" ", result.LaunchRequest.StagingRoot);
        Assert.Contains(result.LaunchRequest.ModelPackZipPath, result.StartInfo.ArgumentList);
        Assert.Contains(result.LaunchRequest.TargetPretrainedModelsRoot, result.StartInfo.ArgumentList);
        Assert.Contains(result.LaunchRequest.StagingRoot, result.StartInfo.ArgumentList);
    }

    [Fact]
    public async Task HelperResultReader_ParsesSuccessResult()
    {
        var resultPath = WriteHelperResult(CreateInstallResult(success: true));

        var result = await new ModelPackHelperResultReader().ReadAsync(resultPath);

        Assert.True(result.Success);
        Assert.Single(result.InstalledFiles);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task HelperResultReader_ParsesFailureAndErrors()
    {
        var resultPath = WriteHelperResult(CreateInstallResult(
            success: false,
            errors: ["failed validation"]));

        var result = await new ModelPackHelperResultReader().ReadAsync(resultPath);

        Assert.False(result.Success);
        Assert.Contains("failed validation", result.Errors);
    }

    [Fact]
    public async Task CompleteAfterHelperResult_SuccessTriggersInventoryRefreshHook()
    {
        var refreshHook = new RecordingRefreshHook();
        var orchestrator = CreateOrchestrator(inventoryRefreshHook: refreshHook);
        var resultPath = WriteHelperResult(CreateInstallResult(success: true));

        var completion = await orchestrator.CompleteAfterHelperResultAsync(resultPath);

        Assert.True(completion.HelperResult.Success);
        Assert.True(completion.RefreshNeeded);
        Assert.True(completion.RefreshCompleted);
        Assert.Equal(1, refreshHook.RefreshCount);
    }

    [Fact]
    public async Task CompleteAfterHelperResult_FailureDoesNotTriggerInventoryRefreshHook()
    {
        var refreshHook = new RecordingRefreshHook();
        var orchestrator = CreateOrchestrator(inventoryRefreshHook: refreshHook);
        var resultPath = WriteHelperResult(CreateInstallResult(
            success: false,
            errors: ["install failed"]));

        var completion = await orchestrator.CompleteAfterHelperResultAsync(resultPath);

        Assert.False(completion.HelperResult.Success);
        Assert.False(completion.RefreshNeeded);
        Assert.False(completion.RefreshCompleted);
        Assert.Equal(0, refreshHook.RefreshCount);
    }

    private ModelPackImportOrchestrator CreateOrchestrator(
        IModelPackImportWorkPathProvider? workPathProvider = null,
        IModelPackInventoryRefreshHook? inventoryRefreshHook = null) => new(
        workPathProvider: workPathProvider ?? new FixedWorkPathProvider(WorkRoot()),
        inventoryRefreshHook: inventoryRefreshHook);

    private static ModelPackImportPrepareRequest CreateRequest(
        string runtimeRoot,
        string zipPath,
        string helperPath) => new(
        RuntimeRoot: runtimeRoot,
        ModelPackZipPath: zipPath,
        HelperExecutablePath: helperPath,
        CurrentIw3Version: "nunif-d23721f1",
        CurrentV3dfyVersion: "0.1.0-preview.1");

    private string CreateHelperExecutable(string? helperDirectory = null)
    {
        helperDirectory ??= Path.Combine(root, "helper");
        Directory.CreateDirectory(helperDirectory);
        var helperPath = Path.Combine(helperDirectory, "V3dfy.SetupHelper.exe");
        File.WriteAllBytes(helperPath, "helper"u8.ToArray());
        return helperPath;
    }

    private string WriteHelperResult(ModelPackInstallResult result)
    {
        var path = Path.Combine(root, "helper-results", $"{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, JsonOptions));
        return path;
    }

    private static ModelPackInstallResult CreateInstallResult(
        bool success,
        IReadOnlyList<string>? errors = null)
    {
        var plannedFile = new ModelPackPlannedFile(
            "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
            @"C:\temp\pretrained_models\hub\checkpoints\depth_anything_metric_depth_outdoor.pt",
            Sha256(OutdoorCheckpointBytes),
            OutdoorCheckpointBytes.Length,
            "checkpoint");

        return new ModelPackInstallResult(
            Success: success,
            Manifest: new ModelPackManifestSummary(
                "depth-anything-metric-outdoor",
                "1.0.0",
                "Depth Anything Metric Outdoor",
                ModelPackManifest.ExpectedTargetRoot,
                ["nunif-d23721f1"],
                "0.1.0-preview.1",
                ModelCount: 1,
                FileCount: 2),
            ModelPackZipPath: @"C:\temp\model-pack.zip",
            TargetPretrainedModelsRoot: @"C:\temp\pretrained_models",
            StagingPath: @"C:\temp\staging",
            InstalledFiles: success ? [plannedFile] : [],
            AlreadyInstalledFiles: [],
            SkippedFiles: [],
            RollbackFilesRemoved: [],
            Errors: errors ?? [],
            Warnings: []);
    }

    private string CreateModelPackZip(
        MutableModelPackManifest? manifest = null,
        string? zipPath = null)
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

    private string RuntimeRoot() =>
        Path.Combine(root, "runtime");

    private string WorkRoot() =>
        Path.Combine(root, "local-app-data", "v3dfy", "model-pack-imports", Guid.NewGuid().ToString("N"));

    private static string TargetPath(string targetRoot, string relativePath) =>
        Path.Combine([targetRoot, .. relativePath.Split('/')]);

    private static void WriteTargetFile(string targetRoot, string relativePath, byte[] bytes)
    {
        var path = TargetPath(targetRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static void AssertPathUnder(string rootDirectory, string path)
    {
        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);
        Assert.StartsWith(root, candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertPathNotUnderProgramFiles(string path)
    {
        var candidate = Path.GetFullPath(path);
        var programFilesRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            }
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Select(static root => Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar);

        Assert.DoesNotContain(
            programFilesRoots,
            root => candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase));
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

    private sealed class FixedWorkPathProvider(string rootDirectory) : IModelPackImportWorkPathProvider
    {
        public ModelPackImportWorkPaths CreateWorkPaths() => new(
            RootDirectory: rootDirectory,
            StagingRoot: Path.Combine(rootDirectory, "staging"),
            ResultPath: Path.Combine(rootDirectory, "model-pack-result.json"),
            LogPath: Path.Combine(rootDirectory, "model-pack-log.json"));
    }

    private sealed class RecordingRefreshHook : IModelPackInventoryRefreshHook
    {
        public int RefreshCount { get; private set; }

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            return Task.CompletedTask;
        }
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
