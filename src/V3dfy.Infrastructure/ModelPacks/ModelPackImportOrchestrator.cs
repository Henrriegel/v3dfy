using System.Diagnostics;
using V3dfy.Core.Models;
using V3dfy.Infrastructure.Paths;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed record ModelPackImportLaunchPreparationResult(
    ModelPackImportPreparationResult Preparation,
    ElevatedModelPackInstallLaunchRequest? LaunchRequest,
    ProcessStartInfo? StartInfo)
{
    public bool CanLaunch => Preparation.IsValid && LaunchRequest is not null && StartInfo is not null;
}

public sealed class ModelPackImportOrchestrator : IModelPackImportPreparationService
{
    private readonly ModelPackInstallPlanner planner;
    private readonly ElevatedModelPackInstallLauncher launcher;
    private readonly IModelPackImportWorkPathProvider workPathProvider;
    private readonly ModelPackHelperResultReader helperResultReader;
    private readonly IModelPackInventoryRefreshHook inventoryRefreshHook;

    public ModelPackImportOrchestrator(
        ModelPackInstallPlanner? planner = null,
        ElevatedModelPackInstallLauncher? launcher = null,
        IModelPackImportWorkPathProvider? workPathProvider = null,
        ModelPackHelperResultReader? helperResultReader = null,
        IModelPackInventoryRefreshHook? inventoryRefreshHook = null)
    {
        this.planner = planner ?? new ModelPackInstallPlanner();
        this.launcher = launcher ?? new ElevatedModelPackInstallLauncher();
        this.workPathProvider = workPathProvider ?? new LocalAppDataModelPackImportWorkPathProvider();
        this.helperResultReader = helperResultReader ?? new ModelPackHelperResultReader();
        this.inventoryRefreshHook = inventoryRefreshHook ?? NullModelPackInventoryRefreshHook.Instance;
    }

    public async Task<ModelPackImportLaunchPreparationResult> PrepareImportAsync(
        ModelPackImportPrepareRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RuntimeRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelPackZipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HelperExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CurrentIw3Version);

        var runtimeRoot = ModelPackPathRules.NormalizeFullPath(request.RuntimeRoot);
        var modelPackZipPath = ModelPackPathRules.NormalizeFullPath(request.ModelPackZipPath);
        var helperExecutablePath = ModelPackPathRules.NormalizeFullPath(request.HelperExecutablePath);
        var toolPaths = new InternalToolPathResolver(runtimeRoot).Resolve();
        var targetRoot = ModelPackPathRules.NormalizeFullPath(toolPaths.ModelsDirectory);
        var workPaths = NormalizeWorkPaths(workPathProvider.CreateWorkPaths());

        var validationErrors = new List<string>();
        var warnings = new List<string>();

        ValidateWorkPaths(workPaths, validationErrors);
        if (validationErrors.Count == 0)
        {
            Directory.CreateDirectory(workPaths.RootDirectory);
            Directory.CreateDirectory(workPaths.StagingRoot);
        }

        var plan = await planner.CreateDryRunPlanAsync(
            new ModelPackDryRunInstallRequest(
                modelPackZipPath,
                targetRoot,
                request.CurrentIw3Version,
                request.CurrentV3dfyVersion),
            cancellationToken);

        validationErrors.AddRange(plan.ValidationErrors);
        warnings.AddRange(plan.Warnings);

        ElevatedModelPackInstallLaunchRequest? launchRequest = null;
        ProcessStartInfo? startInfo = null;
        var isValid = plan.IsValid && validationErrors.Count == 0;
        if (isValid)
        {
            launchRequest = new ElevatedModelPackInstallLaunchRequest(
                helperExecutablePath,
                modelPackZipPath,
                targetRoot,
                workPaths.StagingRoot,
                request.CurrentIw3Version,
                request.CurrentV3dfyVersion,
                workPaths.ResultPath,
                workPaths.LogPath);

            if (!File.Exists(helperExecutablePath))
            {
                validationErrors.Add(ModelPackAppImportCoordinator.CreateMissingHelperExecutableError(
                    helperExecutablePath));
                launchRequest = null;
                isValid = false;
            }
            else
            {
                try
                {
                    startInfo = launcher.BuildStartInfo(launchRequest);
                }
                catch (ArgumentException exception)
                {
                    validationErrors.Add(exception.Message);
                    launchRequest = null;
                    startInfo = null;
                    isValid = false;
                }
            }
        }

        var preparation = new ModelPackImportPreparationResult(
            IsValid: isValid,
            Manifest: plan.Manifest,
            RuntimeRoot: runtimeRoot,
            ModelPackZipPath: modelPackZipPath,
            HelperExecutablePath: helperExecutablePath,
            TargetPretrainedModelsRoot: targetRoot,
            WorkPaths: workPaths,
            FilesToInstall: plan.FilesToInstall,
            AlreadyInstalledFiles: plan.AlreadyInstalledFiles,
            Conflicts: plan.Conflicts,
            ValidationErrors: validationErrors,
            Warnings: warnings,
            ElevationRequired: plan.ElevationWouldBeRequired);

        return new ModelPackImportLaunchPreparationResult(
            preparation,
            launchRequest,
            startInfo);
    }

    public async Task<ModelPackImportCompletionResult> CompleteAfterHelperResultAsync(
        string resultPath,
        CancellationToken cancellationToken = default)
    {
        var helperResult = await helperResultReader.ReadAsync(resultPath, cancellationToken);
        if (!helperResult.Success)
        {
            return new ModelPackImportCompletionResult(
                helperResult,
                RefreshNeeded: false,
                RefreshCompleted: false);
        }

        await inventoryRefreshHook.RefreshAsync(cancellationToken);
        return new ModelPackImportCompletionResult(
            helperResult,
            RefreshNeeded: true,
            RefreshCompleted: true);
    }

    private static ModelPackImportWorkPaths NormalizeWorkPaths(ModelPackImportWorkPaths workPaths) => new(
        RootDirectory: ModelPackPathRules.NormalizeFullPath(workPaths.RootDirectory),
        StagingRoot: ModelPackPathRules.NormalizeFullPath(workPaths.StagingRoot),
        ResultPath: ModelPackPathRules.NormalizeFullPath(workPaths.ResultPath),
        LogPath: string.IsNullOrWhiteSpace(workPaths.LogPath)
            ? null
            : ModelPackPathRules.NormalizeFullPath(workPaths.LogPath));

    private static void ValidateWorkPaths(
        ModelPackImportWorkPaths workPaths,
        ICollection<string> validationErrors)
    {
        if (ModelPackPathRules.IsUnderProgramFiles(workPaths.RootDirectory))
        {
            validationErrors.Add(
                "Model pack temporary import paths must not be under Program Files.");
            return;
        }

        ValidateWorkPathInsideRoot(workPaths.RootDirectory, workPaths.StagingRoot, "staging root", validationErrors);
        ValidateWorkPathInsideRoot(workPaths.RootDirectory, workPaths.ResultPath, "result path", validationErrors);
        if (!string.IsNullOrWhiteSpace(workPaths.LogPath))
        {
            ValidateWorkPathInsideRoot(workPaths.RootDirectory, workPaths.LogPath, "log path", validationErrors);
        }
    }

    private static void ValidateWorkPathInsideRoot(
        string rootDirectory,
        string path,
        string description,
        ICollection<string> validationErrors)
    {
        if (!ModelPackPathRules.IsSameOrUnderDirectory(rootDirectory, path))
        {
            validationErrors.Add($"Model pack {description} must be inside the import work root.");
        }
    }
}
