using System.Globalization;
using System.Text.RegularExpressions;
using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;

namespace V3dfy.Engine.Iw3.Execution;

public sealed record Iw3ProcessDiagnosticContext(
    string EnglishOperationName,
    string SpanishOperationName,
    string InputPath,
    string ProcessOutputPath,
    string? FinalOutputPath,
    OutputContainer OutputContainer,
    AiQualityPreset QualityPreset,
    ThreeDIntensity Intensity,
    ThreeDOutputFormat ThreeDOutputFormat,
    LocalModelPlanSelection? SelectedLocalModel);

public sealed record Iw3ProcessTimingSnapshot(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    TimeSpan TotalProcessingTime,
    DateTimeOffset? FirstOutputTime,
    TimeSpan? TimeToFirstOutput,
    DateTimeOffset? FirstFrameProgressTime,
    TimeSpan? TimeToFirstFrameProgress,
    int? FrameCount,
    double? FramesPerSecond,
    double? SecondsPerFrame);

public static class Iw3ProcessDiagnostics
{
    private static readonly Regex FractionProgressRegex = new(
        @"(?<!\d)(?<current>\d{1,8})\s*/\s*(?<total>\d{1,8})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FrameEqualsRegex = new(
        @"\bframe\s*=\s*(?<frame>\d{1,8})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyList<ConversionExecutionLogEntry> CreateCommandLogs(
        ProcessExecutionRequest processRequest,
        Iw3ProcessDiagnosticContext context)
    {
        ArgumentNullException.ThrowIfNull(processRequest);
        ArgumentNullException.ThrowIfNull(context);

        var timestamp = DateTimeOffset.UtcNow;
        var depthModel = GetDepthModelName(context.SelectedLocalModel);
        var layoutFlag = GetLayoutFlag(context.ThreeDOutputFormat);
        var encoderFlags = FormatConfiguredFlags(
            processRequest.Arguments,
            ["--preset", "--crf", "--video-codec", "-c", "-c:v"]);
        var methodFlags = FormatConfiguredFlags(
            processRequest.Arguments,
            ["--method", "--stereo-method", "--row-flow", "--row-flow-method"]);

        return
        [
            CreateLog(
                timestamp,
                context,
                $"sanitized command line: {FormatCommandLine(processRequest)}",
                $"linea de comando saneada: {FormatCommandLine(processRequest)}"),
            CreateLog(
                timestamp,
                context,
                $"Python executable path: {processRequest.ExecutablePath}",
                $"ruta del ejecutable Python: {processRequest.ExecutablePath}"),
            CreateLog(
                timestamp,
                context,
                $"working directory: {ValueOrNone(processRequest.WorkingDirectory)}",
                $"directorio de trabajo: {ValueOrNone(processRequest.WorkingDirectory)}"),
            CreateLog(
                timestamp,
                context,
                $"environment variables set by v3dfy: {FormatEnvironment(processRequest.EnvironmentVariables)}",
                $"variables de entorno configuradas por v3dfy: {FormatEnvironment(processRequest.EnvironmentVariables)}"),
            CreateLog(
                timestamp,
                context,
                $"input file path: {context.InputPath}",
                $"ruta de entrada: {context.InputPath}"),
            CreateLog(
                timestamp,
                context,
                $"process output file path: {context.ProcessOutputPath}",
                $"ruta de salida del proceso: {context.ProcessOutputPath}"),
            CreateLog(
                timestamp,
                context,
                $"final output file path: {ValueOrNone(context.FinalOutputPath)}",
                $"ruta de salida final: {ValueOrNone(context.FinalOutputPath)}"),
            CreateLog(
                timestamp,
                context,
                $"selected model: {FormatSelectedModel(context.SelectedLocalModel)}",
                $"modelo seleccionado: {FormatSelectedModel(context.SelectedLocalModel)}"),
            CreateLog(
                timestamp,
                context,
                $"iw3 depth model value: {depthModel}",
                $"valor de modelo de profundidad iw3: {depthModel}"),
            CreateLog(
                timestamp,
                context,
                $"layout flag: {layoutFlag}",
                $"flag de layout: {layoutFlag}"),
            CreateLog(
                timestamp,
                context,
                $"selected intensity: {context.Intensity}; selected quality: {context.QualityPreset}; output container: {context.OutputContainer}",
                $"intensidad seleccionada: {context.Intensity}; calidad seleccionada: {context.QualityPreset}; contenedor de salida: {context.OutputContainer}"),
            CreateLog(
                timestamp,
                context,
                $"encoder flags set by v3dfy: {encoderFlags}",
                $"flags de encoder configurados por v3dfy: {encoderFlags}"),
            CreateLog(
                timestamp,
                context,
                $"method/row-flow flags set by v3dfy: {methodFlags}",
                $"flags de metodo/row-flow configurados por v3dfy: {methodFlags}"),
            CreateLog(
                timestamp,
                context,
                $"runtime/cache resolution: AllowedRootDirectory={ValueOrNone(processRequest.AllowedRootDirectory)}; NUNIF_HOME={GetEnvironmentValue(processRequest.EnvironmentVariables, "NUNIF_HOME")}; TORCH_HOME={GetEnvironmentValue(processRequest.EnvironmentVariables, "TORCH_HOME")}; PYTHONNOUSERSITE={GetEnvironmentValue(processRequest.EnvironmentVariables, "PYTHONNOUSERSITE")}",
                $"resolucion de runtime/cache: AllowedRootDirectory={ValueOrNone(processRequest.AllowedRootDirectory)}; NUNIF_HOME={GetEnvironmentValue(processRequest.EnvironmentVariables, "NUNIF_HOME")}; TORCH_HOME={GetEnvironmentValue(processRequest.EnvironmentVariables, "TORCH_HOME")}; PYTHONNOUSERSITE={GetEnvironmentValue(processRequest.EnvironmentVariables, "PYTHONNOUSERSITE")}"),
        ];
    }

