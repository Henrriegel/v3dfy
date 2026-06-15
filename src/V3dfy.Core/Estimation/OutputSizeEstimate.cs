namespace V3dfy.Core.Estimation;

public sealed record OutputSizeEstimate(
    bool IsAvailable,
    long LowBytes,
    long HighBytes,
    long RecommendedFreeBytes,
    IReadOnlyList<string> EnglishBasisItems,
    IReadOnlyList<string> SpanishBasisItems)
{
    public static OutputSizeEstimate Unavailable(
        string englishReason,
        string spanishReason) => new(
        IsAvailable: false,
        LowBytes: 0,
        HighBytes: 0,
        RecommendedFreeBytes: 0,
        EnglishBasisItems: [englishReason],
        SpanishBasisItems: [spanishReason]);
}
