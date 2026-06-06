using V3dfy.Core.Execution;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Infrastructure.Files;
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
    private const string PartialPreparationFailedEnglishSummary =
        "Local iw3 conversion could not prepare the partial output file.";
    private const string PartialPreparationFailedSpanishSummary =
        "La conversion local iw3 no pudo preparar el archivo parcial.";

    private readonly ConversionExecutionRequestValidator _requestValidator;
    private readonly LocalIw3ProcessRequestBuilder _processRequestBuilder;
    private readonly ILocalProcessRunner _processRunner;
    private readonly ConversionOutputFinalizer _outputFinalizer;
    private readonly LgCompatibilityCopyRequestBuilder _lgCompatibilityCopyRequestBuilder;

    public LocalIw3ConversionExecutor(
        ConversionExecutionRequestValidator? requestValidator = null,
        LocalIw3ProcessRequestBuilder? processRequestBuilder = null,
        ILocalProcessRunner? processRunner = null,
        IConversionOutputFileService? outputFileService = null,
        LgCompatibilityCopyRequestBuilder? lgCompatibilityCopyRequestBuilder = null)
    {
        _requestValidator = requestValidator ?? new ConversionExecutionRequestValidator();
        _processRequestBuilder =
            processRequestBuilder ?? new LocalIw3ProcessRequestBuilder(_requestValidator);
        _processRunner = processRunner ?? new BundledLocalProcessRunner();
        _outputFinalizer = new(outputFileService ?? new FileSystemConversionOutputFileService());
        _lgCompatibilityCopyRequestBuilder =
            lgCompatibilityCopyRequestBuilder ?? new LgCompatibilityCopyRequestBuilder();
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

        var outputPreparation = _outputFinalizer.PreparePartialOutput(request.OutputPath);
        if (!outputPreparation.Success)
        {
            return CreatePartialPreparationFailureResult(startedAt, outputPreparation);
        }

        var processExecutionRequest = request with
        {
            OutputPath = outputPreparation.PartialOutputPath,
        };
        var processRequest = _processRequestBuilder.Build(processExecutionRequest) with
        {
            OutputProgress = CreateOutputProgress(progress),
            MetricsProgress = CreateMetricsProgress(progress),
        };
        progress?.Report(new(
            ProgressPercent: 0,
            CurrentStep: new(StartingEnglishDetail, StartingSpanishDetail),
            DetailEnglish:
                "Launching bundled Python with local iw3. Writing to a partial output first.",
            DetailSpanish:
                "Iniciando Python incluido con iw3 local. Escribiendo primero en un archivo parcial."));

        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                request.CancellationToken);
        var processResult = await _processRunner.RunAsync(
            processRequest,
            linkedCancellationTokenSource.Token);

        var outputFinalization = _outputFinalizer.FinalizeAfterProcess(
            processResult,
            outputPreparation.FinalOutputPath,
            outputPreparation.PartialOutputPath);
        var compatibilityCopyResult = await CreateLgCompatibilityCopyAsync(
            request,
            outputFinalization,
            outputPreparation.FinalOutputPath,
            progress,
            linkedCancellationTokenSource.Token);

        progress?.Report(CreateFinalProgress(
            processResult,
            outputFinalization,
            compatibilityCopyResult));
        return CreateProcessResult(
            request,
            startedAt,
            processResult,
            outputPreparation,
            outputFinalization,
            compatibilityCopyResult);
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

    private static ConversionExecutionResult CreatePartialPreparationFailureResult(
        DateTimeOffset startedAt,
        ConversionOutputPreparationResult outputPreparation)
    {
        var finishedAt = DateTimeOffset.UtcNow;
        var logs = new List<ConversionExecutionLogEntry>(outputPreparation.Logs)
        {
            new(
                finishedAt,
                $"{PartialPreparationFailedEnglishSummary} {NoProcessEnglishDetail}",
                $"{PartialPreparationFailedSpanishSummary} {NoProcessSpanishDetail}"),
        };

        return new(
            Success: false,
            WasCanceled: false,
            ExitCode: null,
            EnglishSummary: PartialPreparationFailedEnglishSummary,
            SpanishSummary: PartialPreparationFailedSpanishSummary,
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            Logs: logs);
    }

    private async Task<LgCompatibilityCopyExecutionResult> CreateLgCompatibilityCopyAsync(
        ConversionExecutionRequest request,
        ConversionOutputFinalizationResult primaryFinalization,
        string primaryOutputPath,
        IProgress<ConversionExecutionProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        if (!request.Options.CreateLgCompatibilityCopy ||
            !primaryFinalization.Success)
        {
            return LgCompatibilityCopyExecutionResult.Skipped();
        }

        var compatibilityFinalOutputPath =
            LgCompatibilityCopyRequestBuilder.CreateCompatibilityOutputPath(
                primaryOutputPath,
                request.ThreeDOutputFormat);
        var compatibilityPreparation =
            _outputFinalizer.PreparePartialOutput(compatibilityFinalOutputPath);
        if (!compatibilityPreparation.Success)
        {
            var preparationLogs = new List<ConversionExecutionLogEntry>(
                compatibilityPreparation.Logs)
            {
                CreateLog(
                    "LG-compatible MP4 copy was skipped because the partial copy path could not be prepared. The primary output remains available.",
                    "La copia MP4 compatible con LG se omitio porque no se pudo preparar la ruta parcial. La salida principal sigue disponible."),
            };
            return LgCompatibilityCopyExecutionResult.Failed(
                compatibilityFinalOutputPath,
                preparationLogs);
        }

        var copyRequest = _lgCompatibilityCopyRequestBuilder.Create(
            request,
            primaryOutputPath,
            compatibilityPreparation.PartialOutputPath);
        if (!copyRequest.ShouldRun)
        {
            return LgCompatibilityCopyExecutionResult.Failed(
                compatibilityFinalOutputPath,
                [.. compatibilityPreparation.Logs, .. copyRequest.Logs]);
        }

        progress?.Report(new(
            ProgressPercent: 95,
            CurrentStep: new(
                "Creating LG-compatible MP4 copy.",
                "Creando copia MP4 compatible con LG."),
            DetailEnglish:
                "Post-processing the completed primary output with bundled FFmpeg.",
            DetailSpanish:
                "Postprocesando la salida principal completada con FFmpeg incluido."));

        var compatibilityProcessRequest = copyRequest.ProcessRequest! with
        {
            OutputProgress = CreateOutputProgress(progress),
            MetricsProgress = CreateMetricsProgress(progress),
        };
        var compatibilityProcessResult = await _processRunner.RunAsync(
            compatibilityProcessRequest,
            cancellationToken);
        var compatibilityFinalization = _outputFinalizer.FinalizeAfterProcess(
            compatibilityProcessResult,
            compatibilityPreparation.FinalOutputPath,
            compatibilityPreparation.PartialOutputPath);
        var copySucceeded =
            compatibilityProcessResult.Status == ProcessExecutionStatus.Completed &&
            compatibilityProcessResult.ExitCode == 0 &&
            compatibilityFinalization.Success;

        var logs = new List<ConversionExecutionLogEntry>();
        logs.AddRange(compatibilityPreparation.Logs);
        logs.AddRange(copyRequest.Logs);
        logs.AddRange(CreateOutputLogs(compatibilityProcessResult));
        logs.AddRange(compatibilityFinalization.Logs);
        logs.Add(copySucceeded
            ? CreateLog(
                $"LG-compatible MP4 copy saved to {compatibilityFinalOutputPath}.",
                $"Copia MP4 compatible con LG guardada en {compatibilityFinalOutputPath}.")
            : CreateLog(
                "LG-compatible MP4 copy failed. The primary output remains available.",
                "La copia MP4 compatible con LG fallo. La salida principal sigue disponible."));

        if (compatibilityProcessResult.Status == ProcessExecutionStatus.Canceled)
        {
            return LgCompatibilityCopyExecutionResult.Canceled(
                compatibilityFinalOutputPath,
                logs);
        }

        return copySucceeded
            ? LgCompatibilityCopyExecutionResult.Completed(
                compatibilityFinalOutputPath,
                logs)
            : LgCompatibilityCopyExecutionResult.Failed(
                compatibilityFinalOutputPath,
                logs);
    }

    private static ConversionExecutionProgressUpdate CreateFinalProgress(
        ProcessExecutionResult processResult,
        ConversionOutputFinalizationResult outputFinalization,
        LgCompatibilityCopyExecutionResult compatibilityCopyResult)
    {
        var success = processResult.Status == ProcessExecutionStatus.Completed &&
            processResult.ExitCode == 0 &&
            outputFinalization.Success &&
            !compatibilityCopyResult.WasCanceled;

        return new(
            ProgressPercent: success ? 100 : 0,
            CurrentStep: new(
                outputFinalization.FinalizationFailure
                    ? FailedEnglishSummary
                    : compatibilityCopyResult.WasCanceled
                        ? CanceledEnglishSummary
                    : GetEnglishSummary(processResult),
                outputFinalization.FinalizationFailure
                    ? FailedSpanishSummary
                    : compatibilityCopyResult.WasCanceled
                        ? CanceledSpanishSummary
                    : GetSpanishSummary(processResult)),
            DetailEnglish: outputFinalization.FinalizationFailure
                ? "Final output promotion failed."
                : compatibilityCopyResult.WasCanceled
                    ? "LG-compatible MP4 copy was canceled."
                : processResult.EnglishSummary,
            DetailSpanish: outputFinalization.FinalizationFailure
                ? "La promocion de la salida final fallo."
                : compatibilityCopyResult.WasCanceled
                    ? "La copia MP4 compatible con LG fue cancelada."
                : processResult.SpanishSummary);
    }

    private static IProgress<ProcessOutputLine>? CreateOutputProgress(
        IProgress<ConversionExecutionProgressUpdate>? progress) =>
        progress is null
            ? null
            : new DelegateProgress<ProcessOutputLine>(line => progress.Report(new(
                ProgressPercent: 0,
                CurrentStep: new(
                    "Running local iw3 conversion.",
                    "Ejecutando conversion local iw3."),
                DetailEnglish: FormatOutputLine(line.Stream, line.Text),
                DetailSpanish: FormatOutputLine(line.Stream, line.Text),
                OutputLine: line)));

    private static IProgress<ProcessMetricSample>? CreateMetricsProgress(
        IProgress<ConversionExecutionProgressUpdate>? progress) =>
        progress is null
            ? null
            : new DelegateProgress<ProcessMetricSample>(metrics => progress.Report(new(
                ProgressPercent: 0,
                CurrentStep: new(
                    "Running local iw3 conversion.",
                    "Ejecutando conversion local iw3."),
                DetailEnglish: "Process metrics updated.",
                DetailSpanish: "Metricas del proceso actualizadas.",
                Metrics: metrics)));

    private static ConversionExecutionResult CreateProcessResult(
        ConversionExecutionRequest request,
        DateTimeOffset startedAt,
        ProcessExecutionResult processResult,
        ConversionOutputPreparationResult outputPreparation,
        ConversionOutputFinalizationResult outputFinalization,
        LgCompatibilityCopyExecutionResult compatibilityCopyResult)
    {
        var success = processResult.Status == ProcessExecutionStatus.Completed &&
            processResult.ExitCode == 0 &&
            outputFinalization.Success &&
            !compatibilityCopyResult.WasCanceled;
        var englishSummary = outputFinalization.FinalizationFailure
            ? FailedEnglishSummary
            : compatibilityCopyResult.WasCanceled
                ? CanceledEnglishSummary
            : GetEnglishSummary(processResult);
        var spanishSummary = outputFinalization.FinalizationFailure
            ? FailedSpanishSummary
            : compatibilityCopyResult.WasCanceled
                ? CanceledSpanishSummary
            : GetSpanishSummary(processResult);
        var logs = new List<ConversionExecutionLogEntry>
        {
            new(processResult.EndedAt, englishSummary, spanishSummary),
        };

        logs.AddRange(outputPreparation.Logs);
        logs.AddRange(CreateOutputLogs(processResult));
        logs.AddRange(outputFinalization.Logs);
        logs.AddRange(compatibilityCopyResult.Logs);

        return new(
            Success: success,
            WasCanceled: processResult.WasCanceled || compatibilityCopyResult.WasCanceled,
            ExitCode: processResult.ExitCode,
            EnglishSummary: englishSummary,
            SpanishSummary: spanishSummary,
            StartedAt: startedAt,
            FinishedAt: processResult.EndedAt,
            Logs: logs,
            PrimaryOutputPath: outputPreparation.FinalOutputPath,
            CompatibilityOutputPath: compatibilityCopyResult.Success
                ? compatibilityCopyResult.FinalOutputPath
                : null,
            PreferredOpenOutputPath:
                compatibilityCopyResult.Success &&
                request.Options.PreferLgCompatibilityCopyWhenOpening
                    ? compatibilityCopyResult.FinalOutputPath
                    : outputPreparation.FinalOutputPath,
            CompatibilityCopySucceeded: compatibilityCopyResult.Success);
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

    private static ConversionExecutionLogEntry CreateLog(
        string englishMessage,
        string spanishMessage) => new(
        DateTimeOffset.UtcNow,
        englishMessage,
        spanishMessage);

    private sealed record LgCompatibilityCopyExecutionResult(
        bool Success,
        bool WasCanceled,
        string? FinalOutputPath,
        IReadOnlyList<ConversionExecutionLogEntry> Logs)
    {
        public static LgCompatibilityCopyExecutionResult Skipped() => new(
            Success: false,
            WasCanceled: false,
            FinalOutputPath: null,
            Logs: []);

        public static LgCompatibilityCopyExecutionResult Completed(
            string finalOutputPath,
            IReadOnlyList<ConversionExecutionLogEntry> logs) => new(
            Success: true,
            WasCanceled: false,
            FinalOutputPath: finalOutputPath,
            Logs: logs);

        public static LgCompatibilityCopyExecutionResult Failed(
            string finalOutputPath,
            IReadOnlyList<ConversionExecutionLogEntry> logs) => new(
            Success: false,
            WasCanceled: false,
            FinalOutputPath: finalOutputPath,
            Logs: logs);

        public static LgCompatibilityCopyExecutionResult Canceled(
            string finalOutputPath,
            IReadOnlyList<ConversionExecutionLogEntry> logs) => new(
            Success: false,
            WasCanceled: true,
            FinalOutputPath: finalOutputPath,
            Logs: logs);
    }

    private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
