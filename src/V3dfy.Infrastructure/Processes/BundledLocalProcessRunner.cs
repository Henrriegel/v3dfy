using V3dfy.Core.Processes;

namespace V3dfy.Infrastructure.Processes;

/// <summary>
/// Reusable guard for future bundled local tool execution. It validates that
/// callers provide explicit bundled executable paths before delegating to the
/// raw process runner. This does not enable conversion by itself.
/// </summary>
public sealed class BundledLocalProcessRunner : ILocalProcessRunner
{
    private readonly ILocalProcessRunner _innerRunner;
    private readonly string? _allowedRootDirectory;

    public BundledLocalProcessRunner(
        ILocalProcessRunner? innerRunner = null,
        string? allowedRootDirectory = null)
    {
        _innerRunner = innerRunner ?? new LocalProcessRunner();
        _allowedRootDirectory = allowedRootDirectory;
    }

    public Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestToValidate = request.AllowedRootDirectory is null &&
            !string.IsNullOrWhiteSpace(_allowedRootDirectory)
                ? request with { AllowedRootDirectory = _allowedRootDirectory }
                : request;

        ProcessExecutionRequestValidator.ValidateBundledToolRequest(requestToValidate);

        // The delegated runner uses ProcessStartInfo with UseShellExecute=false
        // and ArgumentList. Future FFmpeg/FFprobe/iw3/Python execution must
        // keep using explicit bundled paths, never global PATH tools.
        return _innerRunner.RunAsync(requestToValidate, cancellationToken);
    }
}
