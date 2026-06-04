using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionRequest(
    VideoConversionPlan Plan,
    string SourcePath,
    string OutputPath,
    TargetDevicePreset SelectedPreset,
    VideoConversionPlanOptions Options,
    InternalToolPaths ExpectedToolPaths,
    LocalModelPlanSelection? SelectedLocalModel,
    string CommandPreview,
    VideoConversionPlanStatus PlanStatus,
    ConversionDryRunReason DryRunReason,
    bool IsDryRun,
    CancellationToken CancellationToken = default)
{
    public OutputContainer OutputContainer => Options.OutputContainer;

    public ThreeDOutputFormat ThreeDOutputFormat => Options.ThreeDOutputFormat;

    public ThreeDIntensity Intensity => Options.Intensity;

    public AiQualityPreset QualityPreset => Options.QualityPreset;
}
