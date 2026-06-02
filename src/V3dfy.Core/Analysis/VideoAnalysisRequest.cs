namespace V3dfy.Core.Analysis;

public sealed record VideoAnalysisRequest(
    string InputPath,
    TimeSpan? Timeout = null);
