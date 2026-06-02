using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.Health;

public sealed class InternalToolsHealthChecker
{
    public EngineHealthStatus Check(InternalToolPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return new EngineHealthStatus(
            Ffmpeg: GetFileStatus(paths.FfmpegExecutable),
            Ffprobe: GetFileStatus(paths.FfprobeExecutable),
            Python: GetFileStatus(paths.PythonExecutable),
            Iw3EngineDirectory: GetDirectoryStatus(paths.Iw3EngineDirectory),
            ModelsDirectory: GetDirectoryStatus(paths.ModelsDirectory));
    }

    private static ToolHealthStatus GetFileStatus(string path) =>
        File.Exists(path) ? ToolHealthStatus.Found : ToolHealthStatus.Missing;

    private static ToolHealthStatus GetDirectoryStatus(string path) =>
        Directory.Exists(path) ? ToolHealthStatus.Found : ToolHealthStatus.Missing;
}
