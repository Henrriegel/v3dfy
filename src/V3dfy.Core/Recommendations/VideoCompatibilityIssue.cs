namespace V3dfy.Core.Recommendations;

public sealed record VideoCompatibilityIssue(
    VideoCompatibilitySeverity Severity,
    string EnglishMessage,
    string SpanishMessage);
