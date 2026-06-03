namespace V3dfy.Core.Models;

public sealed record EngineDependencyHealth(
    ToolDependencyHealth Ffmpeg,
    ToolDependencyHealth Ffprobe,
    ToolDependencyHealth Python,
    ToolDependencyHealth Iw3EngineDirectory,
    ToolDependencyHealth ModelsDirectory)
{
    public EngineHealthStatus Summary => new(
        Ffmpeg.Status,
        Ffprobe.Status,
        Python.Status,
        Iw3EngineDirectory.Status,
        ModelsDirectory.Status);

    public bool IsComplete => Summary.IsComplete;
}
