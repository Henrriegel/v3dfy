using V3dfy.Core.Models;
using V3dfy.Infrastructure.Health;
using V3dfy.Infrastructure.Paths;

namespace V3dfy.Tests.Infrastructure;

public sealed class InternalToolsTests
{
    [Fact]
    public void Resolver_UsesApplicationBaseDirectoryInsteadOfGlobalPath()
    {
        var baseDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "app-root"));

        var paths = new InternalToolPathResolver(baseDirectory).Resolve();

        Assert.Equal(
            Path.Combine(baseDirectory, "tools", "ffmpeg", "win-x64", "ffmpeg.exe"),
            paths.FfmpegExecutable);
        Assert.Equal(
            Path.Combine(baseDirectory, "engine", "iw3", "python", "python.exe"),
            paths.PythonExecutable);
    }

    [Fact]
    public void HealthCheck_MarksAllComponentsMissing_WhenInternalToolsDoNotExist()
    {
        var baseDirectory = Path.Combine(
            Environment.CurrentDirectory,
            "missing-tools",
            Guid.NewGuid().ToString("N"));
        var paths = new InternalToolPathResolver(baseDirectory).Resolve();

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Missing, status.Ffmpeg);
        Assert.Equal(ToolHealthStatus.Missing, status.Ffprobe);
        Assert.Equal(ToolHealthStatus.Missing, status.Python);
        Assert.Equal(ToolHealthStatus.Missing, status.Iw3EngineDirectory);
        Assert.Equal(ToolHealthStatus.Missing, status.ModelsDirectory);
        Assert.False(status.IsComplete);
    }
}
