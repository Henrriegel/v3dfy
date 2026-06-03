using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionRequest(
    VideoConversionPlan Plan,
    string SourcePath,
    string OutputPath,
    TargetDevicePreset SelectedPreset,
    VideoConversionPlanOptions Options,
    string CommandPreview,
    bool IsDryRun,
    CancellationToken CancellationToken = default);
