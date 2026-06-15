using V3dfy.Core.Models;

namespace V3dfy.Core.Estimation;

public sealed record ConversionEstimateInput(
    TimeSpan? Duration,
    double? FrameRate,
    int? SourceWidth,
    int? SourceHeight,
    string? ModelKey,
    string? ModelDisplayName,
    string? OutputPresetId,
    string? OutputPresetName,
    OutputContainer OutputContainer,
    AiQualityPreset QualityPreset,
    ThreeDOutputFormat ThreeDOutputFormat,
    string DeviceBucket)
{
    public static ConversionEstimateInput FromAnalysis(
        VideoAnalysisResult? analysis,
        string? modelKey,
        string? modelDisplayName,
        string? outputPresetId,
        string? outputPresetName,
        OutputContainer outputContainer,
        AiQualityPreset qualityPreset,
        ThreeDOutputFormat threeDOutputFormat,
        string deviceBucket) => new(
        analysis?.File.Duration,
        analysis?.Video?.FrameRate,
        analysis?.Video?.Width,
        analysis?.Video?.Height,
        modelKey,
        modelDisplayName,
        outputPresetId,
        outputPresetName,
        outputContainer,
        qualityPreset,
        threeDOutputFormat,
        string.IsNullOrWhiteSpace(deviceBucket) ? "unknown-device" : deviceBucket);
}
