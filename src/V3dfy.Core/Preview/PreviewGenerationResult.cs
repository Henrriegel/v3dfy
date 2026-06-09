using V3dfy.Core.Execution;

namespace V3dfy.Core.Preview;

public sealed record PreviewGenerationResult(
    bool Success,
    bool WasCanceled,
    PreviewGenerationStatus Status,
    string? PreviewOutputPath,
    PreviewCachePaths CachePaths,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string EnglishSummary,
    string SpanishSummary,
    IReadOnlyList<ConversionExecutionLogEntry> Logs);
