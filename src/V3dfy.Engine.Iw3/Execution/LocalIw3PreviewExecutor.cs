using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Preview;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Infrastructure.Files;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Engine.Iw3.Execution;

public sealed class LocalIw3PreviewExecutor : IIw3PreviewExecutor
{
    private readonly ILocalProcessRunner _processRunner;
    private readonly IPreviewCacheFileService _fileService;
    private readonly Iw3CommandBuilder _commandBuilder;

    public LocalIw3PreviewExecutor(
        ILocalProcessRunner? processRunner = null,
        IPreviewCacheFileService? fileService = null,
        Iw3CommandBuilder? commandBuilder = null)
    {
        _processRunner = processRunner ?? new BundledLocalProcessRunner();
        _fileService = fileService ?? new FileSystemPreviewCacheFileService();
        _commandBuilder = commandBuilder ?? new Iw3CommandBuilder();
    }

    public async Task<PreviewGenerationResult> ExecuteAsync(
        Iw3PreviewGenerationRequest request,
        IProgress<ConversionExecutionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;
        var logs = new List<ConversionExecutionLogEntry>();
        var runtimeDownloadDetected = false;
        DateTimeOffset? sourceClipStartedAt = null;
        DateTimeOffset? sourceClipCompletedAt = null;
        DateTimeOffset? iw3StartedAt = null;
        DateTimeOffset? iw3CompletedAt = null;
        DateTimeOffset? firstIw3OutputAt = null;
        DateTimeOffset? firstIw3FrameProgressAt = null;
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                request.CancellationToken);
        var linkedToken = linkedCancellationTokenSource.Token;

        if (linkedToken.IsCancellationRequested)
        {
            return CreateCanceledResult(request, startedAt, logs);
        }

        if (!ValidatePreviewCachePaths(request.CachePaths, logs))
        {
            return CreateFailedResult(
                request,
                startedAt,
                logs,
                "Preview could not start because one or more preview paths were outside the preview cache. No preview files were created.",
                "La vista previa no pudo iniciar porque una o mas rutas de vista previa estaban fuera del cache de vista previa. No se crearon archivos de vista previa.");
        }

        if (!Iw3DepthModelMapper.TryMap(request.SelectedLocalModel, out _))
        {
            return CreateFailedResult(
                request,
                startedAt,
                logs,
                "Preview could not start because the selected model is not mapped to a verified iw3 depth model.",
                "La vista previa no pudo iniciar porque el modelo seleccionado no esta mapeado a un modelo de profundidad iw3 verificado.");
        }

