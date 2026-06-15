namespace V3dfy.Core.Models;

public sealed record TargetDevicePreset(
    string Id,
    string Name,
    string SpanishName,
    ConversionRecommendation Recommendation,
    IReadOnlyList<OutputContainer> AdvancedOutputContainers,
    string PlaybackTitle,
    string SpanishPlaybackTitle,
    string PlaybackInstructions,
    string SpanishPlaybackInstructions,
    string Description,
    string SpanishDescription,
    string BestFor,
    string SpanishBestFor,
    string CompatibilityNote,
    string SpanishCompatibilityNote,
    TargetDevicePresetCategory Category = TargetDevicePresetCategory.Recommended,
    int EstimatedVideoBitrateLowMbps = 10,
    int EstimatedVideoBitrateHighMbps = 16,
    bool UsesLegacyLgCompatibilityGuidance = false);
