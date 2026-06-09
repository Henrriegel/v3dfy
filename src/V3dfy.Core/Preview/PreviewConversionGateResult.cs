namespace V3dfy.Core.Preview;

public sealed record PreviewConversionGateResult(
    bool CanStart,
    string EnglishStatus,
    string SpanishStatus,
    string EnglishDetail,
    string SpanishDetail);