    public static IReadOnlyList<ConversionExecutionLogEntry> CreateTimingLogs(
        string englishOperationName,
        string spanishOperationName,
        ProcessExecutionResult processResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(englishOperationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(spanishOperationName);
        ArgumentNullException.ThrowIfNull(processResult);

        var snapshot = CreateTimingSnapshot(processResult);
        var english = CreateTimingMessage(
            englishOperationName,
            snapshot,
            useSpanish: false);
        var spanish = CreateTimingMessage(
            spanishOperationName,
            snapshot,
            useSpanish: true);

        return [new(processResult.EndedAt, english, spanish)];
    }

    public static Iw3ProcessTimingSnapshot CreateTimingSnapshot(
        ProcessExecutionResult processResult)
    {
        ArgumentNullException.ThrowIfNull(processResult);

        var outputLines = EnumerateProcessOutputLines(processResult).ToArray();
        var firstOutput = outputLines
            .OrderBy(line => line.CapturedAt)
            .FirstOrDefault();
        var firstFrameProgress = outputLines
            .OrderBy(line => line.CapturedAt)
            .FirstOrDefault(line => IsFrameOrProgressLine(line.Text));
        var frameCount = ParseFrameCount(outputLines.Select(line => line.Text));
        var frameProcessingStartedAt =
            firstFrameProgress?.CapturedAt ??
            firstOutput?.CapturedAt ??
            processResult.StartedAt;
        var frameProcessingDuration = processResult.EndedAt - frameProcessingStartedAt;
        if (frameProcessingDuration < TimeSpan.Zero)
        {
            frameProcessingDuration = TimeSpan.Zero;
        }

        double? framesPerSecond = frameCount is > 0 &&
            frameProcessingDuration.TotalSeconds > 0
                ? frameCount.Value / frameProcessingDuration.TotalSeconds
                : null;
        double? secondsPerFrame = frameCount is > 0 &&
            frameProcessingDuration.TotalSeconds > 0
                ? frameProcessingDuration.TotalSeconds / frameCount.Value
                : null;

        return new(
            StartTime: processResult.StartedAt,
            EndTime: processResult.EndedAt,
            TotalProcessingTime: ClampNonNegative(processResult.EndedAt - processResult.StartedAt),
            FirstOutputTime: firstOutput?.CapturedAt,
            TimeToFirstOutput: firstOutput is null
                ? null
                : ClampNonNegative(firstOutput.CapturedAt - processResult.StartedAt),
            FirstFrameProgressTime: firstFrameProgress?.CapturedAt,
            TimeToFirstFrameProgress: firstFrameProgress is null
                ? null
                : ClampNonNegative(firstFrameProgress.CapturedAt - processResult.StartedAt),
            FrameCount: frameCount,
            FramesPerSecond: framesPerSecond,
            SecondsPerFrame: secondsPerFrame);
    }

    public static bool IsFrameOrProgressLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("frame=", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("frame ", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("fps", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("it/s", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
            FractionProgressRegex.IsMatch(text);
    }

    public static string FormatCommandLine(ProcessExecutionRequest processRequest)
    {
        ArgumentNullException.ThrowIfNull(processRequest);

        return string.Join(
            " ",
            [Quote(processRequest.ExecutablePath), .. processRequest.Arguments.Select(Quote)]);
    }

    private static string CreateTimingMessage(
        string operationName,
        Iw3ProcessTimingSnapshot snapshot,
        bool useSpanish)
    {
        var firstOutput = snapshot.FirstOutputTime is null
            ? "n/a"
            : $"{FormatUtc(snapshot.FirstOutputTime.Value)} (+{FormatDuration(snapshot.TimeToFirstOutput)})";
        var firstFrameProgress = snapshot.FirstFrameProgressTime is null
            ? "n/a"
            : $"{FormatUtc(snapshot.FirstFrameProgressTime.Value)} (+{FormatDuration(snapshot.TimeToFirstFrameProgress)})";
        var frames = snapshot.FrameCount?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
        var throughput = snapshot.FramesPerSecond is null || snapshot.SecondsPerFrame is null
            ? "n/a"
            : $"{snapshot.FramesPerSecond.Value.ToString("0.###", CultureInfo.InvariantCulture)} fps / {snapshot.SecondsPerFrame.Value.ToString("0.###", CultureInfo.InvariantCulture)} seconds per frame";

        return useSpanish
            ? $"{operationName} diagnostico de tiempos: inicio {FormatUtc(snapshot.StartTime)}; primera salida {firstOutput}; primera linea de progreso/cuadro {firstFrameProgress}; tiempo total de proceso {FormatDuration(snapshot.TotalProcessingTime)}; cuadros parseados {frames}; rendimiento promedio {throughput}."
            : $"{operationName} timing diagnostics: start {FormatUtc(snapshot.StartTime)}; first process output {firstOutput}; first progress/frame line {firstFrameProgress}; total processing time {FormatDuration(snapshot.TotalProcessingTime)}; parsed frame count {frames}; average throughput {throughput}.";
    }

    private static int? ParseFrameCount(IEnumerable<string> lines)
    {
        int? frameCount = null;
        foreach (var line in lines)
        {
            foreach (Match match in FrameEqualsRegex.Matches(line))
            {
                if (int.TryParse(
                        match.Groups["frame"].Value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var frame))
                {
                    frameCount = Math.Max(frameCount ?? 0, frame);
                }
            }

            foreach (Match match in FractionProgressRegex.Matches(line))
            {
                if (int.TryParse(
                        match.Groups["total"].Value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var total))
                {
                    frameCount = Math.Max(frameCount ?? 0, total);
                }
            }
        }

        return frameCount;
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
        text.Split(
                ["\r\n", "\n", "\r"],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(line => new ProcessOutputLine(stream, line.TrimEnd(), capturedAt));

    private static ConversionExecutionLogEntry CreateLog(
        DateTimeOffset timestamp,
        Iw3ProcessDiagnosticContext context,
        string englishDetail,
        string spanishDetail) => new(
        timestamp,
        $"{context.EnglishOperationName} diagnostics: {englishDetail}",
        $"{context.SpanishOperationName} diagnostico: {spanishDetail}");

    private static string FormatEnvironment(
        IReadOnlyDictionary<string, string?>? environmentVariables)
    {
        if (environmentVariables is null || environmentVariables.Count == 0)
        {
            return "none";
        }

        var values = environmentVariables
            .Where(item => IsRelevantEnvironmentVariable(item.Key))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Key}={ValueOrNone(item.Value)}")
            .ToArray();

        return values.Length == 0
            ? "none"
            : string.Join("; ", values);
    }

    private static bool IsRelevantEnvironmentVariable(string key) =>
        string.Equals(key, "NUNIF_HOME", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "PYTHONNOUSERSITE", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("TORCH", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("PYTORCH", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("CUDA", StringComparison.OrdinalIgnoreCase);

    private static string GetEnvironmentValue(
        IReadOnlyDictionary<string, string?>? environmentVariables,
        string key)
    {
        if (environmentVariables is null)
        {
            return "none";
        }

        return environmentVariables.TryGetValue(key, out var value)
            ? ValueOrNone(value)
            : "none";
    }

    private static string FormatSelectedModel(LocalModelPlanSelection? selectedModel)
    {
        if (selectedModel is null)
        {
            return "none";
        }

        var key = string.IsNullOrWhiteSpace(selectedModel.MappingKey)
            ? selectedModel.Id
            : selectedModel.MappingKey;

        return $"{selectedModel.DisplayName} ({selectedModel.RelativePath}; key={ValueOrNone(key)})";
    }

    private static string GetDepthModelName(LocalModelPlanSelection? selectedModel)
    {
        if (Iw3DepthModelMapper.TryMap(selectedModel, out var mapping) && mapping is not null)
        {
            return mapping.DepthModelName;
        }

        return string.IsNullOrWhiteSpace(selectedModel?.Iw3DepthModelName)
            ? "none"
            : selectedModel.Iw3DepthModelName;
    }

    private static string GetLayoutFlag(ThreeDOutputFormat outputFormat) =>
        outputFormat switch
        {
            ThreeDOutputFormat.HalfSideBySide => Iw3CliContract.HalfSideBySideSwitch,
            ThreeDOutputFormat.HalfTopBottom => Iw3CliContract.HalfTopBottomSwitch,
            ThreeDOutputFormat.Anaglyph => Iw3CliContract.AnaglyphSwitch,
            ThreeDOutputFormat.FullSideBySide => "not exposed",
            _ => outputFormat.ToString(),
        };

    private static string FormatConfiguredFlags(
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> flagNames)
    {
        var values = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!flagNames.Contains(argument, StringComparer.Ordinal))
            {
                continue;
            }

            var value = index + 1 < arguments.Count &&
                !arguments[index + 1].StartsWith("-", StringComparison.Ordinal)
                    ? $" {arguments[index + 1]}"
                    : string.Empty;
            values.Add($"{argument}{value}");
        }

        return values.Count == 0
            ? "none"
            : string.Join("; ", values);
    }

    private static TimeSpan ClampNonNegative(TimeSpan duration) =>
        duration < TimeSpan.Zero
            ? TimeSpan.Zero
            : duration;

    private static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
        {
            return "n/a";
        }

        return duration.Value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";
    }

    private static string ValueOrNone(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "none"
            : value;

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains("\"", StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
