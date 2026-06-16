using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Core.Image;

public sealed record ImageStereoExportRequest(
    string SourcePath,
    string OutputDirectory,
    ImageStereoExportFormat OutputFormat,
    double EyeSeparationPercent,
    string Convergence,
    bool SwapEyes,
    string AnaglyphMode,
    InternalToolPaths ExpectedToolPaths,
    EngineDependencyHealth DependencyHealth,
    LocalModelPlanSelection? SelectedLocalModel,
    CancellationToken CancellationToken = default);
