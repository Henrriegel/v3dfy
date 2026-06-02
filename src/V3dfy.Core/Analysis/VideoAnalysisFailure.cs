namespace V3dfy.Core.Analysis;

public sealed record VideoAnalysisFailure(
    VideoAnalysisFailureKind Kind,
    string Message,
    string? StandardError = null);
