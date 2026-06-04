using V3dfy.Core.Execution;

namespace V3dfy.Engine.Iw3.Execution;

/// <summary>
/// Safe shell for the future bundled iw3 conversion runner. It validates
/// conversion requests and deliberately starts no Python, iw3, FFmpeg, or
/// model process until real execution is explicitly implemented.
/// </summary>
public sealed class LocalIw3ConversionExecutor : IConversionExecutor
{
    private const string InvalidRequestEnglishSummary =
        "Local iw3 conversion request is invalid. No local process was started.";
    private const string InvalidRequestSpanishSummary =
        "La solicitud de conversion local iw3 no es valida. No se inicio ningun proceso local.";
    private const string DryRunEnglishSummary =
        "Local iw3 conversion is blocked because the request is a dry-run preview.";
    private const string DryRunSpanishSummary =
        "La conversion local iw3 esta bloqueada porque la solicitud es una vista previa en seco.";
    private const string NotImplementedEnglishSummary =
        "Local iw3 conversion execution is not implemented yet.";
    private const string NotImplementedSpanishSummary =
        "La ejecucion de conversion local iw3 aun no esta implementada.";
    private const string CanceledEnglishSummary =
        "Local iw3 conversion was canceled before it started.";
    private const string CanceledSpanishSummary =
        "La conversion local iw3 fue cancelada antes de iniciar.";
    private const string NoProcessEnglishDetail =
        "No Python, iw3, FFmpeg conversion, or model process was started.";
    private const string NoProcessSpanishDetail =
        "No se inicio ningun proceso de Python, iw3, conversion con FFmpeg ni modelo.";
    private const string ProcessRequestPreparedEnglishDetail =
        "Future iw3 process request was prepared but not executed.";
    private const string ProcessRequestPreparedSpanishDetail =
        "La solicitud futura del proceso iw3 fue preparada, pero no se ejecuto.";

    private readonly ConversionExecutionRequestValidator _requestValidator;
    private readonly LocalIw3ProcessRequestBuilder _processRequestBuilder;

    public LocalIw3ConversionExecutor(
        ConversionExecutionRequestValidator? requestValidator = null,
        LocalIw3ProcessRequestBuilder? processRequestBuilder = null)
    {
        _requestValidator = requestValidator ?? new ConversionExecutionRequestValidator();
        _processRequestBuilder =
            processRequestBuilder ?? new LocalIw3ProcessRequestBuilder(_requestValidator);
    }

    public Task<ConversionExecutionResult> ExecuteAsync(
        ConversionExecutionRequest request,
        IProgress<ConversionExecutionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var validationResult = _requestValidator.Validate(request);

        if (!validationResult.IsValid)
        {
            return Task.FromResult(CreateInvalidRequestResult(startedAt, validationResult));
        }

        if (validationResult.IsDryRun)
        {
            return Task.FromResult(CreateBlockedResult(
                startedAt,
                DryRunEnglishSummary,
                DryRunSpanishSummary));
        }

        if (cancellationToken.IsCancellationRequested ||
            request.CancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(CreateCanceledResult(startedAt));
        }

        _ = _processRequestBuilder.Build(request);

        return Task.FromResult(CreateBlockedResult(
            startedAt,
            NotImplementedEnglishSummary,
            NotImplementedSpanishSummary,
            processRequestPrepared: true));
    }

    private static ConversionExecutionResult CreateInvalidRequestResult(
        DateTimeOffset startedAt,
        ConversionExecutionRequestValidationResult validationResult)
    {
        var finishedAt = DateTimeOffset.UtcNow;
        var logs = new List<ConversionExecutionLogEntry>
        {
            new(
                finishedAt,
                $"{InvalidRequestEnglishSummary} {NoProcessEnglishDetail}",
                $"{InvalidRequestSpanishSummary} {NoProcessSpanishDetail}"),
        };

        foreach (var issue in validationResult.Issues)
        {
            logs.Add(new(
                finishedAt,
                $"{issue.FieldName}: {issue.EnglishMessage}",
                $"{issue.FieldName}: {issue.SpanishMessage}"));
        }

        return new(
            Success: false,
            WasCanceled: false,
            ExitCode: null,
            EnglishSummary: InvalidRequestEnglishSummary,
            SpanishSummary: InvalidRequestSpanishSummary,
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            Logs: logs);
    }

    private static ConversionExecutionResult CreateBlockedResult(
        DateTimeOffset startedAt,
        string englishSummary,
        string spanishSummary,
        bool processRequestPrepared = false)
    {
        var finishedAt = DateTimeOffset.UtcNow;
        var logs = new List<ConversionExecutionLogEntry>
        {
            new(
                finishedAt,
                $"{englishSummary} {NoProcessEnglishDetail}",
                $"{spanishSummary} {NoProcessSpanishDetail}"),
        };

        if (processRequestPrepared)
        {
            logs.Add(new(
                finishedAt,
                ProcessRequestPreparedEnglishDetail,
                ProcessRequestPreparedSpanishDetail));
        }

        return new(
            Success: false,
            WasCanceled: false,
            ExitCode: null,
            EnglishSummary: englishSummary,
            SpanishSummary: spanishSummary,
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            Logs: logs);
    }

    private static ConversionExecutionResult CreateCanceledResult(
        DateTimeOffset startedAt)
    {
        var finishedAt = DateTimeOffset.UtcNow;
        return new(
            Success: false,
            WasCanceled: true,
            ExitCode: null,
            EnglishSummary: CanceledEnglishSummary,
            SpanishSummary: CanceledSpanishSummary,
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            Logs:
            [
                new(
                    finishedAt,
                    $"{CanceledEnglishSummary} {NoProcessEnglishDetail}",
                    $"{CanceledSpanishSummary} {NoProcessSpanishDetail}"),
            ]);
    }
}
