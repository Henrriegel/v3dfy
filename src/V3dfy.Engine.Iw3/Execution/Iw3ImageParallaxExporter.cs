using System.Globalization;
using System.Diagnostics;
using V3dfy.Core.Image;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Engine.Iw3.Execution;

public sealed class Iw3ImageParallaxExporter : IImageParallaxExporter
{
    private const string BlockedEnglishSummary =
        "Bundled iw3 2.5D image conversion is not ready.";
    private const string BlockedSpanishSummary =
        "La conversion de imagen 2.5D con iw3 incluido no esta lista.";
    private const int DefaultFramesPerSecond = 24;

    private readonly ILocalProcessRunner _processRunner;
    private readonly Iw3ImageDepthExportCommandBuilder _depthExportCommandBuilder;
    private readonly V3dfyParallaxFrameCommandBuilder _frameCommandBuilder;
    private readonly FfmpegParallaxVideoCommandBuilder _ffmpegCommandBuilder;

    public Iw3ImageParallaxExporter(
        ILocalProcessRunner? processRunner = null,
        Iw3ImageDepthExportCommandBuilder? depthExportCommandBuilder = null,
        V3dfyParallaxFrameCommandBuilder? frameCommandBuilder = null,
        FfmpegParallaxVideoCommandBuilder? ffmpegCommandBuilder = null)
    {
        _processRunner = processRunner ?? new BundledLocalProcessRunner();
        _depthExportCommandBuilder = depthExportCommandBuilder ?? new Iw3ImageDepthExportCommandBuilder();
        _frameCommandBuilder = frameCommandBuilder ?? new V3dfyParallaxFrameCommandBuilder();
        _ffmpegCommandBuilder = ffmpegCommandBuilder ?? new FfmpegParallaxVideoCommandBuilder();
    }

    public async Task<ImageParallaxExportResult> ExportAsync(
        ImageParallaxExportRequest request,
        IProgress<ImageParallaxExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var readiness = EvaluateReadiness(request);
        if (!readiness.CanExport)
        {
            return CreateBlockedResult(request, readiness);
        }

        var outputPaths = ImageParallaxExportPathBuilder.CreateOutputPaths(
            request.SourcePath,
            request.OutputDirectory,
            GetModelFileNameForOutput(request.SelectedLocalModel),
            request.Duration,
            File.Exists);

        var workRoot = CreateWorkRoot();
        var depthExportDirectory = Path.Combine(workRoot, "iw3-depth");
        var framesDirectory = Path.Combine(workRoot, "frames");
        Directory.CreateDirectory(depthExportDirectory);
        Directory.CreateDirectory(framesDirectory);

        var selectedModel = request.SelectedLocalModel!;
        var depthCommand = _depthExportCommandBuilder.Build(
            request,
            depthExportDirectory,
            selectedModel);
        var depthMapPath = GetExpectedDepthMapPath(depthExportDirectory, request.SourcePath);
        var frameCount = GetFrameCount(request);
        var frameCommand = _frameCommandBuilder.Build(
            request,
            depthMapPath,
            framesDirectory,
            frameCount);
        var ffmpegCommand = _ffmpegCommandBuilder.Build(
            request,
            framesDirectory,
            outputPaths.PrimaryOutputPath);

        ProcessExecutionResult? depthResult = null;
        ProcessExecutionResult? frameResult = null;
        ProcessExecutionResult? ffmpegResult = null;

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            request.CancellationToken);

