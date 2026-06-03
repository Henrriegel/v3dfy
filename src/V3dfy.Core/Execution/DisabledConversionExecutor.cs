namespace V3dfy.Core.Execution;

/// <summary>
/// Safe scaffold for the future local conversion runner. Real FFmpeg, iw3,
/// Python, and model execution is intentionally not enabled yet.
/// </summary>
public sealed class DisabledConversionExecutor : IConversionExecutor
{
    private const string EnglishNotEnabled =
        "Conversion execution is not enabled yet.";
    private const string SpanishNotEnabled =
        "La ejecución de conversión aún no está habilitada.";
    private const string EnglishCanceled =
        "Conversion was canceled before it started.";
    private const string SpanishCanceled =
        "La conversión fue cancelada antes de iniciar.";

    public Task<ConversionExecutionResult> ExecuteAsync(
        ConversionExecutionRequest request,
        IProgress<ConversionExecutionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Future implementations must execute only bundled local tools, never
        // global PATH tools. This disabled runner deliberately starts nothing.
        var startedAt = DateTimeOffset.UtcNow;
        var linkedCancellationRequested =
            cancellationToken.IsCancellationRequested ||
            request.CancellationToken.IsCancellationRequested;

        var englishSummary = linkedCancellationRequested
            ? EnglishCanceled
            : EnglishNotEnabled;
        var spanishSummary = linkedCancellationRequested
            ? SpanishCanceled
            : SpanishNotEnabled;
        var finishedAt = DateTimeOffset.UtcNow;

        ConversionExecutionResult result = new(
            Success: false,
            WasCanceled: linkedCancellationRequested,
            ExitCode: null,
            EnglishSummary: englishSummary,
            SpanishSummary: spanishSummary,
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            Logs:
            [
                new(finishedAt, englishSummary, spanishSummary),
            ]);

        return Task.FromResult(result);
    }
}
