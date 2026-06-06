namespace V3dfy.Core.Execution;

public sealed record ConversionOutputPreparationResult(
    bool Success,
    string FinalOutputPath,
    string PartialOutputPath,
    IReadOnlyList<ConversionExecutionLogEntry> Logs);
