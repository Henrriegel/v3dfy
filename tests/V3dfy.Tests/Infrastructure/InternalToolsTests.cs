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

    [Fact]
    public void HealthCheck_MarksPlaceholderOnlyEngineAndModelsMissing()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "README.md"), "placeholder");
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "README.md"), "placeholder");

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Missing, status.Iw3EngineDirectory);
        Assert.Equal(ToolHealthStatus.Missing, status.ModelsDirectory);
    }

    [Fact]
    public void HealthCheck_MarksEngineFound_WhenPythonModuleExists()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "iw3.py"), "# entrypoint");

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Found, status.Iw3EngineDirectory);
    }

    [Theory]
    [InlineData("depth-model.pth")]
    [InlineData("depth-model.onnx")]
    public void HealthCheck_MarksModelsFound_WhenModelFileExists(string fileName)
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, fileName), "model");

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Found, status.ModelsDirectory);
    }

    [Fact]
    public void HealthCheck_MarksExactExecutableFilesFound()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(Path.GetDirectoryName(paths.FfmpegExecutable)!);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.PythonExecutable)!);
        File.WriteAllText(paths.FfmpegExecutable, "ffmpeg");
        File.WriteAllText(paths.FfprobeExecutable, "ffprobe");
        File.WriteAllText(paths.PythonExecutable, "python");

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Found, status.Ffmpeg);
        Assert.Equal(ToolHealthStatus.Found, status.Ffprobe);
        Assert.Equal(ToolHealthStatus.Found, status.Python);
    }

    private static InternalToolPaths CreateToolLayout()
    {
        var baseDirectory = Path.Combine(
            Environment.CurrentDirectory,
            "tool-layouts",
            Guid.NewGuid().ToString("N"));

        return new InternalToolPathResolver(baseDirectory).Resolve();
    }
}
