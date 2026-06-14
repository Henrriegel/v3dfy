using V3dfy.Core.Models;
using V3dfy.Infrastructure.ModelPacks;

namespace V3dfy.SetupHelper;

public sealed record InstallerModelPackImportedPack(
    string PackId,
    string DisplayName,
    string AssetFileName,
    string LocalZipPath,
    ModelPackInstallResult InstallResult);

public sealed record InstallerModelPackImportFailure(
    string PackId,
    string DisplayName,
    string AssetFileName,
    string LocalZipPath,
    string Reason,
    IReadOnlyList<string> Errors);

public sealed record InstallerModelPackImportResult(
    IReadOnlyList<InstallerModelPackImportedPack> ImportedPacks,
    IReadOnlyList<InstallerModelPackImportFailure> Failures)
{
    public int SuccessCount => ImportedPacks.Count;

    public int FailureCount => Failures.Count;

    public bool HasFailures => FailureCount > 0;
}

public sealed class InstallerModelPackImportService
{
    private readonly ModelPackInstallPlanner planner;
    private readonly ModelPackInstallExecutor executor;

    public InstallerModelPackImportService(
        ModelPackInstallPlanner? planner = null,
        ModelPackInstallExecutor? executor = null)
    {
        this.planner = planner ?? new ModelPackInstallPlanner();
        this.executor = executor ?? new ModelPackInstallExecutor();
    }

    public async Task<InstallerModelPackImportResult> ImportAsync(
        IReadOnlyList<InstallerModelPackAcquiredFile> acquiredFiles,
        string installRootDirectory,
        string workDirectory,
        string? currentIw3Version,
        string? currentV3dfyVersion,
        ISetupLog log,
        CancellationToken cancellationToken,
        ISetupProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(acquiredFiles);
        ArgumentNullException.ThrowIfNull(log);
        progress ??= NullSetupProgress.Instance;

        var imported = new List<InstallerModelPackImportedPack>();
        var failures = new List<InstallerModelPackImportFailure>();

        if (acquiredFiles.Count == 0)
        {
            return new InstallerModelPackImportResult(imported, failures);
        }

        var targetPretrainedModelsRoot = GetDefaultTargetPretrainedModelsRoot(installRootDirectory);
        var stagingRoot = GetDefaultStagingRoot(workDirectory);

        if (string.IsNullOrWhiteSpace(currentIw3Version))
        {
            foreach (var acquiredFile in acquiredFiles)
            {
                failures.Add(CreateFailure(
                    acquiredFile,
                    "Current bundled iw3 version is required before optional model packs can be imported.",
                    []));
            }

            return new InstallerModelPackImportResult(imported, failures);
        }

        foreach (var acquiredFile in acquiredFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                log.Info($"Validating optional model pack {acquiredFile.DisplayName}: {acquiredFile.LocalZipPath}");
                progress.Report(new SetupProgressEvent(
                    SetupProgressPhase.ValidatingModelPack,
                    $"Validating optional model pack {acquiredFile.DisplayName}.",
                    acquiredFile.AssetFileName));

                var plan = await planner.CreateDryRunPlanAsync(
                    new ModelPackDryRunInstallRequest(
                        acquiredFile.LocalZipPath,
                        targetPretrainedModelsRoot,
                        currentIw3Version,
                        currentV3dfyVersion),
                    cancellationToken);

                if (!plan.IsValid)
                {
                    failures.Add(CreateFailure(
                        acquiredFile,
                        CreatePlanFailureReason(plan),
                        [.. plan.ValidationErrors, .. plan.Conflicts.Select(static conflict => conflict.Message)]));
                    log.Warning($"Optional model pack validation failed: {acquiredFile.DisplayName}.");
                    continue;
                }

                log.Info($"Installing optional model pack {acquiredFile.DisplayName}.");
                progress.Report(new SetupProgressEvent(
                    SetupProgressPhase.InstallingModelPack,
                    $"Installing optional model pack {acquiredFile.DisplayName}.",
                    acquiredFile.AssetFileName,
                    0,
                    Math.Max(1, plan.FilesToInstall.Count + plan.AlreadyInstalledFiles.Count)));

                var installResult = await executor.InstallAsync(
                    plan,
                    stagingRoot,
                    cancellationToken);

                if (!installResult.Success)
                {
                    failures.Add(CreateFailure(
                        acquiredFile,
                        CreateInstallFailureReason(installResult),
                        installResult.Errors));
                    log.Warning($"Optional model pack install failed: {acquiredFile.DisplayName}.");
                    continue;
                }

                imported.Add(new InstallerModelPackImportedPack(
                    acquiredFile.PackId,
                    acquiredFile.DisplayName,
                    acquiredFile.AssetFileName,
                    acquiredFile.LocalZipPath,
                    installResult));
                log.Info($"Installed optional model pack: {acquiredFile.DisplayName}");
                progress.Report(new SetupProgressEvent(
                    SetupProgressPhase.InstallingModelPack,
                    $"Installed optional model pack {acquiredFile.DisplayName}.",
                    acquiredFile.AssetFileName,
                    Math.Max(1, installResult.InstalledFiles.Count + installResult.AlreadyInstalledFiles.Count),
                    Math.Max(1, installResult.InstalledFiles.Count + installResult.AlreadyInstalledFiles.Count)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                failures.Add(CreateFailure(acquiredFile, ex.Message, []));
                log.Warning($"Optional model pack import failed: {acquiredFile.DisplayName}. {ex.Message}");
            }
        }

        return new InstallerModelPackImportResult(imported, failures);
    }

    public static string GetDefaultTargetPretrainedModelsRoot(string installRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRootDirectory);
        var installRoot = Path.GetFullPath(installRootDirectory);
        var targetRoot = Path.GetFullPath(Path.Combine(
            installRoot,
            "engine",
            "iw3",
            "nunif",
            "iw3",
            "pretrained_models"));

        if (!IsSameOrUnderDirectory(installRoot, targetRoot))
        {
            throw new PayloadInstallException(
                $"Resolved model-pack target path escapes the install directory: {targetRoot}");
        }

        return targetRoot;
    }

