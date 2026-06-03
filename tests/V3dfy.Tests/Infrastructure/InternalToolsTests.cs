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
    public void DetailedHealthCheck_ReturnsExpectedPathsAndMissingFileReasons()
    {
        var paths = CreateToolLayout();

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(paths.FfmpegExecutable, health.Ffmpeg.ExpectedPath);
        Assert.Equal(ToolHealthStatus.Missing, health.Ffmpeg.Status);
        Assert.Equal(ToolHealthDetailKind.BundledFileMissing, health.Ffmpeg.DetailKind);
        Assert.Equal(paths.FfprobeExecutable, health.Ffprobe.ExpectedPath);
        Assert.Equal(ToolHealthDetailKind.BundledFileMissing, health.Ffprobe.DetailKind);
        Assert.Equal(paths.PythonExecutable, health.Python.ExpectedPath);
        Assert.Equal(ToolHealthDetailKind.BundledFileMissing, health.Python.DetailKind);
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
    public void DetailedHealthCheck_ReportsPlaceholderOnlyEngineAndEmptyModels()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "README.md"), "placeholder");
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "README.md"), "placeholder");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(paths.Iw3EngineDirectory, health.Iw3EngineDirectory.ExpectedPath);
        Assert.Equal(ToolHealthStatus.Missing, health.Iw3EngineDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.EnginePlaceholderOnly, health.Iw3EngineDirectory.DetailKind);
        Assert.Equal(paths.ModelsDirectory, health.ModelsDirectory.ExpectedPath);
        Assert.Equal(ToolHealthStatus.Missing, health.ModelsDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.ModelFilesMissing, health.ModelsDirectory.DetailKind);
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

    [Fact]
    public void DetailedHealthCheck_MarksEngineFound_WhenNonPlaceholderManifestExists()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        File.WriteAllText(
            Path.Combine(paths.Iw3EngineDirectory, "ENGINE_MANIFEST.json"),
            """{"version":"1.0.0"}""");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Found, health.Iw3EngineDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.EngineBundleFound, health.Iw3EngineDirectory.DetailKind);
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
    public void DetailedHealthCheck_MarksModelsFound_WhenModelFileExists()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "depth-model.safetensors"), "model");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Found, health.ModelsDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.ModelFilesFound, health.ModelsDirectory.DetailKind);
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
