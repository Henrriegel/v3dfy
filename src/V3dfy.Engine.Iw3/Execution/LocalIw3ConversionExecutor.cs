using V3dfy.Core.Execution;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Engine.Iw3.Execution;

/// <summary>
/// Controlled runner for bundled iw3 conversion execution. It validates
/// conversion requests and starts only an explicit process request built from
/// bundled local paths.
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
    private const string UnmappedModelEnglishSummary =
        "Selected local model is not mapped to a verified iw3 depth model yet.";
    private const string UnmappedModelSpanishSummary =
        "El modelo local seleccionado aun no esta mapeado a un modelo de profundidad iw3 verificado.";
    private const string CanceledEnglishSummary =
        "Local iw3 conversion was canceled.";
    private const string CanceledSpanishSummary =
        "La conversion local iw3 fue cancelada.";
    private const string CompletedEnglishSummary =
        "Local iw3 conversion completed.";
    private const string CompletedSpanishSummary =
        "La conversion local iw3 se completo.";
    private const string FailedEnglishSummary =
        "Local iw3 conversion failed.";
    private const string FailedSpanishSummary =
        "La conversion local iw3 fallo.";
    private const string TimedOutEnglishSummary =
        "Local iw3 conversion timed out.";
    private const string TimedOutSpanishSummary =
        "La conversion local iw3 agoto el tiempo de espera.";
    private const string StartingEnglishDetail =
        "Starting local iw3 conversion.";
    private const string StartingSpanishDetail =
        "Iniciando conversion local iw3.";
    private const string NoProcessEnglishDetail =
        "No Python, iw3, FFmpeg conversion, or model process was started.";
    private const string NoProcessSpanishDetail =
        "No se inicio ningun proceso de Python, iw3, conversion con FFmpeg ni modelo.";

    private readonly ConversionExecutionRequestValidator _requestValidator;
    private readonly LocalIw3ProcessRequestBuilder _processRequestBuilder;
    private readonly ILocalProcessRunner _processRunner;

    public LocalIw3ConversionExecutor(
        ConversionExecutionRequestValidator? requestValidator = null,
        LocalIw3ProcessRequestBuilder? processRequestBuilder = null,
        ILocalProcessRunner? processRunner = null)
    {
        _requestValidator = requestValidator ?? new ConversionExecutionRequestValidator();
        _processRequestBuilder =
            processRequestBuilder ?? new LocalIw3ProcessRequestBuilder(_requestValidator);
        _processRunner = processRunner ?? new BundledLocalProcessRunner();
    }

    public async Task<ConversionExecutionResult> ExecuteAsync(
        ConversionExecutionRequest request,
        IProgress<ConversionExecutionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var validationResult = _requestValidator.Validate(request);

        if (!validationResult.IsValid)
        {
            return CreateInvalidRequestResult(startedAt, validationResult);
        }

        if (validationResult.IsDryRun)
        {
            return CreateBlockedResult(
                startedAt,
                DryRunEnglishSummary,
                DryRunSpanishSummary);
        }

        if (cancellationToken.IsCancellationRequested ||
            request.CancellationToken.IsCancellationRequested)
        {
            return CreateCanceledBeforeStartResult(startedAt);
        }

        if (!Iw3DepthModelMapper.TryMap(request.SelectedLocalModel, out _))
        {
            return CreateBlockedResult(
                startedAt,
                UnmappedModelEnglishSummary,
                UnmappedModelSpanishSummary);
        }

        var processRequest = _processRequestBuilder.Build(request);
        progress?.Report(new(
            ProgressPercent: 0,
            CurrentStep: new(StartingEnglishDetail, StartingSpanishDetail),
            DetailEnglish: "Launching bundled Python with local iw3.",
            DetailSpanish: "Iniciando Python incluido con iw3 local."));

        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                request.CancellationToken);
        var processResult = await _processRunner.RunAsync(
            processRequest,
            linkedCancellationTokenSource.Token);

        progress?.Report(CreateFinalProgress(processResult));
        return CreateProcessResult(startedAt, processResult);
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
        string spanishSummary)
    {
        var finishedAt = DateTimeOffset.UtcNow;
        var logs = new List<ConversionExecutionLogEntry>
        {
            new(
                finishedAt,
                $"{englishSummary} {NoProcessEnglishDetail}",
                $"{spanishSummary} {NoProcessSpanishDetail}"),
        };

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

    private static ConversionExecutionResult CreateCanceledBeforeStartResult(
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

    private static ConversionExecutionProgressUpdate CreateFinalProgress(
        ProcessExecutionResult processResult)
    {
        var success = processResult.Status == ProcessExecutionStatus.Completed &&
            processResult.ExitCode == 0;

        return new(
            ProgressPercent: success ? 100 : 0,
            CurrentStep: new(
                GetEnglishSummary(processResult),
                GetSpanishSummary(processResult)),
            DetailEnglish: processResult.EnglishSummary,
            DetailSpanish: processResult.SpanishSummary);
    }

    private static ConversionExecutionResult CreateProcessResult(
        DateTimeOffset startedAt,
        ProcessExecutionResult processResult)
    {
        var success = processResult.Status == ProcessExecutionStatus.Completed &&
            processResult.ExitCode == 0;
        var englishSummary = GetEnglishSummary(processResult);
        var spanishSummary = GetSpanishSummary(processResult);
        var logs = new List<ConversionExecutionLogEntry>
        {
            new(processResult.EndedAt, englishSummary, spanishSummary),
        };

        logs.AddRange(CreateOutputLogs(processResult));

        return new(
            Success: success,
            WasCanceled: processResult.WasCanceled,
            ExitCode: processResult.ExitCode,
            EnglishSummary: englishSummary,
            SpanishSummary: spanishSummary,
            StartedAt: startedAt,
            FinishedAt: processResult.EndedAt,
            Logs: logs);
    }

    private static string GetEnglishSummary(ProcessExecutionResult processResult) =>
        processResult.Status switch
        {
            ProcessExecutionStatus.Completed when processResult.ExitCode == 0 =>
                CompletedEnglishSummary,
            ProcessExecutionStatus.Canceled => CanceledEnglishSummary,
            ProcessExecutionStatus.TimedOut => TimedOutEnglishSummary,
            _ => FailedEnglishSummary,
        };

    private static string GetSpanishSummary(ProcessExecutionResult processResult) =>
        processResult.Status switch
        {
            ProcessExecutionStatus.Completed when processResult.ExitCode == 0 =>
                CompletedSpanishSummary,
            ProcessExecutionStatus.Canceled => CanceledSpanishSummary,
            ProcessExecutionStatus.TimedOut => TimedOutSpanishSummary,
            _ => FailedSpanishSummary,
        };

    private static IEnumerable<ConversionExecutionLogEntry> CreateOutputLogs(
        ProcessExecutionResult processResult)
    {
        if (processResult.OutputLines.Count > 0)
        {
            foreach (var line in processResult.OutputLines)
            {
                yield return new(
                    line.CapturedAt,
                    FormatOutputLine(line.Stream, line.Text),
                    FormatOutputLine(line.Stream, line.Text));
            }

            yield break;
        }

        foreach (var line in SplitOutput(processResult.StandardOutput))
        {
            yield return new(
                processResult.EndedAt,
                FormatOutputLine(ProcessOutputStream.StandardOutput, line),
                FormatOutputLine(ProcessOutputStream.StandardOutput, line));
        }

        foreach (var line in SplitOutput(processResult.StandardError))
        {
            yield return new(
                processResult.EndedAt,
                FormatOutputLine(ProcessOutputStream.StandardError, line),
                FormatOutputLine(ProcessOutputStream.StandardError, line));
        }
    }

    private static string FormatOutputLine(ProcessOutputStream stream, string text)
    {
        var prefix = stream == ProcessOutputStream.StandardError
            ? "stderr"
            : "stdout";
        return $"{prefix}: {text}";
    }

    private static IEnumerable<string> SplitOutput(string output) =>
        output
            .Split(
                ["\r\n", "\n"],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