    public static string GetDefaultStagingRoot(string workDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workDirectory);
        var workRoot = Path.GetFullPath(workDirectory);
        var stagingRoot = Path.GetFullPath(Path.Combine(workRoot, "model-pack-import-staging"));
        if (!IsSameOrUnderDirectory(workRoot, stagingRoot))
        {
            throw new PayloadInstallException(
                $"Resolved model-pack staging path escapes the work directory: {stagingRoot}");
        }

        return stagingRoot;
    }

    private static InstallerModelPackImportFailure CreateFailure(
        InstallerModelPackAcquiredFile acquiredFile,
        string reason,
        IReadOnlyList<string> errors) =>
        new(
            acquiredFile.PackId,
            acquiredFile.DisplayName,
            acquiredFile.AssetFileName,
            acquiredFile.LocalZipPath,
            reason,
            errors);

    private static string CreatePlanFailureReason(ModelPackDryRunInstallPlan plan)
    {
        if (plan.ValidationErrors.Count > 0)
        {
            return string.Join(" ", plan.ValidationErrors);
        }

        return plan.Conflicts.Count > 0
            ? string.Join(" ", plan.Conflicts.Select(static conflict => conflict.Message))
            : "Model pack validation failed.";
    }

    private static string CreateInstallFailureReason(ModelPackInstallResult result) =>
        result.Errors.Count > 0
            ? string.Join(" ", result.Errors)
            : "Model pack install failed.";

    private static bool IsSameOrUnderDirectory(string rootDirectory, string candidatePath)
    {
        var rootPrefix = NormalizeDirectoryPrefix(rootDirectory);
        var candidate = Path.GetFullPath(candidatePath);
        return candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectoryPrefix(string directory)
    {
        var fullPath = Path.GetFullPath(directory);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }
}
