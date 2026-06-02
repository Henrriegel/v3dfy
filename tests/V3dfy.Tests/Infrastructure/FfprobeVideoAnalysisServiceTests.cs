using V3dfy.Core.Analysis;
using V3dfy.Core.Models;
using V3dfy.Core.Processes;
using V3dfy.Infrastructure.Analysis;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Tests.Infrastructure;

public sealed class FfprobeVideoAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_UsesBundledExecutableAndRequiredArguments()
    {
        var runner = new StubProcessRunner(Completed(StandardOutput: ValidJson));
        var service = CreateService(runner);

        var result = await service.AnalyzeAsync(new VideoAnalysisRequest(InputPath));

        Assert.True(result.IsSuccess);
        Assert.Equal(GetExistingSentinelPath(), runner.Request?.ExecutablePath);
        Assert.NotEqual("ffprobe", runner.Request?.ExecutablePath);
        AssertArgumentsContainPair(runner.Request, "-print_format", "json");
        Assert.Contains("-show_format", runner.Request!.Arguments);
        Assert.Contains("-show_streams", runner.Request.Arguments);
        Assert.Contains(InputPath, runner.Request.Arguments);
        Assert.True(runner.Request.CaptureStandardError);
    }

    [Fact]
    public async Task AnalyzeAsync_MissingFfprobe_ReturnsMissingToolFailure()
    {
        var runner = new StubProcessRunner(Completed(StandardOutput: ValidJson));
        var service = CreateService(
            runner,
            ffprobePath: Path.Combine(AppContext.BaseDirectory, "missing", "ffprobe.exe"));

        var result = await service.AnalyzeAsync(new VideoAnalysisRequest(InputPath));

        AssertFailure(result, VideoAnalysisFailureKind.MissingFfprobe);
        Assert.Null(runner.Request);
    }

    [Fact]
    public async Task AnalyzeAsync_SuccessfulOutput_ReturnsParsedAnalysis()
    {
        var runner = new StubProcessRunner(Completed(StandardOutput: ValidJson));
        var service = CreateService(runner);

        var result = await service.AnalyzeAsync(new VideoAnalysisRequest(InputPath));

        Assert.True(result.IsSuccess);
        Assert.Equal(InputPath, result.Analysis?.InputPath);
        Assert.Equal(1920, result.Analysis?.Video?.Width);
        Assert.Equal(1080, result.Analysis?.Video?.Height);
    }

    [Fact]
    public async Task AnalyzeAsync_NonZeroExit_ReturnsProcessFailureWithStandardError()
    {
        var runner = new StubProcessRunner(Completed(
            ExitCode: 2,
            StandardError: "invalid input"));
        var service = CreateService(runner);

        var result = await service.AnalyzeAsync(new VideoAnalysisRequest(InputPath));

        var failure = AssertFailure(result, VideoAnalysisFailureKind.ProcessFailed);
        Assert.Equal("invalid input", failure.StandardError);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyOutput_ReturnsEmptyOutputFailure()
    {
        var runner = new StubProcessRunner(Completed(StandardOutput: " "));
        var service = CreateService(runner);

        var result = await service.AnalyzeAsync(new VideoAnalysisRequest(InputPath));

        AssertFailure(result, VideoAnalysisFailureKind.EmptyOutput);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidJson_ReturnsInvalidJsonFailure()
    {
        var runner = new StubProcessRunner(Completed(StandardOutput: "{ invalid json"));
        var service = CreateService(runner);

        var result = await service.AnalyzeAsync(new VideoAnalysisRequest(InputPath));

        AssertFailure(result, VideoAnalysisFailureKind.InvalidJson);
    }

    [Fact]
    public async Task AnalyzeAsync_TimedOutExecution_ReturnsTimeoutFailure()
    {
        var runner = new StubProcessRunner(Completed(
            Status: ProcessExecutionStatus.TimedOut));
        var service = CreateService(runner);

        var result = await service.AnalyzeAsync(new VideoAnalysisRequest(InputPath));

        AssertFailure(result, VideoAnalysisFailureKind.TimedOut);
    }

    [Fact]
    public async Task AnalyzeAsync_CanceledExecution_ReturnsCanceledFailure()
    {
        var runner = new StubProcessRunner(Completed(
            Status: ProcessExecutionStatus.Canceled));
        var service = CreateService(runner);

        var result = await service.AnalyzeAsync(new VideoAnalysisRequest(InputPath));

        AssertFailure(result, VideoAnalysisFailureKind.Canceled);
    }

    [Fact]
    public async Task AnalyzeAsync_PassesCancellationTokenToRunner()
    {
        var runner = new StubProcessRunner(Completed(StandardOutput: ValidJson));
        var service = CreateService(runner);
        using var cancellationTokenSource = new CancellationTokenSource();

        await service.AnalyzeAsync(
            new VideoAnalysisRequest(InputPath),
            cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, runner.CancellationToken);
    }

    private const string InputPath = @"C:\videos\input.mp4";

    private const string ValidJson =
        """
        {
          "streams": [
            {
              "index": 0,
              "codec_type": "video",
              "codec_name": "h264",
              "width": 1920,
              "height": 1080
            }
          ],
          "format": {
            "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
            "duration": "60.0"
          }
        }
        """;

    private static FfprobeVideoAnalysisService CreateService(
        StubProcessRunner runner,
        string? ffprobePath = null) => new(
        paths: CreatePaths(ffprobePath ?? GetExistingSentinelPath()),
        processRunner: runner,
        parser: new FfprobeJsonParser());

    private static InternalToolPaths CreatePaths(string ffprobePath) => new(
        FfmpegExecutable: @"C:\bundle\tools\ffmpeg\win-x64\ffmpeg.exe",
        FfprobeExecutable: ffprobePath,
        PythonExecutable: @"C:\bundle\engine\iw3\python\python.exe",
        Iw3EngineDirectory: @"C:\bundle\engine\iw3",
        ModelsDirectory: @"C:\bundle\engine\iw3\models");

    private static string GetExistingSentinelPath() =>
        typeof(FfprobeVideoAnalysisServiceTests).Assembly.Location;

    private static ProcessExecutionResult Completed(
        int ExitCode = 0,
        string StandardOutput = "",
        string StandardError = "",
        ProcessExecutionStatus Status = ProcessExecutionStatus.Completed)
    {
        var startedAt = DateTimeOffset.UtcNow;

        return new ProcessExecutionResult(
            ExitCode,
            StandardOutput,
            StandardError,
            OutputLines: [],
            Status,
            StartedAt: startedAt,
            EndedAt: startedAt);
    }

    private static VideoAnalysisFailure AssertFailure(
        VideoAnalysisServiceResult result,
        VideoAnalysisFailureKind expectedKind)
    {
        Assert.False(result.IsSuccess);
        var failure = Assert.IsType<VideoAnalysisFailure>(result.Failure);
        Assert.Equal(expectedKind, failure.Kind);
        return failure;
    }

    private static void AssertArgumentsContainPair(
        ProcessExecutionRequest? request,
        string argument,
        string value)
    {
        var arguments = Assert.IsAssignableFrom<IReadOnlyList<string>>(request?.Arguments);
        var argumentIndex = Assert.Single(
            arguments.Select((item, index) => (item, index)),
            item => item.item == argument).index;

        Assert.Equal(value, arguments[argumentIndex + 1]);
    }

    private sealed class StubProcessRunner(ProcessExecutionResult result) : ILocalProcessRunner
    {
        public ProcessExecutionRequest? Request { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            CancellationToken = cancellationToken;
            return Task.FromResult(result);
        }
    }
}
