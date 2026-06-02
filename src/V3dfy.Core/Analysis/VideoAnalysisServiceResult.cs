using V3dfy.Core.Models;

namespace V3dfy.Core.Analysis;

public sealed record VideoAnalysisServiceResult(
    VideoAnalysisResult? Analysis,
    VideoAnalysisFailure? Failure)
{
    public bool IsSuccess => Analysis is not null && Failure is null;

    public static VideoAnalysisServiceResult Success(VideoAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        return new VideoAnalysisServiceResult(analysis, Failure: null);
    }

    public static VideoAnalysisServiceResult Failed(VideoAnalysisFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new VideoAnalysisServiceResult(Analysis: null, failure);
    }
}
