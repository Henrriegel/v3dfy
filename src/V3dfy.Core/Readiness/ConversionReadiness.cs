namespace V3dfy.Core.Readiness;

public sealed record ConversionReadiness(
    bool CanConvert,
    string EnglishStatus,
    string SpanishStatus,
    IReadOnlyList<ConversionReadinessIssue> Issues,
    string EnglishRequiredComponentsSummary,
    string SpanishRequiredComponentsSummary);
