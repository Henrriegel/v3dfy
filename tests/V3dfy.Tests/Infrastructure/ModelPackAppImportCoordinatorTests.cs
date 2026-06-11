using System.Diagnostics;
using V3dfy.Core.Models;
using V3dfy.Infrastructure.ModelPacks;

namespace V3dfy.Tests.Infrastructure;

public sealed class ModelPackAppImportCoordinatorTests
{
    [Fact]
    public async Task PickerCancellation_ReturnsCanceledAndDoesNotPrepareOrExecute()
    {
        var picker = new RecordingModelPackFilePicker(null);
        var preparation = new RecordingPreparationService(CreateValidLaunchPreparation(ModelPackZipPath("model-pack.zip")));
        var execution = new RecordingExecutionService(CreateSuccessfulExecutionResult());
        var confirmation = new RecordingConfirmationService(confirm: true);
        var refreshCount = 0;

        var result = await CreateCoordinator(picker, preparation, execution, confirmation).ImportAsync(
            CreateRequest(refresh: _ =>
            {
                refreshCount++;
                return Task.CompletedTask;
            }));

        Assert.True(result.Canceled);
        Assert.False(result.Success);
        Assert.Equal(1, picker.PickCount);
        Assert.Equal(0, preparation.PrepareCount);
        Assert.Equal(0, execution.ExecuteCount);
        Assert.Equal(0, confirmation.ConfirmCount);
        Assert.Equal(0, refreshCount);
        Assert.Null(result.SelectedModelPackZipPath);
    }

    [Fact]
    public async Task InvalidPreparationSurfacesValidationErrorsAndDoesNotExecuteHelper()
    {
        var zipPath = ModelPackZipPath("invalid-model-pack.zip");
        var preparation = new RecordingPreparationService(CreateInvalidLaunchPreparation(
            zipPath,
            "MODEL_PACK.json is missing."));
        var execution = new RecordingExecutionService(CreateSuccessfulExecutionResult());
        var confirmation = new RecordingConfirmationService(confirm: true);

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            preparation,
            execution,
            confirmation).ImportAsync(CreateRequest());

