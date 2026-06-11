using System.Diagnostics;
using System.Text.Json;
using V3dfy.Core.Models;
using V3dfy.Infrastructure.ModelPacks;

namespace V3dfy.Tests.Infrastructure;

public sealed class ModelPackImportExecutionServiceTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string root = TestPaths.TempRoot(
        "model-pack-import-execution",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RefusesInvalidPreparationAndDoesNotStartProcess()
    {
        var starter = new RecordingElevatedProcessStarter(SuccessfulProcessResult());
        var service = CreateService(starter);
        var preparation = CreatePreparation(isValid: false);

        var result = await service.ExecuteAsync(preparation);

        Assert.False(result.Success);
        Assert.False(result.HelperProcessStarted);
        Assert.Equal(0, starter.StartCount);
        Assert.Contains(result.Errors, error => error.Contains("preparation is invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_RefusesPreparationWithoutLaunchRequestOrStartInfo()
    {
        var starter = new RecordingElevatedProcessStarter(SuccessfulProcessResult());
        var service = CreateService(starter);

        var withoutLaunchRequest = await service.ExecuteAsync(CreatePreparation(includeLaunchRequest: false));
        var withoutStartInfo = await service.ExecuteAsync(CreatePreparation(includeStartInfo: false));

        Assert.False(withoutLaunchRequest.Success);
        Assert.False(withoutStartInfo.Success);
        Assert.Equal(0, starter.StartCount);
        Assert.Contains(withoutLaunchRequest.Errors, error => error.Contains("launch request is missing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(withoutStartInfo.Errors, error => error.Contains("start information is missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_StartsFakeProcessForValidPreparation()
    {
        var starter = new RecordingElevatedProcessStarter(SuccessfulProcessResult());
        var service = CreateService(starter);
        var preparation = CreatePreparation();
        WriteHelperResult(preparation.LaunchRequest!.ResultPath!, CreateHelperResult(success: true));

        var result = await service.ExecuteAsync(preparation);

        Assert.True(result.Success);
        Assert.Equal(1, starter.StartCount);
        Assert.Same(preparation.StartInfo, starter.LastStartInfo);
    }

    [Fact]
    public async Task ExecuteAsync_ReadsSuccessHelperResultAndTriggersRefresh()
    {
        var refreshHook = new RecordingRefreshHook();
        var service = CreateService(
            new RecordingElevatedProcessStarter(SuccessfulProcessResult()),
            refreshHook);
        var preparation = CreatePreparation();
        WriteHelperResult(preparation.LaunchRequest!.ResultPath!, CreateHelperResult(success: true));

        var result = await service.ExecuteAsync(preparation);

        Assert.True(result.Success);
        Assert.NotNull(result.HelperResult);
        Assert.True(result.HelperResult.Success);
        Assert.True(result.RefreshNeeded);
        Assert.True(result.RefreshCompleted);
        Assert.Equal(1, refreshHook.RefreshCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReadsFailureHelperResultAndDoesNotRefresh()
    {
        var refreshHook = new RecordingRefreshHook();
        var service = CreateService(
            new RecordingElevatedProcessStarter(SuccessfulProcessResult()),
            refreshHook);
        var preparation = CreatePreparation();
        WriteHelperResult(
            preparation.LaunchRequest!.ResultPath!,
            CreateHelperResult(success: false, errors: ["helper validation failed"]));

        var result = await service.ExecuteAsync(preparation);

        Assert.False(result.Success);
        Assert.NotNull(result.HelperResult);
        Assert.False(result.HelperResult.Success);
        Assert.False(result.RefreshNeeded);
        Assert.False(result.RefreshCompleted);
        Assert.Equal(0, refreshHook.RefreshCount);
        Assert.Contains("helper validation failed", result.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_NonZeroExitWithReadableFailureResultReturnsFailure()
    {
        var refreshHook = new RecordingRefreshHook();
        var service = CreateService(
            new RecordingElevatedProcessStarter(new ModelPackElevatedProcessResult(
                Started: true,
                ExitCode: 7)),
            refreshHook);
        var preparation = CreatePreparation();
        WriteHelperResult(
            preparation.LaunchRequest!.ResultPath!,
            CreateHelperResult(success: false, errors: ["install failed"]));

        var result = await service.ExecuteAsync(preparation);

        Assert.False(result.Success);
        Assert.True(result.HelperProcessStarted);
        Assert.Equal(7, result.ExitCode);
        Assert.NotNull(result.HelperResult);
        Assert.Contains(result.Errors, error => error.Contains("exited with code 7", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("install failed", result.Errors);
        Assert.Equal(0, refreshHook.RefreshCount);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroExitWithMissingResultJsonReturnsFailure()
    {
        var service = CreateService(new RecordingElevatedProcessStarter(SuccessfulProcessResult()));
        var preparation = CreatePreparation();

        var result = await service.ExecuteAsync(preparation);

        Assert.False(result.Success);
        Assert.True(result.HelperProcessStarted);
        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.HelperResult);
        Assert.Contains(result.Errors, error => error.Contains("Could not read model pack helper result", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ProcessStartFailureReturnsFailure()
    {
        var starter = new RecordingElevatedProcessStarter(new ModelPackElevatedProcessResult(
            Started: false,
            ExitCode: null,
            ErrorMessage: "UAC was cancelled."));
        var service = CreateService(starter);
        var preparation = CreatePreparation();

        var result = await service.ExecuteAsync(preparation);

        Assert.False(result.Success);
        Assert.False(result.HelperProcessStarted);
        Assert.Equal(1, starter.StartCount);
        Assert.Contains("UAC was cancelled.", result.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessWaitFailureReturnsFailure()
    {
        var starter = new RecordingElevatedProcessStarter(new ModelPackElevatedProcessResult(
            Started: true,
            ExitCode: null,
            ErrorMessage: "Model pack helper wait was canceled."));
        var service = CreateService(starter);
        var preparation = CreatePreparation();

        var result = await service.ExecuteAsync(preparation);

        Assert.False(result.Success);
        Assert.True(result.HelperProcessStarted);
        Assert.Null(result.ExitCode);
        Assert.Null(result.HelperResult);
        Assert.Contains(result.Errors, error => error.Contains("wait was canceled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_PreservesPathsWithSpacesInStartedProcessInfo()
    {
        var starter = new RecordingElevatedProcessStarter(SuccessfulProcessResult());
        var service = CreateService(starter);
        var preparation = CreatePreparation(useSpaces: true);
        WriteHelperResult(preparation.LaunchRequest!.ResultPath!, CreateHelperResult(success: true));

        var result = await service.ExecuteAsync(preparation);

        Assert.True(result.Success);
        Assert.NotNull(starter.LastStartInfo);
        Assert.Contains(" ", starter.LastStartInfo.FileName);
        Assert.Contains(preparation.LaunchRequest.ModelPackZipPath, starter.LastStartInfo.ArgumentList);
        Assert.Contains(preparation.LaunchRequest.TargetPretrainedModelsRoot, starter.LastStartInfo.ArgumentList);
        Assert.Contains(preparation.LaunchRequest.StagingRoot, starter.LastStartInfo.ArgumentList);
        Assert.Contains(preparation.LaunchRequest.ResultPath!, starter.LastStartInfo.ArgumentList);
    }

    [Fact]
    public async Task ExecuteAsync_RefusesMissingResultPath()
    {
        var starter = new RecordingElevatedProcessStarter(SuccessfulProcessResult());
        var service = CreateService(starter);
        var preparation = CreatePreparation(includeResultPath: false);

        var result = await service.ExecuteAsync(preparation);

        Assert.False(result.Success);
        Assert.Equal(0, starter.StartCount);
        Assert.Contains(result.Errors, error => error.Contains("result path is missing", StringComparison.OrdinalIgnoreCase));
    }

    private ModelPackImportExecutionService CreateService(
        RecordingElevatedProcessStarter starter,
        RecordingRefreshHook? refreshHook = null) => new(
        processStarter: starter,
        orchestrator: new ModelPackImportOrchestrator(
            inventoryRefreshHook: refreshHook ?? new RecordingRefreshHook()));

    private ModelPackImportLaunchPreparationResult CreatePreparation(
        bool isValid = true,
        bool includeLaunchRequest = true,
        bool includeStartInfo = true,
        bool includeResultPath = true,
        bool useSpaces = false)
    {
        var baseDirectory = useSpaces
            ? Path.Combine(root, "paths with spaces")
            : root;
        Directory.CreateDirectory(baseDirectory);

        var helperPath = Path.Combine(baseDirectory, "V3dfy.SetupHelper.exe");
        var zipPath = Path.Combine(baseDirectory, "model pack.zip");
        File.WriteAllBytes(helperPath, "helper"u8.ToArray());
        File.WriteAllBytes(zipPath, "zip"u8.ToArray());

        var targetRoot = Path.Combine(
            baseDirectory,
            "runtime root",
            "engine",
            "iw3",
            "nunif",
            "iw3",
            "pretrained_models");
        var workRoot = Path.Combine(baseDirectory, "work root");
        var workPaths = new ModelPackImportWorkPaths(
            RootDirectory: workRoot,
            StagingRoot: Path.Combine(workRoot, "staging root"),
            ResultPath: Path.Combine(workRoot, "helper result.json"),
            LogPath: Path.Combine(workRoot, "helper log.json"));

        var preparation = new ModelPackImportPreparationResult(
            IsValid: isValid,
            Manifest: new ModelPackManifestSummary(
                "depth-anything-metric-outdoor",
                "1.0.0",
                "Depth Anything Metric Outdoor",
                ModelPackManifest.ExpectedTargetRoot,
                ["nunif-d23721f1"],
                "0.1.0-preview.1",
                ModelCount: 1,
                FileCount: 2),
            RuntimeRoot: Path.Combine(baseDirectory, "runtime root"),
            ModelPackZipPath: zipPath,
            HelperExecutablePath: helperPath,
            TargetPretrainedModelsRoot: targetRoot,
            WorkPaths: workPaths,
            FilesToInstall: [CreatePlannedFile(targetRoot)],
            AlreadyInstalledFiles: [],
            Conflicts: [],
            ValidationErrors: isValid ? [] : ["invalid model pack"],
            Warnings: [],
            ElevationRequired: false);

        ElevatedModelPackInstallLaunchRequest? launchRequest = null;
        ProcessStartInfo? startInfo = null;
        if (includeLaunchRequest)
        {
            launchRequest = new ElevatedModelPackInstallLaunchRequest(
                helperPath,
                zipPath,
                targetRoot,
                workPaths.StagingRoot,
                "nunif-d23721f1",
                CurrentV3dfyVersion: "0.1.0-preview.1",
                ResultPath: includeResultPath ? workPaths.ResultPath : null,
                LogPath: workPaths.LogPath);
        }

        if (includeStartInfo && launchRequest is not null)
        {
            startInfo = new ElevatedModelPackInstallLauncher().BuildStartInfo(launchRequest);
        }

        return new ModelPackImportLaunchPreparationResult(
            preparation,
            launchRequest,
            startInfo);
    }

    private static ModelPackPlannedFile CreatePlannedFile(string targetRoot) => new(
        "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
        Path.Combine(
            targetRoot,
            "hub",
            "checkpoints",
            "depth_anything_metric_depth_outdoor.pt"),
        "A".PadLeft(64, 'A'),
        123,
        "checkpoint");

    private static ModelPackElevatedProcessResult SuccessfulProcessResult() => new(
        Started: true,
        ExitCode: 0);

    private void WriteHelperResult(
        string resultPath,
        ModelPackInstallResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        File.WriteAllText(resultPath, JsonSerializer.Serialize(result, JsonOptions));
    }

    private static ModelPackInstallResult CreateHelperResult(
        bool success,
        IReadOnlyList<string>? errors = null) => new(
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
        InstalledFiles: success
            ?
            [
                new ModelPackPlannedFile(
                    "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
                    @"C:\temp\pretrained_models\hub\checkpoints\depth_anything_metric_depth_outdoor.pt",
                    "A".PadLeft(64, 'A'),
                    123,
                    "checkpoint"),
            ]
            : [],
        AlreadyInstalledFiles: [],
        SkippedFiles: [],
        RollbackFilesRemoved: [],
        Errors: errors ?? [],
        Warnings: []);

    private sealed class RecordingElevatedProcessStarter(
        ModelPackElevatedProcessResult result) : IModelPackElevatedProcessStarter
    {
        public int StartCount { get; private set; }

        public ProcessStartInfo? LastStartInfo { get; private set; }

        public Task<ModelPackElevatedProcessResult> StartAndWaitAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken = default)
        {
            StartCount++;
            LastStartInfo = startInfo;
            return Task.FromResult(result);
        }
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
}
