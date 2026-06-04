using V3dfy.Core.Models;

namespace V3dfy.Core.Planning;

public sealed record VideoConversionPlan(
    string SourcePath,
    string SuggestedOutputPath,
    OutputContainer OutputContainer,
    string VideoCodec,
    string AudioCodec,
    int Width,
    int Height,
    ThreeDOutputFormat ThreeDOutputFormat,
    AiQualityPreset QualityPreset,
    ThreeDIntensity Intensity,
    VideoConversionPlanStatus Status,
    ConversionDryRunReason DryRunReason,
    IReadOnlyList<VideoConversionPlanStep> Steps,
    string CommandPreview)
{
    public LocalModelPlanSelection? SelectedLocalModel { get; init; }

    public bool IsDryRun => Status == VideoConversionPlanStatus.DryRun;
}
