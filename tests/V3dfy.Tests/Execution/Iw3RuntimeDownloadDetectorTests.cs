using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Execution;

namespace V3dfy.Tests.Execution;

public sealed class Iw3RuntimeDownloadDetectorTests
{
    [Fact]
    public void IsRuntimeDownloadLine_DetectsDownloadingPrefix()
    {
        Assert.True(Iw3RuntimeDownloadDetector.IsRuntimeDownloadLine(
            "Downloading: \"https://github.com/nagadomi/nunif/releases/download/0.0.0/iw3_row_flow_v3_20250627\""));
    }

    [Fact]
    public void IsRuntimeDownloadLine_DetectsGithubHttpUrls()
    {
        Assert.True(Iw3RuntimeDownloadDetector.IsRuntimeDownloadLine(
            "Fetching https://github.com/nagadomi/nunif/releases/download/0.0.0/runtime.zip"));
        Assert.True(Iw3RuntimeDownloadDetector.IsRuntimeDownloadLine(
            "Fetching http://github.com/nagadomi/nunif/releases/download/0.0.0/runtime.zip"));
    }

    [Fact]
    public void IsRuntimeDownloadLine_IgnoresNormalIw3Progress()
    {
        Assert.False(Iw3RuntimeDownloadDetector.IsRuntimeDownloadLine("frame=1 fps=0.5"));
        Assert.False(Iw3RuntimeDownloadDetector.IsRuntimeDownloadLine("Running iw3 preview conversion..."));
    }

    [Fact]
    public void ContainsRuntimeDownload_ScansCapturedOutputAndOutputLines()
    {
        var now = DateTimeOffset.UtcNow;
        var capturedResult = new ProcessExecutionResult(
            ExitCode: 0,
            StandardOutput: string.Empty,
            StandardError: "Downloading: \"https://github.com/nagadomi/nunif/releases/download/0.0.0/runtime.zip\"",
            OutputLines: [],
            Status: ProcessExecutionStatus.Completed,
            StartedAt: now,
            EndedAt: now);
        var liveResult = capturedResult with
        {
            StandardError = string.Empty,
            OutputLines =
            [
                new(
                    ProcessOutputStream.StandardError,
                    "Downloading: \"https://github.com/nagadomi/nunif/releases/download/0.0.0/runtime.zip\"",
                    now),
            ],
        };

        Assert.True(Iw3RuntimeDownloadDetector.ContainsRuntimeDownload(capturedResult));
        Assert.True(Iw3RuntimeDownloadDetector.ContainsRuntimeDownload(liveResult));
    }
}
