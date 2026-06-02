using V3dfy.Core.Processes;

namespace V3dfy.Infrastructure.Processes;

public interface ILocalProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken = default);
}
