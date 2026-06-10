using V3dfy.Core.Processes;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Tests.Infrastructure;

public sealed class BundledLocalProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_RejectsExecutableNameWithoutPath()
    {
        var runner = new BundledLocalProcessRunner(new StubProcessRunner());
        var request = CreateRequest("ffmpeg.exe");

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(request));

        Assert.Contains("PATH lookup is not allowed", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsExecutableOutsideAllowedRoot()
    {
        var runner = new BundledLocalProcessRunner(new StubProcessRunner());
        var allowedRootDirectory = TestPaths.RuntimeRoot("tools");
        var request = CreateRequest(
            executablePath: TestPaths.OtherRoot("ffmpeg.exe"),
            allowedRootDirectory: allowedRootDirectory);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(request));

        Assert.Contains("allowed bundled tool root", exception.Message);
    }

    [Fact]
    public async Task RunAsync_AppliesConstructorAllowedRootWhenRequestDoesNotSetOne()
    {
        var innerRunner = new StubProcessRunner();
        var allowedRootDirectory = TestPaths.RuntimeRoot("tools");
        var runner = new BundledLocalProcessRunner(
            innerRunner,
            allowedRootDirectory: allowedRootDirectory);
        var request = CreateRequest(Path.Combine(
            allowedRootDirectory,
            "ffmpeg",
            "win-x64",
            "ffmpeg.exe"));

        await runner.RunAsync(request);

        Assert.NotNull(innerRunner.Request);
        Assert.Equal(allowedRootDirectory, innerRunner.Request.AllowedRootDirectory);
    }

    [Fact]
    public async Task RunAsync_PreservesLiveOutputAndMetricsCallbacksWhenApplyingAllowedRoot()
    {
        var innerRunner = new StubProcessRunner();
        var outputProgress = new CapturingProgress<ProcessOutputLine>();
        var metricsProgress = new CapturingProgress<ProcessMetricSample>();
        var allowedRootDirectory = TestPaths.RuntimeRoot("tools");
        var runner = new BundledLocalProcessRunner(
            innerRunner,
            allowedRootDirectory: allowedRootDirectory);
        var request = CreateRequest(
            executablePath: Path.Combine(
                allowedRootDirectory,
                "ffmpeg",
                "win-x64",
                "ffmpeg.exe"),
            outputProgress: outputProgress,
            metricsProgress: metricsProgress);

        await runner.RunAsync(request);

        Assert.NotNull(innerRunner.Request);
        Assert.Same(outputProgress, innerRunner.Request.OutputProgress);
        Assert.Same(metricsProgress, innerRunner.Request.MetricsProgress);
    }

    [Fact]
    public async Task RunAsync_AllowsAbsoluteExecutableInsideAllowedRoot()
    {
        var innerRunner = new StubProcessRunner();
        var runner = new BundledLocalProcessRunner(innerRunner);
        var allowedRootDirectory = TestPaths.RuntimeRoot("tools");
        var request = CreateRequest(
            executablePath: Path.Combine(
                allowedRootDirectory,
                "ffmpeg",
                "win-x64",
                "ffmpeg.exe"),
            allowedRootDirectory: allowedRootDirectory);

        var result = await runner.RunAsync(request);

        Assert.Equal(ProcessExecutionStatus.Completed, result.Status);
        Assert.NotNull(innerRunner.Request);
        Assert.Equal(request.ExecutablePath, innerRunner.Request.ExecutablePath);
    }

    [Fact]
    public void ValidateBundledToolRequest_RejectsNonPositiveTimeout()
    {
        var request = CreateRequest(
            executablePath: Path.Combine(
                TestPaths.RuntimeRoot("tools"),
                "ffmpeg",
                "win-x64",
                "ffmpeg.exe"),
            timeout: TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProcessExecutionRequestValidator.ValidateBundledToolRequest(request));
    }

    [Fact]
    public void ValidateBundledToolRequest_RejectsNonPositiveMetricsInterval()
    {
        var request = CreateRequest(
            executablePath: Path.Combine(
                TestPaths.RuntimeRoot("tools"),
                "ffmpeg",
                "win-x64",
                "ffmpeg.exe"),
            metricsInterval: TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProcessExecutionRequestValidator.ValidateBundledToolRequest(request));
    }

    [Fact]
    public void ProcessExecutionResult_ProvidesLocalizedStatusSummaries()
    {
        var result = new ProcessExecutionResult(
            ExitCode: 7,
            StandardOutput: string.Empty,
            StandardError: string.Empty,
            OutputLines: [],
            Status: ProcessExecutionStatus.Completed,
            StartedAt: DateTimeOffset.UtcNow,
            EndedAt: DateTimeOffset.UtcNow);

        Assert.Equal("Process exited with code 7.", result.EnglishSummary);
        Assert.Equal("El proceso termin\u00f3 con c\u00f3digo 7.", result.SpanishSummary);
    }

    private static ProcessExecutionRequest CreateRequest(
        string executablePath,
        string? allowedRootDirectory = null,
        TimeSpan? timeout = null,
        IProgress<ProcessOutputLine>? outputProgress = null,
        IProgress<ProcessMetricSample>? metricsProgress = null,
        TimeSpan? metricsInterval = null) => new(
        ExecutablePath: executablePath,
        Arguments: ["-version"],
        Timeout: timeout,
        AllowedRootDirectory: allowedRootDirectory,
        OutputProgress: outputProgress,
        MetricsProgress: metricsProgress,
        MetricsInterval: metricsInterval);

    private sealed class CapturingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value) => Values.Add(value);
    }

    private sealed class StubProcessRunner : ILocalProcessRunner
    {
        public ProcessExecutionRequest? Request { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;

            return Task.FromResult(new ProcessExecutionResult(
                ExitCode: 0,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                OutputLines: [],
                Status: ProcessExecutionStatus.Completed,
                StartedAt: DateTimeOffset.UtcNow,
                EndedAt: DateTimeOffset.UtcNow));
        }
    }
}
