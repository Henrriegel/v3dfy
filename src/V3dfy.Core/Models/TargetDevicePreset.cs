namespace V3dfy.Core.Models;

public sealed record TargetDevicePreset(
    string Name,
    ConversionRecommendation Recommendation,
    IReadOnlyList<OutputContainer> AdvancedOutputContainers);
