using V3dfy.Core.Models;

namespace V3dfy.Core.Planning;

public sealed record VideoConversionPlanOptions(
    OutputContainer OutputContainer,
    AiQualityPreset QualityPreset,
    ThreeDIntensity Intensity,
    ThreeDOutputFormat ThreeDOutputFormat,
    string? CustomOutputPath = null);
