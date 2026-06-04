namespace V3dfy.Core.Models;

public sealed record EngineDependencyHealth(
    ToolDependencyHealth Ffmpeg,
    ToolDependencyHealth Ffprobe,
    ToolDependencyHealth Python,
    ToolDependencyHealth Iw3EngineDirectory,
    ToolDependencyHealth ModelsDirectory,
    LocalModelInventory ModelInventory)
{
    public EngineDependencyHealth(
        ToolDependencyHealth Ffmpeg,
        ToolDependencyHealth Ffprobe,
        ToolDependencyHealth Python,
        ToolDependencyHealth Iw3EngineDirectory,
        ToolDependencyHealth ModelsDirectory)
        : this(
            Ffmpeg,
            Ffprobe,
            Python,
            Iw3EngineDirectory,
            ModelsDirectory,
            LocalModelInventory.Empty(ModelsDirectory.ExpectedPath))
    {
    }

    public EngineHealthStatus Summary => new(
        Ffmpeg.Status,
        Ffprobe.Status,
        Python.Status,
        Iw3EngineDirectory.Status,
        ModelsDirectory.Status);

    public bool IsComplete => Summary.IsComplete;
}
