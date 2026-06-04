namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionRequestValidationIssue(
    ConversionExecutionRequestValidationIssueKind Kind,
    string FieldName,
    string EnglishMessage,
    string SpanishMessage);