        try
        {
            progress?.Report(CreateProgress(
                1,
                "Preparing preview...",
                "Preparando vista previa..."));

            PrepareCachePaths(request.CachePaths);
            logs.Add(CreateLog(
                $"Preview cache prepared: {request.CachePaths.CacheDirectory}",
                $"Cache de vista previa preparado: {request.CachePaths.CacheDirectory}"));

            progress?.Report(CreateProgress(
                5,
                "Creating source clip...",
                "Creando clip de origen..."));

            sourceClipStartedAt = DateTimeOffset.UtcNow;
            logs.Add(CreateLog(
                "Timing checkpoint: source clip generation started.",
                "Punto de tiempo: generacion del clip fuente iniciada."));
            var sourceClipExecution = await CreateShortSourceClipWithFallbackAsync(
                request,
                logs,
                progress,
                linkedToken);
            var ffmpegResult = sourceClipExecution.ProcessResult;
            sourceClipCompletedAt = ffmpegResult.EndedAt;
            logs.Add(CreateLog(
                "Timing checkpoint: source clip generation completed.",
                "Punto de tiempo: generacion del clip fuente completada."));
            if (ffmpegResult.WasCanceled || linkedToken.IsCancellationRequested)
            {
                AddPreviewTimingLogs(
                    logs,
                    sourceClipStartedAt,
                    sourceClipCompletedAt,
                    iw3StartedAt,
                    iw3CompletedAt,
                    firstIw3OutputAt,
                    firstIw3FrameProgressAt,
                    runtimeDownloadDetected);
                CleanupPartialAndTempFiles(request.CachePaths, logs, startedAt);
                return CreateCanceledResult(request, startedAt, logs);
            }

            if (!ProcessSucceeded(ffmpegResult) ||
                !_fileService.Exists(request.CachePaths.PartialShortSourcePath))
            {
                AddPreviewTimingLogs(
                    logs,
                    sourceClipStartedAt,
                    sourceClipCompletedAt,
                    iw3StartedAt,
                    iw3CompletedAt,
                    firstIw3OutputAt,
                    firstIw3FrameProgressAt,
                    runtimeDownloadDetected);
                CleanupPartialAndTempFiles(request.CachePaths, logs, startedAt);
                return CreateFailedResult(
                    request,
                    startedAt,
                    logs,
                    "Preview source clip generation failed. FFmpeg stderr was logged; unsupported subtitle, data, and attachment streams are excluded from preview sources. Fast stream-copy was retried once with safe reencode. No iw3 preview process was started.",
                    "La generacion del clip de origen para vista previa fallo. Se registro stderr de FFmpeg; subtitulos, datos y adjuntos no se incluyen en las fuentes de vista previa. La copia rapida se reintento una vez con recodificacion segura. No se inicio el proceso iw3 de vista previa.");
            }

            _fileService.Move(
                request.CachePaths.PartialShortSourcePath,
                request.CachePaths.ShortSourcePath,
                overwrite: true);
            logs.Add(CreateLog(
                $"Preview source clip prepared: {request.CachePaths.ShortSourcePath}",
                $"Clip de origen para vista previa preparado: {request.CachePaths.ShortSourcePath}"));

            progress?.Report(CreateProgress(
                35,
                "Starting iw3...",
                "Iniciando iw3..."));

            iw3StartedAt = DateTimeOffset.UtcNow;
            logs.Add(CreateLog(
                "Timing checkpoint: iw3 process starting.",
                "Punto de tiempo: inicio del proceso iw3."));
            var iw3ProcessRequest = CreateIw3PreviewRequest(
                request,
                progress,
                ObserveIw3OutputLine);
            logs.AddRange(Iw3ProcessDiagnostics.CreateCommandLogs(
                iw3ProcessRequest,
                new(
                    EnglishOperationName: "Preview iw3",
                    SpanishOperationName: "Vista previa iw3",
                    InputPath: request.CachePaths.ShortSourcePath,
                    ProcessOutputPath: request.CachePaths.PartialPreviewOutputPath,
                    FinalOutputPath: request.CachePaths.PreviewOutputPath,
                    OutputContainer: request.OutputContainer,
                    QualityPreset: request.QualityPreset,
                    Intensity: request.Intensity,
                    ThreeDOutputFormat: request.ThreeDOutputFormat,
                    SelectedLocalModel: request.SelectedLocalModel)));
            var iw3Result = await _processRunner.RunAsync(
                iw3ProcessRequest,
                linkedToken);
            iw3CompletedAt = iw3Result.EndedAt;
            ObserveIw3ResultOutput(iw3Result, ObserveIw3OutputLine);
            runtimeDownloadDetected |= Iw3RuntimeDownloadDetector.ContainsRuntimeDownload(iw3Result);
            AddIw3TimingCheckpointLogs(
                logs,
                firstIw3OutputAt,
                firstIw3FrameProgressAt,
                iw3CompletedAt);
            logs.AddRange(Iw3ProcessDiagnostics.CreateTimingLogs(
                "Preview iw3",
                "Vista previa iw3",
                iw3Result));
            AddProcessLogs(logs, iw3Result);
            AddOfflineDependencyLogs(logs, iw3CompletedAt.Value, runtimeDownloadDetected);
            AddPreviewTimingLogs(
                logs,
                sourceClipStartedAt,
                sourceClipCompletedAt,
                iw3StartedAt,
                iw3CompletedAt,
                firstIw3OutputAt,
                firstIw3FrameProgressAt,
                runtimeDownloadDetected);
            if (iw3Result.WasCanceled || linkedToken.IsCancellationRequested)
            {
                CleanupPartialAndTempFiles(request.CachePaths, logs, startedAt);
                return CreateCanceledResult(request, startedAt, logs);
            }

            if (!ProcessSucceeded(iw3Result) ||
                !_fileService.Exists(request.CachePaths.PartialPreviewOutputPath))
            {
                CleanupPartialAndTempFiles(request.CachePaths, logs, startedAt);
                return CreateFailedResult(
                    request,
                    startedAt,
                    logs,
                    "Preview conversion failed. Partial preview files were deleted.",
                    "La conversion de vista previa fallo. Los archivos parciales de vista previa fueron eliminados.");
            }

            _fileService.Move(
                request.CachePaths.PartialPreviewOutputPath,
                request.CachePaths.PreviewOutputPath,
                overwrite: true);
            CleanupPartialAndTempFiles(request.CachePaths, logs, startedAt);
            progress?.Report(CreateProgress(
                100,
                "Preview completed.",
                "Vista previa completada."));
            logs.Add(CreateLog(
                $"Preview saved to {request.CachePaths.PreviewOutputPath}",
                $"Vista previa guardada en {request.CachePaths.PreviewOutputPath}"));

            return new(
                Success: true,
                WasCanceled: false,
                Status: PreviewGenerationStatus.Ready,
                PreviewOutputPath: request.CachePaths.PreviewOutputPath,
                CachePaths: request.CachePaths,
                StartedAt: startedAt,
                FinishedAt: DateTimeOffset.UtcNow,
                EnglishSummary: CreatePreviewCompletionSummary(
                    runtimeDownloadDetected,
                    sourceClipStartedAt,
                    sourceClipCompletedAt,
                    iw3StartedAt,
                    iw3CompletedAt,
                    firstIw3OutputAt,
                    firstIw3FrameProgressAt,
                    useSpanish: false),
                SpanishSummary: CreatePreviewCompletionSummary(
                    runtimeDownloadDetected,
                    sourceClipStartedAt,
                    sourceClipCompletedAt,
                    iw3StartedAt,
                    iw3CompletedAt,
                    firstIw3OutputAt,
                    firstIw3FrameProgressAt,
                    useSpanish: true),
                Logs: logs);
        }
        catch (OperationCanceledException)
        {
            CleanupPartialAndTempFiles(request.CachePaths, logs, startedAt);
            return CreateCanceledResult(request, startedAt, logs);
        }
        catch (Exception exception)
        {
            CleanupPartialAndTempFiles(request.CachePaths, logs, startedAt);
            return CreateFailedResult(
                request,
                startedAt,
                logs,
                $"Preview generation failed unexpectedly: {exception.Message}",
                $"La generacion de vista previa fallo inesperadamente: {exception.Message}");
        }

