namespace V3dfy.Core.Estimation;

public sealed record ModelGuidance(
    string EnglishHeadline,
    string SpanishHeadline,
    string EnglishBestFor,
    string SpanishBestFor,
    string EnglishSpeed,
    string SpanishSpeed,
    string EnglishQuality,
    string SpanishQuality,
    string EnglishSize,
    string SpanishSize,
    bool IsRecommendedFirstOptionalModel,
    bool IsBaseModel,
    bool IsExperimental);