        try
        {
            var depthPhaseStartedAt = Stopwatch.StartNew();
            progress?.Report(new(
                0,
                "Starting bundled iw3 depth export. This can take a while for high-resolution images.",
                "Iniciando exportacion de profundidad con iw3 incluido. Esto puede tardar con imagenes de alta resolucion."));
            progress?.Report(new(
                5,
                $"Expected depth output: {depthMapPath}",
                $"Salida de profundidad esperada: {depthMapPath}"));
            depthResult = await RunBundledPythonAsync(
                depthCommand,
                request,
                linkedCancellationTokenSource.Token);
            if (!IsCompletedSuccessfully(depthResult) || !File.Exists(depthMapPath))
            {
                return CreateFailureResult(
                    request,
                    outputPaths,
                    depthMapPath,
                    "Bundled iw3 depth export failed.",
                    "La exportacion de profundidad con iw3 incluido fallo.",
                    depthResult,
                    frameResult,
                    ffmpegResult,
                    depthCommand,
                    frameCommand,
                    ffmpegCommand,
                    $"Expected depth map was not created: {depthMapPath}");
            }

            progress?.Report(new(
                40,
                $"Depth export completed in {FormatElapsed(depthPhaseStartedAt.Elapsed)}.",
                $"Exportacion de profundidad completada en {FormatElapsed(depthPhaseStartedAt.Elapsed)}."));

            var framePhaseStartedAt = Stopwatch.StartNew();
            progress?.Report(new(
                45,
                $"Generating {frameCount} 2.5D parallax frames from the depth map.",
                $"Generando {frameCount} cuadros parallax 2.5D desde el mapa de profundidad."));
            progress?.Report(new(
                48,
                $"Parallax frame directory: {framesDirectory}",
                $"Carpeta de cuadros parallax: {framesDirectory}"));
            var frameProcessTask = RunBundledPythonAsync(
                frameCommand,
                request,
                linkedCancellationTokenSource.Token);
            await ReportFrameGenerationProgressAsync(
                framesDirectory,
                frameCount,
                frameProcessTask,
                progress,
                linkedCancellationTokenSource.Token);
            frameResult = await frameProcessTask;
            var generatedFrameCount = CountGeneratedParallaxFrames(framesDirectory);
            if (!IsCompletedSuccessfully(frameResult) || generatedFrameCount == 0)
            {
                return CreateFailureResult(
                    request,
                    outputPaths,
                    depthMapPath,
                    "2.5D parallax frame generation failed.",
                    "La generacion de cuadros parallax 2.5D fallo.",
                    depthResult,
                    frameResult,
                    ffmpegResult,
                    depthCommand,
                    frameCommand,
                    ffmpegCommand,
                    $"No parallax frames were created under: {framesDirectory}");
            }

            progress?.Report(new(
                70,
                $"Parallax frame generation completed: {generatedFrameCount} frame(s) in {FormatElapsed(framePhaseStartedAt.Elapsed)}.",
                $"Generacion de cuadros parallax completada: {generatedFrameCount} cuadro(s) en {FormatElapsed(framePhaseStartedAt.Elapsed)}."));

            var ffmpegPhaseStartedAt = Stopwatch.StartNew();
            progress?.Report(new(
                75,
                "Starting bundled FFmpeg MP4 encoding.",
                "Iniciando codificacion MP4 con FFmpeg incluido."));
            progress?.Report(new(
                78,
                $"FFmpeg output path: {outputPaths.PrimaryOutputPath}",
                $"Ruta de salida FFmpeg: {outputPaths.PrimaryOutputPath}"));
            ffmpegResult = await RunBundledFfmpegAsync(
                ffmpegCommand,
                request,
                linkedCancellationTokenSource.Token);
            var success = IsCompletedSuccessfully(ffmpegResult) && File.Exists(outputPaths.PrimaryOutputPath);
            if (!success)
            {
                return CreateFailureResult(
                    request,
                    outputPaths,
                    depthMapPath,
                    "Bundled FFmpeg parallax encoding failed.",
                    "La codificacion parallax con FFmpeg incluido fallo.",
                    depthResult,
                    frameResult,
                    ffmpegResult,
                    depthCommand,
                    frameCommand,
                    ffmpegCommand,
                    $"Expected MP4 output was not created: {outputPaths.PrimaryOutputPath}");
            }

            progress?.Report(new(
                95,
                $"FFmpeg encoding completed in {FormatElapsed(ffmpegPhaseStartedAt.Elapsed)}.",
                $"Codificacion FFmpeg completada en {FormatElapsed(ffmpegPhaseStartedAt.Elapsed)}."));
            progress?.Report(new(
                100,
                $"2.5D parallax conversion completed: {outputPaths.PrimaryOutputPath}",
                $"Conversion parallax 2.5D completada: {outputPaths.PrimaryOutputPath}"));

            return new(
                Success: true,
                WasBlocked: false,
                EnglishSummary: "2.5D parallax conversion completed.",
                SpanishSummary: "Conversion parallax 2.5D completada.",
                OutputDirectory: request.OutputDirectory,
                GeneratedFiles: outputPaths.GeneratedFiles,
                PrimaryOutputPath: outputPaths.PrimaryOutputPath,
                DepthMapPath: depthMapPath,
                DepthExportExitCode: depthResult.ExitCode,
                FrameGenerationExitCode: frameResult.ExitCode,
                FfmpegExitCode: ffmpegResult.ExitCode,
                DepthExportCommandPreview: depthCommand.FullCommandPreview,
                FrameGenerationCommandPreview: frameCommand.FullCommandPreview,
                FfmpegCommandPreview: ffmpegCommand.FullCommandPreview,
                StandardOutputSummary: CombineProcessSummaries(
                    depthResult.StandardOutput,
                    frameResult.StandardOutput,
                    ffmpegResult.StandardOutput),
                StandardErrorSummary: CombineProcessSummaries(
                    depthResult.StandardError,
                    frameResult.StandardError,
                    ffmpegResult.StandardError));
        }
        finally
        {
            DeleteWorkRootIfSafe(workRoot);
        }
    }

    public static Iw3ImageParallaxExportReadiness EvaluateReadiness(
        ImageParallaxExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<Iw3ImageParallaxExportReadinessIssue>();
        AddFileIssue(
            issues,
            request.SourcePath,
            "source image file",
            "archivo de imagen origen");
        AddDirectoryIssue(
            issues,
            request.OutputDirectory,
            "image output directory",
            "carpeta de salida de imagen");
        AddFileIssue(
            issues,
            request.ExpectedToolPaths.PythonExecutable,
            "embedded Python executable",
            "ejecutable Python incluido");
        AddFileIssue(
            issues,
            request.ExpectedToolPaths.FfmpegExecutable,
            "bundled FFmpeg executable",
            "ejecutable FFmpeg incluido");
        AddDirectoryIssue(
            issues,
            request.ExpectedToolPaths.NunifRootDirectory,
            "nunif root directory",
            "carpeta raiz de nunif");
        AddFileIssue(
            issues,
            Path.Combine(request.ExpectedToolPaths.Iw3PackageDirectory, "__main__.py"),
            "iw3 package entry file",
            "archivo de entrada del paquete iw3");
        AddFileIssue(
            issues,
            request.ExpectedToolPaths.V3dfyParallaxHelperScript,
            "v3dfy parallax helper script",
            "script auxiliar parallax de v3dfy");
        AddDirectoryIssue(
            issues,
            request.ExpectedToolPaths.ModelsDirectory,
            "iw3 pretrained models directory",
            "carpeta de modelos pretrained de iw3");

        if (request.SelectedLocalModel is null)
        {
            issues.Add(new(
                "Select a bundled local depth model before converting.",
                "Selecciona un modelo local de profundidad incluido antes de convertir."));
        }
        else if (!Iw3DepthModelMapper.TryMap(request.SelectedLocalModel, out var mapping) ||
            mapping is null)
        {
            issues.Add(new(
                "Selected local model is not mapped to a verified iw3 depth model.",
                "El modelo local seleccionado no esta mapeado a un modelo de profundidad iw3 verificado."));
        }
        else
        {
            if (mapping.MediaCapability == Iw3DepthModelMediaCapability.VideoOnly)
            {
                issues.Add(new(
                    "Selected local model is not verified for image input.",
                    "El modelo local seleccionado no esta verificado para imagenes."));
            }

            var modelPath = ResolveModelPath(request.ExpectedToolPaths.ModelsDirectory, mapping.ModelRelativePath);
            if (modelPath is null || !File.Exists(modelPath))
            {
                issues.Add(new(
                    $"Selected bundled model file is missing: {mapping.ModelRelativePath}",
                    $"Falta el archivo del modelo incluido seleccionado: {mapping.ModelRelativePath}"));
            }
        }

        var missingDepthOptions = GetMissingDepthExportCapabilityOptions(
            request.DependencyHealth.Iw3CliCapabilities);
        if (missingDepthOptions.Count > 0)
        {
            issues.Add(new(
                "Bundled iw3 depth export is blocked because IW3_CLI_CAPABILITIES.json does not verify " +
                $"{string.Join(", ", missingDepthOptions)}.",
                "La exportacion de profundidad iw3 incluida esta bloqueada porque IW3_CLI_CAPABILITIES.json no verifica " +
                $"{string.Join(", ", missingDepthOptions)}."));
        }

        if (request.FramesPerSecond <= 0)
        {
            issues.Add(new(
                "Parallax frame rate must be greater than zero.",
                "La tasa de cuadros parallax debe ser mayor que cero."));
        }

        return new(issues.Count == 0, issues);
    }

    public static bool HasVerifiedDepthExportCapability(Iw3CliCapabilitiesManifest capabilities) =>
        GetMissingDepthExportCapabilityOptions(capabilities).Count == 0;

    public static IReadOnlyList<string> GetMissingDepthExportCapabilityOptions(
        Iw3CliCapabilitiesManifest capabilities)
    {
        if (!capabilities.HasVerifiedCapabilities)
        {
            return ["verifiedBaseCommand=true"];
        }

        var missing = new List<string>();
        AddMissingOptionGroup(
            missing,
            capabilities,
            $"{Iw3CliContract.InputSwitch} or {Iw3CliContract.InputLongSwitch}",
            Iw3CliContract.InputSwitch,
            Iw3CliContract.InputLongSwitch);
        AddMissingOptionGroup(
            missing,
            capabilities,
            $"{Iw3CliContract.OutputSwitch} or {Iw3CliContract.OutputLongSwitch}",
            Iw3CliContract.OutputSwitch,
            Iw3CliContract.OutputLongSwitch);
        AddMissingOption(missing, capabilities, Iw3CliContract.DepthModelSwitch);
        AddMissingOption(missing, capabilities, Iw3CliContract.ExportSwitch);
        AddMissingOption(missing, capabilities, Iw3CliContract.ExportDepthOnlySwitch);
        AddMissingOption(missing, capabilities, Iw3CliContract.ExportDepthFitSwitch);
        return missing;
    }

    public static int ParseDurationSeconds(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return 6;
        }

        var digits = new string(duration.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? Math.Clamp(seconds, 1, 30)
            : 6;
    }

    private static ProcessExecutionRequest BuildBundledPythonRequest(
        Iw3Command command,
        ImageParallaxExportRequest request) =>
        new(
            ExecutablePath: command.ExecutablePath,
            Arguments: command.Arguments,
            WorkingDirectory: request.ExpectedToolPaths.NunifRootDirectory,
            EnvironmentVariables: Iw3BundledRuntimeEnvironment.Create(request.ExpectedToolPaths),
            AllowedRootDirectory: request.ExpectedToolPaths.Iw3EngineDirectory);

    private static ProcessExecutionRequest BuildBundledFfmpegRequest(
        Iw3Command command,
        ImageParallaxExportRequest request) =>
        new(
            ExecutablePath: command.ExecutablePath,
            Arguments: command.Arguments,
            WorkingDirectory: Path.GetDirectoryName(request.ExpectedToolPaths.FfmpegExecutable),
            AllowedRootDirectory: Path.GetDirectoryName(request.ExpectedToolPaths.FfmpegExecutable));

    private Task<ProcessExecutionResult> RunBundledPythonAsync(
        Iw3Command command,
        ImageParallaxExportRequest request,
        CancellationToken cancellationToken) =>
        _processRunner.RunAsync(BuildBundledPythonRequest(command, request), cancellationToken);

    private Task<ProcessExecutionResult> RunBundledFfmpegAsync(
        Iw3Command command,
        ImageParallaxExportRequest request,
        CancellationToken cancellationToken) =>
        _processRunner.RunAsync(BuildBundledFfmpegRequest(command, request), cancellationToken);

    private static async Task ReportFrameGenerationProgressAsync(
        string framesDirectory,
        int expectedFrameCount,
        Task<ProcessExecutionResult> frameProcessTask,
        IProgress<ImageParallaxExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (progress is null || expectedFrameCount <= 0)
        {
            return;
        }

        var lastReportedFrameCount = 0;
        var lastReportedAt = DateTimeOffset.MinValue;
        var reportFrameStep = Math.Max(1, expectedFrameCount / 12);
        while (!frameProcessTask.IsCompleted)
        {
            await Task.WhenAny(
                    frameProcessTask,
                    Task.Delay(TimeSpan.FromSeconds(2), cancellationToken))
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var generatedFrameCount = CountGeneratedParallaxFrames(framesDirectory);
            var now = DateTimeOffset.UtcNow;
            if (!ShouldReportFrameProgress(
                generatedFrameCount,
                lastReportedFrameCount,
                expectedFrameCount,
                lastReportedAt,
                now,
                reportFrameStep))
            {
                continue;
            }

            ReportFrameProgress(progress, generatedFrameCount, expectedFrameCount);
            lastReportedFrameCount = generatedFrameCount;
            lastReportedAt = now;
        }

        var finalFrameCount = CountGeneratedParallaxFrames(framesDirectory);
        if (finalFrameCount > lastReportedFrameCount)
        {
            ReportFrameProgress(progress, finalFrameCount, expectedFrameCount);
        }
    }

    private static bool ShouldReportFrameProgress(
        int generatedFrameCount,
        int lastReportedFrameCount,
        int expectedFrameCount,
        DateTimeOffset lastReportedAt,
        DateTimeOffset now,
        int reportFrameStep) =>
        generatedFrameCount > 0 &&
        generatedFrameCount != lastReportedFrameCount &&
        (generatedFrameCount >= expectedFrameCount ||
            generatedFrameCount - lastReportedFrameCount >= reportFrameStep ||
            now - lastReportedAt >= TimeSpan.FromSeconds(5));

    private static void ReportFrameProgress(
        IProgress<ImageParallaxExportProgress> progress,
        int generatedFrameCount,
        int expectedFrameCount)
    {
        var boundedFrameCount = Math.Clamp(generatedFrameCount, 0, expectedFrameCount);
        var framePhasePercent = expectedFrameCount <= 0
            ? 0d
            : Math.Min(1d, boundedFrameCount / (double)expectedFrameCount);
        var progressPercent = 48 + (int)Math.Round(framePhasePercent * 22d, MidpointRounding.AwayFromZero);
        progress.Report(new(
            Math.Clamp(progressPercent, 48, 70),
            $"Parallax frames: {boundedFrameCount} / {expectedFrameCount}",
            $"Cuadros parallax: {boundedFrameCount} / {expectedFrameCount}"));
    }

    private static ImageParallaxExportResult CreateBlockedResult(
        ImageParallaxExportRequest request,
        Iw3ImageParallaxExportReadiness readiness)
    {
        var englishDetail = string.Join(" ", readiness.Issues.Select(issue => issue.EnglishMessage));
        var spanishDetail = string.Join(" ", readiness.Issues.Select(issue => issue.SpanishMessage));
        return new(
            Success: false,
            WasBlocked: true,
            EnglishSummary: $"{BlockedEnglishSummary} {englishDetail}",
            SpanishSummary: $"{BlockedSpanishSummary} {spanishDetail}",
            OutputDirectory: request.OutputDirectory,
            GeneratedFiles: [],
            PrimaryOutputPath: null,
            TechnicalDetail: englishDetail);
    }

    private static ImageParallaxExportResult CreateFailureResult(
        ImageParallaxExportRequest request,
        ImageParallaxExportOutputPaths outputPaths,
        string depthMapPath,
        string englishSummary,
        string spanishSummary,
        ProcessExecutionResult? depthResult,
        ProcessExecutionResult? frameResult,
        ProcessExecutionResult? ffmpegResult,
        Iw3Command depthCommand,
        Iw3Command frameCommand,
        Iw3Command ffmpegCommand,
        string technicalDetail)
    {
        var stdout = CombineProcessSummaries(
            depthResult?.StandardOutput ?? string.Empty,
            frameResult?.StandardOutput ?? string.Empty,
            ffmpegResult?.StandardOutput ?? string.Empty);
        var stderr = CombineProcessSummaries(
            depthResult?.StandardError ?? string.Empty,
            frameResult?.StandardError ?? string.Empty,
            ffmpegResult?.StandardError ?? string.Empty);

        return new(
            Success: false,
            WasBlocked: false,
            EnglishSummary: englishSummary,
            SpanishSummary: spanishSummary,
            OutputDirectory: request.OutputDirectory,
            GeneratedFiles: [],
            PrimaryOutputPath: null,
            DepthMapPath: File.Exists(depthMapPath) ? depthMapPath : null,
            TechnicalDetail: $"{technicalDetail} stdout: {stdout}. stderr: {stderr}",
            DepthExportExitCode: depthResult?.ExitCode,
            FrameGenerationExitCode: frameResult?.ExitCode,
            FfmpegExitCode: ffmpegResult?.ExitCode,
            DepthExportCommandPreview: depthCommand.FullCommandPreview,
            FrameGenerationCommandPreview: frameCommand.FullCommandPreview,
            FfmpegCommandPreview: ffmpegCommand.FullCommandPreview,
            StandardOutputSummary: stdout,
            StandardErrorSummary: stderr);
    }

    private static int GetFrameCount(ImageParallaxExportRequest request)
    {
        var fps = request.FramesPerSecond <= 0 ? DefaultFramesPerSecond : request.FramesPerSecond;
        return Math.Clamp(ParseDurationSeconds(request.Duration) * fps, fps, fps * 30);
    }

    private static string GetExpectedDepthMapPath(string depthExportDirectory, string sourcePath) =>
        Path.Combine(
            depthExportDirectory,
            "depth",
            Path.GetFileNameWithoutExtension(sourcePath) + ".png");

    private static bool IsCompletedSuccessfully(ProcessExecutionResult result) =>
        result.Status == ProcessExecutionStatus.Completed && result.ExitCode == 0;

    private static int CountGeneratedParallaxFrames(string framesDirectory)
    {
        try
        {
            return Directory.Exists(framesDirectory)
                ? Directory.EnumerateFiles(framesDirectory, "frame_*.png").Count()
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string GetModelFileNameForOutput(LocalModelPlanSelection? selectedModel) =>
        string.IsNullOrWhiteSpace(selectedModel?.MappingKey)
            ? selectedModel?.DisplayName ?? "model"
            : selectedModel.MappingKey!;

    private static string CreateWorkRoot()
    {
        var workRoot = Path.Combine(
            Path.GetTempPath(),
            "v3dfy",
            "image-parallax",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(workRoot);
        return workRoot;
    }

    private static void DeleteWorkRootIfSafe(string workRoot)
    {
        if (string.IsNullOrWhiteSpace(workRoot) || !Directory.Exists(workRoot))
        {
            return;
        }

        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "v3dfy", "image-parallax"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var fullWorkRoot = Path.GetFullPath(workRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!fullWorkRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Directory.Delete(workRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void AddFileIssue(
        ICollection<Iw3ImageParallaxExportReadinessIssue> issues,
        string path,
        string englishLabel,
        string spanishLabel)
    {
        if (!File.Exists(path))
        {
            issues.Add(new(
                $"Missing bundled {englishLabel}: {path}",
                $"Falta {spanishLabel} incluido: {path}"));
        }
    }

    private static void AddDirectoryIssue(
        ICollection<Iw3ImageParallaxExportReadinessIssue> issues,
        string path,
        string englishLabel,
        string spanishLabel)
    {
        if (!Directory.Exists(path))
        {
            issues.Add(new(
                $"Missing bundled {englishLabel}: {path}",
                $"Falta {spanishLabel} incluida: {path}"));
        }
    }

    private static string? ResolveModelPath(string modelsDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(modelsDirectory) ||
            string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Contains(".."))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(
            [modelsDirectory, .. relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)]));
        var root = Path.GetFullPath(modelsDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }

    private static void AddMissingOption(
        ICollection<string> missing,
        Iw3CliCapabilitiesManifest capabilities,
        string option)
    {
        if (!HasVerifiedOption(capabilities, option))
        {
            missing.Add(option);
        }
    }

    private static void AddMissingOptionGroup(
        ICollection<string> missing,
        Iw3CliCapabilitiesManifest capabilities,
        string displayName,
        params string[] options)
    {
        if (!options.Any(option => HasVerifiedOption(capabilities, option)))
        {
            missing.Add(displayName);
        }
    }

    private static bool HasVerifiedOption(
        Iw3CliCapabilitiesManifest capabilities,
        string option) =>
        capabilities.VerifiedOptions.Contains(option, StringComparer.OrdinalIgnoreCase);

    private static string CombineProcessSummaries(params string[] texts)
    {
        var lines = texts
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(SummarizeProcessText)
            .Where(text => !string.IsNullOrWhiteSpace(text));
        return string.Join(" | ", lines);
    }

    private static string SummarizeProcessText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Take(8);
        return string.Join(" | ", lines);
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalSeconds >= 1
            ? $"{elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s"
            : $"{elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)} ms";
}

public sealed record Iw3ImageParallaxExportReadiness(
    bool CanExport,
    IReadOnlyList<Iw3ImageParallaxExportReadinessIssue> Issues);

public sealed record Iw3ImageParallaxExportReadinessIssue(
    string EnglishMessage,
    string SpanishMessage);
