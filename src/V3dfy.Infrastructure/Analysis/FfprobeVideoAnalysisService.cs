using System.Text.Json;
using V3dfy.Core.Analysis;
using V3dfy.Core.Models;
using V3dfy.Core.Processes;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Infrastructure.Analysis;

public sealed class FfprobeVideoAnalysisService : IFfprobeVideoAnalysisService
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly InternalToolPaths _paths;
    private readonly ILocalProcessRunner _processRunner;
    private readonly FfprobeJsonParser _parser;

    public FfprobeVideoAnalysisService(
        InternalToolPaths paths,
        ILocalProcessRunner processRunner,
        FfprobeJsonParser parser)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(parser);

        _paths = paths;
        _processRunner = processRunner;
        _parser = parser;
    }

    public async Task<VideoAnalysisServiceResult> AnalyzeAsync(
        VideoAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);

        if (!File.Exists(_paths.FfprobeExecutable))
        {
            return Failed(
                VideoAnalysisFailureKind.MissingFfprobe,
                $"Bundled FFprobe executable was not found: {_paths.FfprobeExecutable}");
        }

        ProcessExecutionResult execution;
        try
        {
            execution = await _processRunner.RunAsync(
                CreateProcessRequest(request),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failed(
                VideoAnalysisFailureKind.Canceled,
                "FFprobe analysis was canceled.");
        }

        if (execution.TimedOut)
        {
            return Failed(
                VideoAnalysisFailureKind.TimedOut,
                "FFprobe analysis timed out.",
                execution.StandardError);
        }

        if (execution.WasCanceled)
        {
            return Failed(
                VideoAnalysisFailureKind.Canceled,
                "FFprobe analysis was canceled.",
                execution.StandardError);
        }

        if (execution.ExitCode != 0)
        {
            return Failed(
                VideoAnalysisFailureKind.ProcessFailed,
                $"FFprobe exited with code {execution.ExitCode}.",
                execution.StandardError);
        }

        if (string.IsNullOrWhiteSpace(execution.StandardOutput))
        {
            return Failed(
                VideoAnalysisFailureKind.EmptyOutput,
                "FFprobe returned no JSON output.",
                execution.StandardError);
        }

        try
        {
            return VideoAnalysisServiceResult.Success(
                _parser.Parse(request.InputPath, execution.StandardOutput));
        }
        catch (JsonException exception)
        {
            return Failed(
                VideoAnalysisFailureKind.InvalidJson,
                $"FFprobe returned invalid JSON: {exception.Message}",
                execution.StandardError);
        }
    }

    private ProcessExecutionRequest CreateProcessRequest(VideoAnalysisRequest request) => new(
        ExecutablePath: _paths.FfprobeExecutable,
        Arguments:
        [
            "-v",
            "error",
            "-print_format",
            "json",
            "-show_format",
            "-show_streams",
            request.InputPath,
        ],
        Timeout: request.Timeout ?? DefaultTimeout,
        CaptureStandardError: true);

    private static VideoAnalysisServiceResult Failed(
        VideoAnalysisFailureKind kind,
        string message,
        string? standardError = null) =>
        VideoAnalysisServiceResult.Failed(new VideoAnalysisFailure(
            Kind: kind,
            Message: message,
            StandardError: standardError));
}
