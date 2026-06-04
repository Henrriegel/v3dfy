namespace V3dfy.Core.Models;

public sealed record InternalToolPaths(
    string FfmpegExecutable,
    string FfprobeExecutable,
    string PythonExecutable,
    string Iw3EngineDirectory,
    string ModelsDirectory)
{
    public string ModelCatalogFile { get; init; } =
        Path.Combine(ModelsDirectory, Iw3EngineBundleContract.ModelCatalogFileName);

    public string Iw3CliCapabilitiesFile { get; init; } =
        Path.Combine(Iw3EngineDirectory, Iw3EngineBundleContract.CliCapabilitiesFileName);
}