        void ObserveIw3OutputLine(ProcessOutputLine line)
        {
            firstIw3OutputAt ??= line.CapturedAt;
            if (firstIw3FrameProgressAt is null &&
                IsIw3FrameOrProgressLine(line.Text))
            {
                firstIw3FrameProgressAt = line.CapturedAt;
            }

            if (Iw3RuntimeDownloadDetector.IsRuntimeDownloadLine(line.Text))
            {
                runtimeDownloadDetected = true;
            }
        }
    }

    private void PrepareCachePaths(PreviewCachePaths paths)
    {
        if (!paths.AreAllPathsInsideCache)
        {
            throw new InvalidOperationException(
                "Preview cache paths must be inside the preview cache directory.");
        }

        _fileService.EnsureDirectory(paths.CacheDirectory);
        foreach (var path in paths.AllPaths)
        {
            _fileService.DeleteIfExists(path);
        }
    }

    private static bool ValidatePreviewCachePaths(
        PreviewCachePaths paths,
        ICollection<ConversionExecutionLogEntry> logs)
    {
        var outsidePaths = paths.PathsOutsideCache;
        if (outsidePaths.Count == 0)
        {
            return true;
        }

        foreach (var path in outsidePaths)
        {
            logs.Add(CreateLog(
                $"Preview path safety violation: {path} is outside preview cache {paths.CacheDirectory}.",
                $"Violacion de seguridad de ruta de vista previa: {path} esta fuera del cache de vista previa {paths.CacheDirectory}."));
        }

        return false;
    }

    private async Task<PreviewSourceClipExecutionResult> CreateShortSourceClipWithFallbackAsync(
        Iw3PreviewGenerationRequest request,
        ICollection<ConversionExecutionLogEntry> logs,
        IProgress<ConversionExecutionProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        logs.Add(CreateLog(
            "Preview source clip fast stream-copy started.",
            "Copia rapida del clip fuente de vista previa iniciada."));
        var fastRequest = CreateFastShortSourceClipRequest(request, progress);
        AddSourceClipDiagnosticLogs(
            logs,
            request,
            fastRequest,
            "fast stream-copy",
            "copia rapida de streams");
        var fastResult = await _processRunner.RunAsync(
            fastRequest,
            cancellationToken);
        AddProcessLogs(logs, fastResult);
        AddLogs(logs, Iw3ProcessDiagnostics.CreateTimingLogs(
            "Preview source clip",
            "Clip fuente de vista previa",
            fastResult));
        if (fastResult.WasCanceled ||
            cancellationToken.IsCancellationRequested ||
            (ProcessSucceeded(fastResult) &&
                _fileService.Exists(request.CachePaths.PartialShortSourcePath)))
        {
            return new(fastResult);
        }

        try
        {
            _fileService.DeleteIfExists(request.CachePaths.PartialShortSourcePath);
        }
        catch (Exception exception)
        {
            logs.Add(CreateLog(
                $"Preview source clip fallback cleanup warning: {exception.Message}",
                $"Advertencia de limpieza antes del fallback del clip fuente: {exception.Message}"));
        }

        logs.Add(CreateLog(
            "Fast preview source stream-copy failed; retrying with safe reencode.",
            "La copia rapida del clip fuente fallo; reintentando con recodificacion segura."));
        var fallbackRequest = CreateSafeReencodeShortSourceClipRequest(request, progress);
        AddSourceClipDiagnosticLogs(
            logs,
            request,
            fallbackRequest,
            "safe H.264/AAC reencode fallback",
            "fallback de recodificacion segura H.264/AAC");
        var fallbackResult = await _processRunner.RunAsync(
            fallbackRequest,
            cancellationToken);
        AddProcessLogs(logs, fallbackResult);
        AddLogs(logs, Iw3ProcessDiagnostics.CreateTimingLogs(
            "Preview source clip",
            "Clip fuente de vista previa",
            fallbackResult));

        return new(fallbackResult);
    }

    private static void AddSourceClipDiagnosticLogs(
        ICollection<ConversionExecutionLogEntry> logs,
        Iw3PreviewGenerationRequest request,
        ProcessExecutionRequest processRequest,
        string englishMode,
        string spanishMode)
    {
        logs.Add(CreateLog(
            $"Preview source clip diagnostics: mode {englishMode}; sanitized command line: {Iw3ProcessDiagnostics.FormatCommandLine(processRequest)}",
            $"Diagnostico del clip fuente de vista previa: modo {spanishMode}; linea de comando saneada: {Iw3ProcessDiagnostics.FormatCommandLine(processRequest)}"));
        logs.Add(CreateLog(
            $"Preview source clip diagnostics: input file path: {request.SourcePath}; process output file path: {request.CachePaths.PartialShortSourcePath}; promoted source path: {request.CachePaths.ShortSourcePath}.",
            $"Diagnostico del clip fuente de vista previa: ruta de entrada: {request.SourcePath}; ruta de salida del proceso: {request.CachePaths.PartialShortSourcePath}; ruta fuente promovida: {request.CachePaths.ShortSourcePath}."));
        logs.Add(CreateLog(
            "Preview source clip diagnostics: maps 0:v:0 and optional 0:a:0?, excludes subtitles/data/attachments, drops metadata/chapters, uses input seeking before -i, does not use copyts, and sets -avoid_negative_ts make_zero for fast stream-copy.",
            "Diagnostico del clip fuente de vista previa: mapea 0:v:0 y audio opcional 0:a:0?, excluye subtitulos/datos/adjuntos, omite metadata/capitulos, usa seek de entrada antes de -i, no usa copyts y configura -avoid_negative_ts make_zero para copia rapida."));
    }

    private ProcessExecutionRequest CreateFastShortSourceClipRequest(
        Iw3PreviewGenerationRequest request,
        IProgress<ConversionExecutionProgressUpdate>? progress) => new(
        ExecutablePath: request.ExpectedToolPaths.FfmpegExecutable,
        Arguments:
        [
            "-y",
            "-ss",
            FormatTime(request.Configuration.PreviewStartTime),
            "-i",
            request.SourcePath,
            "-t",
            FormatTime(request.Configuration.PreviewDuration),
            "-map",
            "0:v:0",
            "-map",
            "0:a:0?",
            "-sn",
            "-dn",
            "-map_metadata",
            "-1",
            "-map_chapters",
            "-1",
            "-c",
            "copy",
            "-avoid_negative_ts",
            "make_zero",
            request.CachePaths.PartialShortSourcePath,
        ],
        WorkingDirectory: Path.GetDirectoryName(request.ExpectedToolPaths.FfmpegExecutable),
        AllowedRootDirectory: Path.GetDirectoryName(request.ExpectedToolPaths.FfmpegExecutable),
        OutputProgress: CreateOutputProgress(
            progress,
            15,
            "Creating source clip...",
            "Creando clip de origen..."),
        MetricsProgress: CreateMetricsProgress(
            progress,
            "Creating source clip...",
            "Creando clip de origen..."));

    private ProcessExecutionRequest CreateSafeReencodeShortSourceClipRequest(
        Iw3PreviewGenerationRequest request,
        IProgress<ConversionExecutionProgressUpdate>? progress) => new(
        ExecutablePath: request.ExpectedToolPaths.FfmpegExecutable,
        Arguments:
        [
            "-y",
            "-ss",
            FormatTime(request.Configuration.PreviewStartTime),
            "-i",
            request.SourcePath,
            "-t",
            FormatTime(request.Configuration.PreviewDuration),
            "-map",
            "0:v:0",
            "-map",
            "0:a:0?",
            "-sn",
            "-dn",
            "-map_metadata",
            "-1",
            "-map_chapters",
            "-1",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "23",
            "-pix_fmt",
            "yuv420p",
            "-c:a",
            "aac",
            "-b:a",
            "160k",
            "-ac",
            "2",
            request.CachePaths.PartialShortSourcePath,
        ],
        WorkingDirectory: Path.GetDirectoryName(request.ExpectedToolPaths.FfmpegExecutable),
        AllowedRootDirectory: Path.GetDirectoryName(request.ExpectedToolPaths.FfmpegExecutable),
        OutputProgress: CreateOutputProgress(
            progress,
            15,
            "Creating source clip...",
            "Creando clip de origen..."),
        MetricsProgress: CreateMetricsProgress(
            progress,
            "Creating source clip...",
            "Creando clip de origen..."));

    private ProcessExecutionRequest CreateIw3PreviewRequest(
        Iw3PreviewGenerationRequest request,
        IProgress<ConversionExecutionProgressUpdate>? progress,
        Action<ProcessOutputLine>? observeOutputLine)
    {
        var command = _commandBuilder.Build(
            new ConversionRequest(
                InputPath: request.CachePaths.ShortSourcePath,
                OutputPath: request.CachePaths.PartialPreviewOutputPath,
                OutputContainer: request.OutputContainer,
                ThreeDOutputFormat: request.ThreeDOutputFormat,
                AiQualityPreset: request.QualityPreset,
                ThreeDIntensity: request.Intensity),
            request.ExpectedToolPaths,
            CreateCompleteHealthStatus(),
            request.SelectedLocalModel,
            requireVerifiedDepthModel: true);

        return new(
            ExecutablePath: command.ExecutablePath,
            Arguments: command.Arguments,
            WorkingDirectory: request.ExpectedToolPaths.NunifRootDirectory,
            EnvironmentVariables: Iw3BundledRuntimeEnvironment.Create(request.ExpectedToolPaths),
            AllowedRootDirectory: request.ExpectedToolPaths.Iw3EngineDirectory,
            OutputProgress: CreateOutputProgress(
                progress,
                55,
                "Running iw3 preview conversion...",
                "Ejecutando conversion de vista previa iw3...",
                detectRuntimeDownload: true,
                observeOutputLine),
            MetricsProgress: CreateMetricsProgress(
                progress,
                "Running iw3 preview conversion...",
                "Ejecutando conversion de vista previa iw3..."));
    }

    private static IProgress<ProcessOutputLine>? CreateOutputProgress(
        IProgress<ConversionExecutionProgressUpdate>? progress,
        int progressPercent,
        string englishStage,
        string spanishStage,
        bool detectRuntimeDownload = false,
        Action<ProcessOutputLine>? observeOutputLine = null) =>
        progress is null
            ? null
            : new DelegateProgress<ProcessOutputLine>(line =>
            {
                observeOutputLine?.Invoke(line);
                var runtimeDownloadDetected = detectRuntimeDownload &&
                    Iw3RuntimeDownloadDetector.IsRuntimeDownloadLine(line.Text);
                progress.Report(new(
                    ProgressPercent: progressPercent,
                    CurrentStep: new(
                        englishStage,
                        spanishStage),
                    DetailEnglish: runtimeDownloadDetected
                        ? Iw3RuntimeDownloadDetector.EnglishWarning
                        : englishStage,
                    DetailSpanish: runtimeDownloadDetected
                        ? Iw3RuntimeDownloadDetector.SpanishWarning
                        : spanishStage,
                    OutputLine: line));
            });

    private static IProgress<ProcessMetricSample>? CreateMetricsProgress(
        IProgress<ConversionExecutionProgressUpdate>? progress,
        string englishStage,
        string spanishStage) =>
        progress is null
            ? null
            : new DelegateProgress<ProcessMetricSample>(metrics => progress.Report(new(
                ProgressPercent: 0,
                CurrentStep: new(
                    englishStage,
                    spanishStage),
                DetailEnglish: englishStage,
                DetailSpanish: spanishStage,
                Metrics: metrics)));

    private static EngineHealthStatus CreateCompleteHealthStatus() => new(
        Ffmpeg: ToolHealthStatus.Found,
        Ffprobe: ToolHealthStatus.Found,
        Python: ToolHealthStatus.Found,
        Iw3EngineDirectory: ToolHealthStatus.Found,
        ModelsDirectory: ToolHealthStatus.Found);

    private static bool ProcessSucceeded(ProcessExecutionResult result) =>
        result.Status == ProcessExecutionStatus.Completed &&
        result.ExitCode == 0;

    private static void ObserveIw3ResultOutput(
        ProcessExecutionResult result,
        Action<ProcessOutputLine> observeOutputLine)
    {
        foreach (var line in EnumerateProcessOutputLines(result))
        {
            observeOutputLine(line);
        }
    }

    private static IEnumerable<ProcessOutputLine> EnumerateProcessOutputLines(
        ProcessExecutionResult result)
    {
        if (result.OutputLines.Count > 0)
        {
            return result.OutputLines;
        }

        return EnumerateCapturedLines(
                result.StandardOutput,
                ProcessOutputStream.StandardOutput,
                result.EndedAt)
            .Concat(EnumerateCapturedLines(
                result.StandardError,
                ProcessOutputStream.StandardError,
                result.EndedAt));
    }

    private static IEnumerable<ProcessOutputLine> EnumerateCapturedLines(
        string text,
        ProcessOutputStream stream,
        DateTimeOffset capturedAt) =>
        SplitProcessText(text)
            .Select(line => new ProcessOutputLine(stream, line, capturedAt));

    private static void AddIw3TimingCheckpointLogs(
        ICollection<ConversionExecutionLogEntry> logs,
        DateTimeOffset? firstIw3OutputAt,
        DateTimeOffset? firstIw3FrameProgressAt,
        DateTimeOffset? iw3CompletedAt)
    {
        logs.Add(firstIw3OutputAt is null
            ? CreateLog(
                "Timing checkpoint: first iw3 output was not observed.",
                "Punto de tiempo: no se observo la primera salida de iw3.")
            : CreateLog(
                "Timing checkpoint: first iw3 output received.",
                "Punto de tiempo: primera salida de iw3 recibida."));
        logs.Add(firstIw3FrameProgressAt is null
            ? CreateLog(
                "Timing checkpoint: first iw3 frame/progress line was not observed.",
                "Punto de tiempo: no se observo la primera linea de cuadro/progreso iw3.")
            : CreateLog(
                "Timing checkpoint: first iw3 frame/progress line received.",
                "Punto de tiempo: primera linea de cuadro/progreso iw3 recibida."));
        if (iw3CompletedAt is not null)
        {
            logs.Add(CreateLog(
                "Timing checkpoint: iw3 preview completed.",
                "Punto de tiempo: vista previa iw3 completada."));
        }
    }

    private static void AddOfflineDependencyLogs(
        ICollection<ConversionExecutionLogEntry> logs,
        DateTimeOffset timestamp,
        bool runtimeDownloadDetected)
    {
        if (!runtimeDownloadDetected)
        {
            return;
        }

        logs.Add(Iw3RuntimeDownloadDetector.CreateWarningLog(timestamp));
        logs.Add(Iw3RuntimeDownloadDetector.CreateTimingNoteLog(timestamp));
    }

    private static void AddPreviewTimingLogs(
        ICollection<ConversionExecutionLogEntry> logs,
        DateTimeOffset? sourceClipStartedAt,
        DateTimeOffset? sourceClipCompletedAt,
        DateTimeOffset? iw3StartedAt,
        DateTimeOffset? iw3CompletedAt,
        DateTimeOffset? firstIw3OutputAt,
        DateTimeOffset? firstIw3FrameProgressAt,
        bool runtimeDownloadDetected)
    {
        var (englishSummary, spanishSummary) = CreatePreviewTimingSummary(
            sourceClipStartedAt,
            sourceClipCompletedAt,
            iw3StartedAt,
            iw3CompletedAt,
            firstIw3OutputAt,
            firstIw3FrameProgressAt,
            runtimeDownloadDetected);
        logs.Add(CreateLog(englishSummary, spanishSummary));
    }

    private static string CreatePreviewCompletionSummary(
        bool runtimeDownloadDetected,
        DateTimeOffset? sourceClipStartedAt,
        DateTimeOffset? sourceClipCompletedAt,
        DateTimeOffset? iw3StartedAt,
        DateTimeOffset? iw3CompletedAt,
        DateTimeOffset? firstIw3OutputAt,
        DateTimeOffset? firstIw3FrameProgressAt,
        bool useSpanish)
    {
        var (englishTiming, spanishTiming) = CreatePreviewTimingSummary(
            sourceClipStartedAt,
            sourceClipCompletedAt,
            iw3StartedAt,
            iw3CompletedAt,
            firstIw3OutputAt,
            firstIw3FrameProgressAt,
            runtimeDownloadDetected);
        var timing = useSpanish ? spanishTiming : englishTiming;
        var baseSummary = useSpanish
            ? "La vista previa se genero correctamente."
            : "Preview generated successfully.";
        var warning = runtimeDownloadDetected
            ? useSpanish
                ? $" {Iw3RuntimeDownloadDetector.SpanishWarning}"
                : $" {Iw3RuntimeDownloadDetector.EnglishWarning}"
            : string.Empty;
        return $"{baseSummary}{warning} {timing}";
    }

    private static (string English, string Spanish) CreatePreviewTimingSummary(
        DateTimeOffset? sourceClipStartedAt,
        DateTimeOffset? sourceClipCompletedAt,
        DateTimeOffset? iw3StartedAt,
        DateTimeOffset? iw3CompletedAt,
        DateTimeOffset? firstIw3OutputAt,
        DateTimeOffset? firstIw3FrameProgressAt,
        bool runtimeDownloadDetected)
    {
        var sourceClipDuration = FormatDurationBetween(
            sourceClipStartedAt,
            sourceClipCompletedAt);
        var iw3StartupDuration = firstIw3OutputAt is null
            ? "n/a"
            : FormatDurationBetween(iw3StartedAt, firstIw3OutputAt);
        var conversionStartedAt = firstIw3FrameProgressAt ?? firstIw3OutputAt ?? iw3StartedAt;
        var iw3ConversionDuration = FormatDurationBetween(
            conversionStartedAt,
            iw3CompletedAt);
        var englishDownloadNote = runtimeDownloadDetected
            ? $" {Iw3RuntimeDownloadDetector.EnglishTimingNote}"
            : string.Empty;
        var spanishDownloadNote = runtimeDownloadDetected
            ? $" {Iw3RuntimeDownloadDetector.SpanishTimingNote}"
            : string.Empty;

        return (
            $"Preview timings: source clip {sourceClipDuration}, iw3 startup {iw3StartupDuration}, conversion {iw3ConversionDuration}.{englishDownloadNote}",
            $"Tiempos de vista previa: clip fuente {sourceClipDuration}, inicio iw3 {iw3StartupDuration}, conversion {iw3ConversionDuration}.{spanishDownloadNote}");
    }

    private static string FormatDurationBetween(
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt)
    {
        if (startedAt is null || endedAt is null)
        {
            return "n/a";
        }

        var duration = endedAt.Value - startedAt.Value;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return FormatDuration(duration);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalSeconds = Math.Max(0, (int)Math.Round(duration.TotalSeconds));
        if (totalSeconds >= 3600)
        {
            return $"{totalSeconds / 3600}h {(totalSeconds % 3600) / 60}m {totalSeconds % 60}s";
        }

        if (totalSeconds >= 60)
        {
            return $"{totalSeconds / 60}m {totalSeconds % 60}s";
        }

        return $"{totalSeconds}s";
    }

    private static bool IsIw3FrameOrProgressLine(string text) =>
        Iw3ProcessDiagnostics.IsFrameOrProgressLine(text);

    private void CleanupPartialAndTempFiles(
        PreviewCachePaths paths,
        ICollection<ConversionExecutionLogEntry> logs,
        DateTimeOffset attemptStartedAtUtc)
    {
        var cleanedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in new[]
        {
            paths.PartialShortSourcePath,
            paths.ShortSourcePath,
            paths.PartialPreviewOutputPath,
        })
        {
            TryDeletePreviewCachePath(paths.CacheDirectory, path, logs, cleanedPaths);
        }

        try
        {
            foreach (var file in _fileService.EnumerateFiles(paths.CacheDirectory))
            {
                if (cleanedPaths.Contains(file.Path) ||
                    !IsCurrentAttemptPartialFile(paths, file, attemptStartedAtUtc))
                {
                    continue;
                }

                TryDeletePreviewCachePath(paths.CacheDirectory, file.Path, logs, cleanedPaths);
            }
        }
        catch (Exception exception)
        {
            logs.Add(CreateLog(
                $"Preview partial cleanup warning: {exception.Message}",
                $"Advertencia de limpieza de parciales de vista previa: {exception.Message}"));
        }
    }

    private void TryDeletePreviewCachePath(
        string cacheDirectory,
        string path,
        ICollection<ConversionExecutionLogEntry> logs,
        ISet<string> cleanedPaths)
    {
        if (!PreviewCachePathSafety.IsPathInsideRoot(cacheDirectory, path))
        {
            logs.Add(CreateLog(
                $"Preview cleanup skipped path outside cache: {path}",
                $"Limpieza de vista previa omitio ruta fuera del cache: {path}"));
            return;
        }

        try
        {
            _fileService.DeleteIfExists(path);
            cleanedPaths.Add(path);
        }
        catch (Exception exception)
        {
            logs.Add(CreateLog(
                $"Preview cleanup warning for {path}: {exception.Message}",
                $"Advertencia de limpieza de vista previa para {path}: {exception.Message}"));
        }
    }

    private static bool IsCurrentAttemptPartialFile(
        PreviewCachePaths paths,
        PreviewCacheFile file,
        DateTimeOffset attemptStartedAtUtc)
    {
        if (!PreviewCacheCleaner.IsPreviewPartialFilePath(
                paths.CacheDirectory,
                file.Path) ||
            file.LastWriteTimeUtc < attemptStartedAtUtc.AddSeconds(-2))
        {
            return false;
        }

        var fileName = Path.GetFileName(file.Path);
        var shortSourcePartialName = Path.GetFileName(paths.PartialShortSourcePath);
        var previewPartialName = Path.GetFileName(paths.PartialPreviewOutputPath);

        return string.Equals(fileName, shortSourcePartialName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, previewPartialName, StringComparison.OrdinalIgnoreCase) ||
            IsCurrentAttemptTempPartialName(fileName);
    }

    private static bool IsCurrentAttemptTempPartialName(string fileName) =>
        fileName.StartsWith("_tmp_", StringComparison.OrdinalIgnoreCase) &&
        (fileName.Contains(".preview.v3dfy-partial.", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains(".source.v3dfy-partial.", StringComparison.OrdinalIgnoreCase));

    private static PreviewGenerationResult CreateCanceledResult(
        Iw3PreviewGenerationRequest request,
        DateTimeOffset startedAt,
        IReadOnlyList<ConversionExecutionLogEntry> logs) => new(
        Success: false,
        WasCanceled: true,
        Status: PreviewGenerationStatus.Canceled,
        PreviewOutputPath: null,
        CachePaths: request.CachePaths,
        StartedAt: startedAt,
        FinishedAt: DateTimeOffset.UtcNow,
        EnglishSummary: "Preview generation was canceled.",
        SpanishSummary: "La generacion de vista previa fue cancelada.",
        Logs: [.. logs, CreateLog(
            "Preview canceled. Partial preview files were deleted.",
            "Vista previa cancelada. Los archivos parciales fueron eliminados.")]);

    private static PreviewGenerationResult CreateFailedResult(
        Iw3PreviewGenerationRequest request,
        DateTimeOffset startedAt,
        IReadOnlyList<ConversionExecutionLogEntry> logs,
        string englishSummary,
        string spanishSummary) => new(
        Success: false,
        WasCanceled: false,
        Status: PreviewGenerationStatus.Failed,
        PreviewOutputPath: null,
        CachePaths: request.CachePaths,
        StartedAt: startedAt,
        FinishedAt: DateTimeOffset.UtcNow,
        EnglishSummary: englishSummary,
        SpanishSummary: spanishSummary,
        Logs: logs);

    private static ConversionExecutionProgressUpdate CreateProgress(
        int progressPercent,
        string englishStep,
        string spanishStep) => new(
        ProgressPercent: progressPercent,
        CurrentStep: new(englishStep, spanishStep),
        DetailEnglish: englishStep,
        DetailSpanish: spanishStep);

    private static void AddProcessLogs(
        ICollection<ConversionExecutionLogEntry> logs,
        ProcessExecutionResult result)
    {
        if (result.OutputLines.Count > 0)
        {
            foreach (var line in result.OutputLines)
            {
                var prefix = line.Stream == ProcessOutputStream.StandardError
                    ? "stderr"
                    : "stdout";
                logs.Add(CreateLog($"{prefix}: {line.Text}", $"{prefix}: {line.Text}"));
            }

            return;
        }

        AddCapturedText(logs, "stdout", result.StandardOutput);
        AddCapturedText(logs, "stderr", result.StandardError);
    }

    private static void AddCapturedText(
        ICollection<ConversionExecutionLogEntry> logs,
        string prefix,
        string text)
    {
        foreach (var line in SplitProcessText(text))
        {
            logs.Add(CreateLog($"{prefix}: {line}", $"{prefix}: {line}"));
        }
    }

    private static IEnumerable<string> SplitProcessText(string text) =>
        text.Split(
                ["\r\n", "\n", "\r"],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd());

    private static ConversionExecutionLogEntry CreateLog(
        string englishMessage,
        string spanishMessage) => new(
        DateTimeOffset.UtcNow,
        englishMessage,
        spanishMessage);

    private static void AddLogs(
        ICollection<ConversionExecutionLogEntry> logs,
        IEnumerable<ConversionExecutionLogEntry> entries)
    {
        foreach (var entry in entries)
        {
            logs.Add(entry);
        }
    }

    private static string FormatTime(TimeSpan value) =>
        value.ToString(@"hh\:mm\:ss");

    private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed record PreviewSourceClipExecutionResult(
        ProcessExecutionResult ProcessResult);
}
