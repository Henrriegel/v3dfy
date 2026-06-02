using V3dfy.Core.Processes;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Tests.Infrastructure;

public sealed class LocalProcessRunnerTests
{
    private readonly LocalProcessRunner _runner = new();

    [Fact]
    public async Task RunAsync_RejectsEmptyExecutablePath()
    {
        var request = CreateRequest(string.Empty, "/c", "echo ignored");

        await Assert.ThrowsAsync<ArgumentException>(() => _runner.RunAsync(request));
    }

    [Fact]
    public async Task RunAsync_RejectsPathLookup()
    {
        var request = CreateRequest("cmd.exe", "/c", "echo ignored");

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _runner.RunAsync(request));

        Assert.Contains("PATH lookup is not allowed", exception.Message);
    }

    [Fact]
    public async Task RunAsync_CapturesStandardOutput()
    {
        var result = await _runner.RunAsync(CreateRequest(
            GetCommandInterpreterPath(),
            "/c",
            "echo hello"));

        Assert.Equal(ProcessExecutionStatus.Completed, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StandardOutput);
        Assert.Contains(
            result.OutputLines,
            line => line.Stream == ProcessOutputStream.StandardOutput &&
                line.Text.Contains("hello", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_CapturesStandardErrorAndNonZeroExitCode()
    {
        var result = await _runner.RunAsync(CreateRequest(
            GetCommandInterpreterPath(),
            "/c",
            "echo failed 1>&2 & exit /b 7"));

        Assert.Equal(ProcessExecutionStatus.Completed, result.Status);
        Assert.Equal(7, result.ExitCode);
        Assert.Contains("failed", result.StandardError);
        Assert.Contains(
            result.OutputLines,
            line => line.Stream == ProcessOutputStream.StandardError &&
                line.Text.Contains("failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_ReturnsTimedOutResult()
    {
        var request = CreateRequest(
            GetCommandInterpreterPath(),
            ["/c", "for /L %i in (1,1,2147483647) do @rem"],
            timeout: TimeSpan.FromMilliseconds(100));

        var result = await _runner.RunAsync(request);

        Assert.Equal(ProcessExecutionStatus.TimedOut, result.Status);
        Assert.True(result.TimedOut);
        Assert.False(result.WasCanceled);
    }

    [Fact]
    public async Task RunAsync_ReturnsCanceledResult()
    {
        using var cancellationTokenSource = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(100));
        var request = CreateRequest(
            GetCommandInterpreterPath(),
            "/c",
            "for /L %i in (1,1,2147483647) do @rem");

        var result = await _runner.RunAsync(request, cancellationTokenSource.Token);

        Assert.Equal(ProcessExecutionStatus.Canceled, result.Status);
        Assert.True(result.WasCanceled);
        Assert.False(result.TimedOut);
    }

    private static ProcessExecutionRequest CreateRequest(
        string executablePath,
        params string[] arguments) =>
        CreateRequest(executablePath, arguments, timeout: null);

    private static ProcessExecutionRequest CreateRequest(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout) => new(
        ExecutablePath: executablePath,
        Arguments: arguments,
        Timeout: timeout);

    private static string GetCommandInterpreterPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "cmd.exe");
}
