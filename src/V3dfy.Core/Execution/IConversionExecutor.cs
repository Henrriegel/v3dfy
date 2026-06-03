namespace V3dfy.Core.Execution;

public interface IConversionExecutor
{
    Task<ConversionExecutionResult> ExecuteAsync(
        ConversionExecutionRequest request,
        IProgress<ConversionExecutionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);
}
