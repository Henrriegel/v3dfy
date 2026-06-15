namespace V3dfy.Core.Estimation;

public sealed record ConversionTimeEstimate(
    bool IsAvailable,
    TimeSpan Low,
    TimeSpan High,
    ConversionEstimateConfidence Confidence,
    IReadOnlyList<string> EnglishBasisItems,
    IReadOnlyList<string> SpanishBasisItems,
    bool UsedLocalHistory)
{
    public static ConversionTimeEstimate Unavailable(
        string englishReason,
        string spanishReason) => new(
        IsAvailable: false,
        Low: TimeSpan.Zero,
        High: TimeSpan.Zero,
        Confidence: ConversionEstimateConfidence.Unavailable,
        EnglishBasisItems: [englishReason],
        SpanishBasisItems: [spanishReason],
        UsedLocalHistory: false);
}