        Assert.Equal(ModelPackAppImportStatus.Invalid, result.Status);
        Assert.False(result.Success);
        Assert.Contains("MODEL_PACK.json is missing.", result.Errors);
        Assert.Equal(1, preparation.PrepareCount);
        Assert.Equal(0, confirmation.ConfirmCount);
        Assert.Equal(0, execution.ExecuteCount);
    }

    [Fact]
    public async Task InvalidPreparationReturnsValidationErrorsEvenWhenHelperIsMissing()
    {
        var zipPath = ModelPackZipPath("invalid-model-pack.zip");
        var preparation = new RecordingPreparationService(CreateInvalidLaunchPreparation(
            zipPath,
            "Model pack ZIP is missing MODEL_PACK.json."));
        var execution = new RecordingExecutionService(CreateSuccessfulExecutionResult());
        var confirmation = new RecordingConfirmationService(confirm: true);

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            preparation,
            execution,
            confirmation).ImportAsync(CreateRequest(createHelperFile: false));

        Assert.Equal(ModelPackAppImportStatus.Invalid, result.Status);
        Assert.Contains("Model pack ZIP is missing MODEL_PACK.json.", result.Errors);
        Assert.DoesNotContain(
            result.Errors,
            error => error.Contains("helper executable", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, preparation.PrepareCount);
        Assert.Equal(0, confirmation.ConfirmCount);
        Assert.Equal(0, execution.ExecuteCount);
    }

    [Fact]
    public async Task ValidPreparationCallsExecutionService()
    {
        var zipPath = ModelPackZipPath("model-pack.zip");
        var preparationResult = CreateValidLaunchPreparation(zipPath);
        var preparation = new RecordingPreparationService(preparationResult);
        var execution = new RecordingExecutionService(CreateSuccessfulExecutionResult());
        var confirmation = new RecordingConfirmationService(confirm: true);

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            preparation,
            execution,
            confirmation).ImportAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.Equal(1, preparation.PrepareCount);
        Assert.Equal(1, confirmation.ConfirmCount);
        Assert.Equal(1, execution.ExecuteCount);
        Assert.Same(preparationResult, execution.LastPreparation);
    }

    [Fact]
    public async Task ConfirmationIsRequestedAfterValidPreparation()
    {
        var zipPath = ModelPackZipPath("model-pack.zip");
        var preparation = new RecordingPreparationService(CreateValidLaunchPreparation(zipPath));
        var confirmation = new RecordingConfirmationService(confirm: true);

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            preparation,
            new RecordingExecutionService(CreateSuccessfulExecutionResult()),
            confirmation).ImportAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.Equal(1, preparation.PrepareCount);
        Assert.Equal(1, confirmation.ConfirmCount);
        Assert.NotNull(confirmation.LastPrompt);
        Assert.Same(preparation.Result.Preparation, confirmation.LastPrompt.Preparation);
    }

    [Fact]
    public async Task UserCancelsConfirmationAndHelperExecutionIsNotCalled()
    {
        var zipPath = ModelPackZipPath("model-pack.zip");
        var execution = new RecordingExecutionService(CreateSuccessfulExecutionResult());
        var confirmation = new RecordingConfirmationService(confirm: false);

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            new RecordingPreparationService(CreateValidLaunchPreparation(zipPath)),
            execution,
            confirmation).ImportAsync(CreateRequest());

        Assert.Equal(ModelPackAppImportStatus.ConfirmationCanceled, result.Status);
        Assert.True(result.Canceled);
        Assert.False(result.Success);
        Assert.Equal(1, confirmation.ConfirmCount);
        Assert.Equal(0, execution.ExecuteCount);
    }

    [Fact]
    public void ConfirmationSummaryIncludesPackTargetCountsAndAdminWarning()
    {
        var preparation = CreateValidLaunchPreparation(
            ModelPackZipPath("model-pack.zip"),
            useSpaces: true).Preparation;

        var prompt = ModelPackImportConfirmationFormatter.CreatePrompt(preparation);

        Assert.Contains("Depth Anything Metric Outdoor", prompt.EnglishMessage);
        Assert.Contains(preparation.TargetPretrainedModelsRoot, prompt.EnglishMessage);
        Assert.Contains("Files to install: 1", prompt.EnglishMessage);
        Assert.Contains("Already installed files: 0", prompt.EnglishMessage);
        Assert.Contains("Conflicts: 0", prompt.EnglishMessage);
        Assert.Contains("Administrator/UAC permission: required", prompt.EnglishMessage);
        Assert.Contains("supports or maps", prompt.EnglishMessage);
        Assert.Contains("Permiso de administrador/UAC", prompt.SpanishMessage);
    }

    [Fact]
    public async Task SuccessfulExecutionTriggersAppRefreshCallback()
    {
        var refreshCount = 0;
        var zipPath = ModelPackZipPath("model-pack.zip");

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            new RecordingPreparationService(CreateValidLaunchPreparation(zipPath)),
            new RecordingExecutionService(CreateSuccessfulExecutionResult()),
            new RecordingConfirmationService(confirm: true)).ImportAsync(
            CreateRequest(refresh: _ =>
            {
                refreshCount++;
                return Task.CompletedTask;
            }));

        Assert.True(result.Success);
        Assert.True(result.AppRefreshCompleted);
        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public async Task FailedExecutionSurfacesErrorsAndDoesNotReportSuccessOrRefresh()
    {
        var refreshCount = 0;
        var zipPath = ModelPackZipPath("model-pack.zip");

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            new RecordingPreparationService(CreateValidLaunchPreparation(zipPath)),
            new RecordingExecutionService(CreateFailedExecutionResult("UAC was cancelled.")),
            new RecordingConfirmationService(confirm: true)).ImportAsync(
            CreateRequest(refresh: _ =>
            {
                refreshCount++;
                return Task.CompletedTask;
            }));

        Assert.Equal(ModelPackAppImportStatus.Failed, result.Status);
        Assert.False(result.Success);
        Assert.False(result.AppRefreshCompleted);
        Assert.Contains("UAC was cancelled.", result.Errors);
        Assert.Equal(0, refreshCount);
    }

    [Fact]
    public async Task PathsWithSpacesArePassedToPreparationAndExecution()
    {
        var zipPath = ModelPackZipPath("model pack folder", "model pack.zip");
        var runtimeRoot = RuntimeRoot(useSpaces: true);
        var preparationResult = CreateValidLaunchPreparation(zipPath, useSpaces: true);
        var preparation = new RecordingPreparationService(preparationResult);
        var execution = new RecordingExecutionService(CreateSuccessfulExecutionResult());

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            preparation,
            execution,
            new RecordingConfirmationService(confirm: true)).ImportAsync(CreateRequest(
            runtimeRoot: runtimeRoot,
            helperPath: HelperPath(runtimeRoot)));

        Assert.True(result.Success);
        Assert.NotNull(preparation.LastRequest);
        Assert.Contains(" ", preparation.LastRequest.ModelPackZipPath);
        Assert.Contains(" ", preparation.LastRequest.RuntimeRoot);
        Assert.Contains(" ", preparation.LastRequest.HelperExecutablePath);
        Assert.Equal(zipPath, preparation.LastRequest.ModelPackZipPath);
        Assert.Same(preparationResult, execution.LastPreparation);
    }

    [Fact]
    public async Task CoordinatorUsesFakeExecutionServiceAndDoesNotLaunchRealUac()
    {
        var execution = new RecordingExecutionService(CreateSuccessfulExecutionResult());
        var zipPath = ModelPackZipPath("model-pack.zip");

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            new RecordingPreparationService(CreateValidLaunchPreparation(zipPath)),
            execution,
            new RecordingConfirmationService(confirm: true)).ImportAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.Equal(1, execution.ExecuteCount);
        Assert.IsType<RecordingExecutionService>(execution);
    }

    [Fact]
    public async Task MissingIw3VersionReturnsInvalidResultWithoutPreparationOrExecution()
    {
        var zipPath = ModelPackZipPath("model-pack.zip");
        var preparation = new RecordingPreparationService(CreateValidLaunchPreparation(zipPath));
        var execution = new RecordingExecutionService(CreateSuccessfulExecutionResult());
        var confirmation = new RecordingConfirmationService(confirm: true);

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            preparation,
            execution,
            confirmation).ImportAsync(CreateRequest(currentIw3Version: string.Empty));

        Assert.Equal(ModelPackAppImportStatus.Invalid, result.Status);
        Assert.False(result.Success);
        Assert.Contains(
            result.Errors,
            error => error.Contains("iw3 version", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, preparation.PrepareCount);
        Assert.Equal(0, confirmation.ConfirmCount);
        Assert.Equal(0, execution.ExecuteCount);
    }

    [Fact]
    public async Task HelperExecutableMissingIsSurfacedClearlyWithoutPreparationConfirmationOrExecution()
    {
        var zipPath = ModelPackZipPath("model-pack.zip");
        var missingHelperPath = Path.Combine(RuntimeRoot(useSpaces: false), "missing-helper.exe");
        var preparation = new RecordingPreparationService(CreateValidLaunchPreparation(zipPath));
        var execution = new RecordingExecutionService(CreateSuccessfulExecutionResult());
        var confirmation = new RecordingConfirmationService(confirm: true);

        var result = await CreateCoordinator(
            new RecordingModelPackFilePicker(zipPath),
            preparation,
            execution,
            confirmation).ImportAsync(CreateRequest(
            helperPath: missingHelperPath,
            createHelperFile: false));

        Assert.Equal(ModelPackAppImportStatus.Invalid, result.Status);
        Assert.Contains(
            result.Errors,
            error => error.Contains("helper executable was not found", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, preparation.PrepareCount);
        Assert.Equal(0, confirmation.ConfirmCount);
        Assert.Equal(0, execution.ExecuteCount);
    }

    private static ModelPackAppImportCoordinator CreateCoordinator(
        RecordingModelPackFilePicker picker,
        RecordingPreparationService preparation,
        RecordingExecutionService execution,
        RecordingConfirmationService confirmation) => new(
        picker,
        preparation,
        execution,
        confirmation);

    private static ModelPackAppImportRequest CreateRequest(
        string? runtimeRoot = null,
        string? helperPath = null,
        string currentIw3Version = "nunif-d23721f1",
        Func<CancellationToken, Task>? refresh = null,
        bool createHelperFile = true)
    {
        runtimeRoot ??= RuntimeRoot(useSpaces: false);
        helperPath ??= HelperPath(runtimeRoot);
        if (createHelperFile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(helperPath)!);
            File.WriteAllBytes(helperPath, "helper"u8.ToArray());
        }

        return new ModelPackAppImportRequest(
            RuntimeRoot: runtimeRoot,
            HelperExecutablePath: helperPath,
            CurrentIw3Version: currentIw3Version,
            CurrentV3dfyVersion: "0.1.0-preview.1")
        {
            RefreshAfterSuccessfulImportAsync = refresh,
        };
    }

    private static ModelPackImportLaunchPreparationResult CreateValidLaunchPreparation(
        string zipPath,
        bool useSpaces = false)
    {
        var runtimeRoot = RuntimeRoot(useSpaces);
        var targetRoot = Path.Combine(
            runtimeRoot,
            "engine",
            "iw3",
            "nunif",
            "iw3",
            "pretrained_models");
        var workRoot = WorkRoot(useSpaces);
        var workPaths = new ModelPackImportWorkPaths(
            workRoot,
            Path.Combine(workRoot, "staging"),
            Path.Combine(workRoot, "result.json"),
            Path.Combine(workRoot, "helper.log"));
        var launchRequest = new ElevatedModelPackInstallLaunchRequest(
            Path.Combine(runtimeRoot, "V3dfy.SetupHelper.exe"),
            zipPath,
            targetRoot,
            workPaths.StagingRoot,
            "nunif-d23721f1",
            CurrentV3dfyVersion: "0.1.0-preview.1",
            ResultPath: workPaths.ResultPath,
            LogPath: workPaths.LogPath);

        return new ModelPackImportLaunchPreparationResult(
            new ModelPackImportPreparationResult(
                IsValid: true,
                Manifest: CreateManifestSummary(),
                RuntimeRoot: runtimeRoot,
                ModelPackZipPath: zipPath,
                HelperExecutablePath: launchRequest.HelperExecutablePath,
                TargetPretrainedModelsRoot: targetRoot,
                WorkPaths: workPaths,
                FilesToInstall: [CreatePlannedFile(targetRoot)],
                AlreadyInstalledFiles: [],
                Conflicts: [],
                ValidationErrors: [],
                Warnings: [],
                ElevationRequired: useSpaces),
            launchRequest,
            new ProcessStartInfo
            {
                FileName = launchRequest.HelperExecutablePath,
                UseShellExecute = true,
                Verb = "runas",
            });
    }

    private static ModelPackImportLaunchPreparationResult CreateInvalidLaunchPreparation(
        string zipPath,
        string validationError)
    {
        var workPaths = new ModelPackImportWorkPaths(
            WorkRoot(useSpaces: false),
            Path.Combine(WorkRoot(useSpaces: false), "staging"),
            Path.Combine(WorkRoot(useSpaces: false), "result.json"),
            null);
        var runtimeRoot = RuntimeRoot(useSpaces: false);
        var targetRoot = TargetRoot(runtimeRoot);
        return new ModelPackImportLaunchPreparationResult(
            new ModelPackImportPreparationResult(
                IsValid: false,
                Manifest: null,
                RuntimeRoot: runtimeRoot,
                ModelPackZipPath: zipPath,
                HelperExecutablePath: HelperPath(runtimeRoot),
                TargetPretrainedModelsRoot: targetRoot,
                WorkPaths: workPaths,
                FilesToInstall: [],
                AlreadyInstalledFiles: [],
                Conflicts: [],
                ValidationErrors: [validationError],
                Warnings: [],
                ElevationRequired: false),
            LaunchRequest: null,
            StartInfo: null);
    }

    private static ModelPackImportExecutionResult CreateSuccessfulExecutionResult() => new(
        Success: true,
        HelperProcessStarted: true,
        ExitCode: 0,
        HelperResult: CreateInstallResult(success: true),
        RefreshNeeded: true,
        RefreshCompleted: true,
        Errors: [],
        Warnings: [],
        ResultPath: ResultPath(),
        LogPath: HelperLogPath());

    private static ModelPackImportExecutionResult CreateFailedExecutionResult(string error) => new(
        Success: false,
        HelperProcessStarted: false,
        ExitCode: null,
        HelperResult: null,
        RefreshNeeded: false,
        RefreshCompleted: false,
        Errors: [error],
        Warnings: [],
        ResultPath: ResultPath(),
        LogPath: HelperLogPath());

    private static ModelPackInstallResult CreateInstallResult(bool success) => new(
        Success: success,
        Manifest: CreateManifestSummary(),
        ModelPackZipPath: ModelPackZipPath("model-pack.zip"),
        TargetPretrainedModelsRoot: TargetRoot(RuntimeRoot(useSpaces: false)),
        StagingPath: Path.Combine(WorkRoot(useSpaces: false), "staging"),
        InstalledFiles: success ? [CreatePlannedFile(TargetRoot(RuntimeRoot(useSpaces: false)))] : [],
        AlreadyInstalledFiles: [],
        SkippedFiles: [],
        RollbackFilesRemoved: [],
        Errors: [],
        Warnings: []);

    private static ModelPackManifestSummary CreateManifestSummary() => new(
        PackId: "depth-anything-metric-outdoor",
        PackVersion: "1.0.0",
        DisplayName: "Depth Anything Metric Outdoor",
        TargetRoot: ModelPackManifest.ExpectedTargetRoot,
        CompatibleIw3Versions: ["nunif-d23721f1"],
        MinV3dfyVersion: "0.1.0-preview.1",
        ModelCount: 1,
        FileCount: 1);

    private static ModelPackPlannedFile CreatePlannedFile(string targetRoot) => new(
        RelativePath: "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
        DestinationPath: Path.Combine(
            targetRoot,
            "hub",
            "checkpoints",
            "depth_anything_metric_depth_outdoor.pt"),
        Sha256: "A".PadLeft(64, 'A'),
        SizeBytes: 123,
        Role: "checkpoint");

    private static string ModelPackZipPath(params string[] segments) =>
        TestPaths.SourceRoot(["model-pack-app-import", .. segments]);

    private static string RuntimeRoot(bool useSpaces) =>
        TestPaths.RuntimeRoot(useSpaces ? "model pack app import" : "model-pack-app-import", "runtime");

    private static string WorkRoot(bool useSpaces) =>
        TestPaths.AppDataRoot(useSpaces ? "model pack app import" : "model-pack-app-import", "work");

    private static string TargetRoot(string runtimeRoot) =>
        Path.Combine(runtimeRoot, "engine", "iw3", "nunif", "iw3", "pretrained_models");

    private static string HelperPath(string runtimeRoot) =>
        Path.Combine(runtimeRoot, "V3dfy.SetupHelper.exe");

    private static string ResultPath() =>
        Path.Combine(WorkRoot(useSpaces: false), "result.json");

    private static string HelperLogPath() =>
        Path.Combine(WorkRoot(useSpaces: false), "helper.log");

    private sealed class RecordingModelPackFilePicker(string? selectedPath) : IModelPackFilePicker
    {
        public int PickCount { get; private set; }

        public Task<string?> PickModelPackZipAsync(CancellationToken cancellationToken = default)
        {
            PickCount++;
            return Task.FromResult(selectedPath);
        }
    }

    private sealed class RecordingPreparationService(
        ModelPackImportLaunchPreparationResult result) : IModelPackImportPreparationService
    {
        public ModelPackImportLaunchPreparationResult Result => result;

        public int PrepareCount { get; private set; }

        public ModelPackImportPrepareRequest? LastRequest { get; private set; }

        public Task<ModelPackImportLaunchPreparationResult> PrepareImportAsync(
            ModelPackImportPrepareRequest request,
            CancellationToken cancellationToken = default)
        {
            PrepareCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingExecutionService(
        ModelPackImportExecutionResult result) : IModelPackImportExecutionService
    {
        public int ExecuteCount { get; private set; }

        public ModelPackImportLaunchPreparationResult? LastPreparation { get; private set; }

        public Task<ModelPackImportExecutionResult> ExecuteAsync(
            ModelPackImportLaunchPreparationResult preparation,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            LastPreparation = preparation;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingConfirmationService(bool confirm) : IModelPackImportConfirmationService
    {
        public int ConfirmCount { get; private set; }

        public ModelPackImportConfirmationPrompt? LastPrompt { get; private set; }

        public Task<bool> ConfirmAsync(
            ModelPackImportConfirmationPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            ConfirmCount++;
            LastPrompt = prompt;
            return Task.FromResult(confirm);
        }
    }
}
