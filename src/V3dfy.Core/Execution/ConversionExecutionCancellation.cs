namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionCancellation(
    bool IsCancellationRequested,
    string EnglishReason,
    string SpanishReason);
