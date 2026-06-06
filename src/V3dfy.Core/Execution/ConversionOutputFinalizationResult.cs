namespace V3dfy.Core.Execution;

public sealed record ConversionOutputFinalizationResult(
    bool Success,
    bool FinalizationFailure,
    IReadOnlyList<ConversionExecutionLogEntry> Logs);
