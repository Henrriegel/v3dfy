using V3dfy.Core.Models;

namespace V3dfy.Core.Estimation;

public sealed record OutputSizeEstimateInput(
    TimeSpan? Duration,
    OutputContainer OutputContainer,
    AiQualityPreset QualityPreset,
    string? OutputPresetId,
    int? TargetWidth,
    int? TargetHeight,
    bool IncludeTemporaryWorkingSpace = true);
