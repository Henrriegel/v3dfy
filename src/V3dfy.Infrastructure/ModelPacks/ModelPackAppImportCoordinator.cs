using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public enum ModelPackAppImportStatus
{
    Canceled,
    ConfirmationCanceled,
    Invalid,
    Failed,
    Succeeded,
}

public sealed record ModelPackAppImportRequest(
    string RuntimeRoot,
    string HelperExecutablePath,
    string CurrentIw3Version,
    string? CurrentV3dfyVersion = null)
{
    public Func<ModelPackImportPreparationResult, CancellationToken, Task>? BeforeExecutionAsync { get; init; }

    public Func<CancellationToken, Task>? RefreshAfterSuccessfulImportAsync { get; init; }
}

public sealed record ModelPackAppImportResult(
    ModelPackAppImportStatus Status,
    string? SelectedModelPackZipPath,
    ModelPackImportLaunchPreparationResult? LaunchPreparation,
    ModelPackImportExecutionResult? ExecutionResult,
    bool AppRefreshCompleted)
{
    public bool Success => Status == ModelPackAppImportStatus.Succeeded;

    public bool Canceled => Status is
        ModelPackAppImportStatus.Canceled or
        ModelPackAppImportStatus.ConfirmationCanceled;

    public bool ConfirmationCanceled => Status == ModelPackAppImportStatus.ConfirmationCanceled;

    public IReadOnlyList<string> Errors => Status switch
    {
        ModelPackAppImportStatus.Invalid =>
            LaunchPreparation?.Preparation.ValidationErrors ?? [],
        ModelPackAppImportStatus.Failed =>
            ExecutionResult?.Errors ?? [],
        _ => [],
    };

    public IReadOnlyList<string> Warnings =>
        ExecutionResult?.Warnings ??
        LaunchPreparation?.Preparation.Warnings ??
        [];
}

public sealed class ModelPackAppImportCoordinator
{
    private readonly IModelPackFilePicker filePicker;
    private readonly IModelPackImportPreparationService preparationService;
    private readonly IModelPackImportExecutionService executionService;
    private readonly IModelPackImportConfirmationService confirmationService;

    public ModelPackAppImportCoordinator(
        IModelPackFilePicker filePicker,
        IModelPackImportPreparationService? preparationService = null,
        IModelPackImportExecutionService? executionService = null,
        IModelPackImportConfirmationService? confirmationService = null)
    {
        this.filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        this.preparationService = preparationService ?? new ModelPackImportOrchestrator();
        this.executionService = executionService ?? new ModelPackImportExecutionService();
        this.confirmationService = confirmationService ?? AcceptingModelPackImportConfirmationService.Instance;
    }

    public async Task<ModelPackAppImportResult> ImportAsync(
        ModelPackAppImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RuntimeRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HelperExecutablePath);

        var selectedZipPath = await filePicker.PickModelPackZipAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(selectedZipPath))
        {
            return new ModelPackAppImportResult(
                ModelPackAppImportStatus.Canceled,
                SelectedModelPackZipPath: null,
                LaunchPreparation: null,
                ExecutionResult: null,
                AppRefreshCompleted: false);
        }

        if (string.IsNullOrWhiteSpace(request.CurrentIw3Version))
        {
            return new ModelPackAppImportResult(
                ModelPackAppImportStatus.Invalid,
                SelectedModelPackZipPath: selectedZipPath,
                LaunchPreparation: CreateInvalidPreparation(
                    request,
                    selectedZipPath,
                    "Bundled iw3 version is not known. Refresh the engine status after installing the engine bundle."),
                ExecutionResult: null,
                AppRefreshCompleted: false);
        }

        var launchPreparation = await preparationService.PrepareImportAsync(
            new ModelPackImportPrepareRequest(
                request.RuntimeRoot,
                selectedZipPath,
                request.HelperExecutablePath,
                request.CurrentIw3Version,
                request.CurrentV3dfyVersion),
            cancellationToken);

        if (!launchPreparation.CanLaunch)
        {
            return new ModelPackAppImportResult(
                ModelPackAppImportStatus.Invalid,
                selectedZipPath,
                launchPreparation,
                ExecutionResult: null,
                AppRefreshCompleted: false);
        }

        if (!File.Exists(request.HelperExecutablePath))
        {
            return new ModelPackAppImportResult(
                ModelPackAppImportStatus.Invalid,
                selectedZipPath,
                CreateInvalidPreparation(
                    request,
                    selectedZipPath,
                    CreateMissingHelperExecutableError(request.HelperExecutablePath)),
                ExecutionResult: null,
                AppRefreshCompleted: false);
        }

        if (request.BeforeExecutionAsync is not null)
        {
            await request.BeforeExecutionAsync(launchPreparation.Preparation, cancellationToken);
        }

        var confirmationPrompt = ModelPackImportConfirmationFormatter.CreatePrompt(
            launchPreparation.Preparation);
        var confirmed = await confirmationService.ConfirmAsync(
            confirmationPrompt,
            cancellationToken);
        if (!confirmed)
        {
            return new ModelPackAppImportResult(
                ModelPackAppImportStatus.ConfirmationCanceled,
                selectedZipPath,
                launchPreparation,
                ExecutionResult: null,
                AppRefreshCompleted: false);
        }

        var executionResult = await executionService.ExecuteAsync(
            launchPreparation,
            cancellationToken);
        if (!executionResult.Success)
        {
            return new ModelPackAppImportResult(
                ModelPackAppImportStatus.Failed,
                selectedZipPath,
                launchPreparation,
                executionResult,
                AppRefreshCompleted: false);
        }

        var appRefreshCompleted = false;
        if (request.RefreshAfterSuccessfulImportAsync is not null)
        {
            await request.RefreshAfterSuccessfulImportAsync(cancellationToken);
            appRefreshCompleted = true;
        }

        return new ModelPackAppImportResult(
            ModelPackAppImportStatus.Succeeded,
            selectedZipPath,
            launchPreparation,
            executionResult,
            appRefreshCompleted);
    }

    private static ModelPackImportLaunchPreparationResult CreateInvalidPreparation(
        ModelPackAppImportRequest request,
        string selectedZipPath,
        string validationError)
    {
        var workPaths = new ModelPackImportWorkPaths(
            RootDirectory: string.Empty,
            StagingRoot: string.Empty,
            ResultPath: string.Empty,
            LogPath: null);
        var preparation = new ModelPackImportPreparationResult(
            IsValid: false,
            Manifest: null,
            RuntimeRoot: request.RuntimeRoot,
            ModelPackZipPath: selectedZipPath,
            HelperExecutablePath: request.HelperExecutablePath,
            TargetPretrainedModelsRoot: string.Empty,
            WorkPaths: workPaths,
            FilesToInstall: [],
            AlreadyInstalledFiles: [],
            Conflicts: [],
            ValidationErrors: [validationError],
            Warnings: [],
            ElevationRequired: false);

        return new ModelPackImportLaunchPreparationResult(
            preparation,
            LaunchRequest: null,
            StartInfo: null);
    }

    internal static string CreateMissingHelperExecutableError(string helperExecutablePath) =>
        $"Model pack helper executable was not found: {helperExecutablePath}. Reinstall or repair v3dfy before importing model packs.";
}
