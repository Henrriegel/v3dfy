namespace V3dfy.Core.Models;

public sealed record EngineHealthStatus(
    ToolHealthStatus Ffmpeg,
    ToolHealthStatus Ffprobe,
    ToolHealthStatus Python,
    ToolHealthStatus Iw3EngineDirectory,
    ToolHealthStatus ModelsDirectory)
{
    public ToolHealthStatus Iw3RuntimeDependencies { get; init; } = ToolHealthStatus.Found;

    public bool IsComplete =>
        Ffmpeg == ToolHealthStatus.Found &&
        Ffprobe == ToolHealthStatus.Found &&
        Python == ToolHealthStatus.Found &&
        Iw3EngineDirectory == ToolHealthStatus.Found &&
        ModelsDirectory == ToolHealthStatus.Found &&
        Iw3RuntimeDependencies == ToolHealthStatus.Found;
}
