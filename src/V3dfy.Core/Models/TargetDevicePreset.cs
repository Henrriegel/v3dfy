namespace V3dfy.Core.Models;

public sealed record TargetDevicePreset(
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
    bool UsesLegacyLgCompatibilityGuidance = false);
