namespace V3dfy.Core.Models;

public sealed record ModelPackImportPrepareRequest(
    string RuntimeRoot,
    string ModelPackZipPath,
    string HelperExecutablePath,
    string CurrentIw3Version,
    string? CurrentV3dfyVersion = null);

public sealed record ModelPackImportWorkPaths(
    string RootDirectory,
    string StagingRoot,
    string ResultPath,
    string? LogPath);

public sealed record ModelPackImportPreparationResult(
    bool IsValid,
    ModelPackManifestSummary? Manifest,
    string RuntimeRoot,
    string ModelPackZipPath,
    string HelperExecutablePath,
    string TargetPretrainedModelsRoot,
    ModelPackImportWorkPaths WorkPaths,
    IReadOnlyList<ModelPackPlannedFile> FilesToInstall,
    IReadOnlyList<ModelPackPlannedFile> AlreadyInstalledFiles,
    IReadOnlyList<ModelPackFileConflict> Conflicts,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> Warnings,
    bool ElevationRequired);

public sealed record ModelPackImportCompletionResult(
    ModelPackInstallResult HelperResult,
    bool RefreshNeeded,
    bool RefreshCompleted);

public sealed record ModelPackElevatedProcessResult(
    bool Started,
    int? ExitCode,
    string? ErrorMessage = null);

public sealed record ModelPackImportExecutionResult(
    bool Success,
    bool HelperProcessStarted,
    int? ExitCode,
    ModelPackInstallResult? HelperResult,
    bool RefreshNeeded,
    bool RefreshCompleted,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string? ResultPath,
    string? LogPath);
