namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionLogEntry(
    DateTimeOffset Timestamp,
    string EnglishMessage,
    string SpanishMessage);
