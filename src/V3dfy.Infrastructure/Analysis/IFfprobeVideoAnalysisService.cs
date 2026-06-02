using V3dfy.Core.Analysis;

namespace V3dfy.Infrastructure.Analysis;

public interface IFfprobeVideoAnalysisService
{
    Task<VideoAnalysisServiceResult> AnalyzeAsync(
        VideoAnalysisRequest request,
        CancellationToken cancellationToken = default);
}
