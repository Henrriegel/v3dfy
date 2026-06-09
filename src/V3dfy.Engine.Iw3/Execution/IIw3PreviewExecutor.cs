using V3dfy.Core.Execution;
using V3dfy.Core.Preview;

namespace V3dfy.Engine.Iw3.Execution;

public interface IIw3PreviewExecutor
{
    Task<PreviewGenerationResult> ExecuteAsync(
        Iw3PreviewGenerationRequest request,
        IProgress<ConversionExecutionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);
}
