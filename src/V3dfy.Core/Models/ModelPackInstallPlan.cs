namespace V3dfy.Core.Models;

public sealed record ModelPackDryRunInstallRequest(
    string ModelPackZipPath,
    string TargetPretrainedModelsRoot,
    string? CurrentIw3Version = null,
    string? CurrentV3dfyVersion = null);

public sealed record ModelPackManifestSummary(
    string PackId,
    string PackVersion,
    string DisplayName,
    string TargetRoot,
    IReadOnlyList<string> CompatibleIw3Versions,
    string MinV3dfyVersion,
    int ModelCount,
    int FileCount);

public sealed record ModelPackPlannedFile(
    string RelativePath,
    string DestinationPath,
    string Sha256,
    long SizeBytes,
    string Role);

public sealed record ModelPackFileConflict(
    string RelativePath,
    string DestinationPath,
    string ExpectedSha256,
    string? ExistingSha256,
    long ExpectedSizeBytes,
    long? ExistingSizeBytes,
    string Message);

public sealed record ModelPackDryRunInstallPlan(
    bool IsValid,
    ModelPackManifestSummary? Manifest,
    string ModelPackZipPath,
    string TargetPretrainedModelsRoot,
    IReadOnlyList<ModelPackPlannedFile> FilesToInstall,
    IReadOnlyList<ModelPackPlannedFile> AlreadyInstalledFiles,
    IReadOnlyList<ModelPackFileConflict> Conflicts,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> Warnings,
    bool ElevationWouldBeRequired);
