namespace V3dfy.Core.Models;

public sealed record EngineDependencyHealth(
    ToolDependencyHealth Ffmpeg,
    ToolDependencyHealth Ffprobe,
    ToolDependencyHealth Python,
    ToolDependencyHealth Iw3EngineDirectory,
    ToolDependencyHealth ModelsDirectory,
    LocalModelInventory ModelInventory,
    Iw3CliCapabilitiesManifest Iw3CliCapabilities)
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
            LocalModelInventory.Empty(ModelsDirectory.ExpectedPath),
            Iw3CliCapabilitiesManifest.Missing(Path.Combine(
                Iw3EngineDirectory.ExpectedPath,
                Iw3EngineBundleContract.CliCapabilitiesFileName)))
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
