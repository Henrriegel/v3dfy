using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.Paths;

public sealed class InternalToolPathResolver
{
    private readonly string _applicationBaseDirectory;

    public InternalToolPathResolver(string applicationBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        _applicationBaseDirectory = Path.GetFullPath(applicationBaseDirectory);
    }

    public InternalToolPaths Resolve() => new(
        FfmpegExecutable: ResolvePath("tools", "ffmpeg", "win-x64", "ffmpeg.exe"),
        FfprobeExecutable: ResolvePath("tools", "ffmpeg", "win-x64", "ffprobe.exe"),
        PythonExecutable: ResolvePath("engine", "iw3", "python", "python.exe"),
        Iw3EngineDirectory: ResolvePath("engine", "iw3"),
        ModelsDirectory: ResolvePath("engine", "iw3", "models"));

    private string ResolvePath(params string[] segments) =>
        Path.GetFullPath(Path.Combine([_applicationBaseDirectory, .. segments]));
}
