namespace V3dfy.Core.Execution;

public sealed record ConversionOutputOpenResult(
    bool Attempted,
    bool Opened,
    string? EnglishWarning,
    string? SpanishWarning);
