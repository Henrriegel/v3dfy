namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionResult(
    bool Success,
    bool WasCanceled,
    int? ExitCode,
    string EnglishSummary,
    string SpanishSummary,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<ConversionExecutionLogEntry> Logs);
