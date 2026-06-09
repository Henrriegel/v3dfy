namespace V3dfy.Core.Preview;

public sealed record PreviewOutputOpenResult(
    bool Opened,
    string? EnglishWarning,
    string? SpanishWarning);
