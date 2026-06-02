using V3dfy.Core.Models;

namespace V3dfy.Core.Recommendations;

public sealed record VideoConversionSetupRecommendation(
    OutputContainer OutputContainer,
    string VideoCodec,
    string AudioCodec,
    int Width,
    int Height,
    ThreeDOutputFormat ThreeDOutputFormat,
    AiQualityPreset QualityPreset,
    ThreeDIntensity Intensity,
    bool IsSourceAboveTargetResolution,
    bool IsHdrSource,
    bool UseTvCompatibleMp4,
    bool SuggestMkvMasterOutput,
    IReadOnlyList<VideoCompatibilityIssue> CompatibilityIssues);
