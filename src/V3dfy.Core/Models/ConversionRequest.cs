namespace V3dfy.Core.Models;

public sealed record ConversionRequest(
    string InputPath,
    string OutputPath,
    OutputContainer OutputContainer,
    ThreeDOutputFormat ThreeDOutputFormat,
    AiQualityPreset AiQualityPreset,
    ThreeDIntensity ThreeDIntensity,
    double? CustomDepth = null);
