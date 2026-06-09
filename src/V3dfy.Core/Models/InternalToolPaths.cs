namespace V3dfy.Core.Models;

public sealed record InternalToolPaths(
    string FfmpegExecutable,
    string FfprobeExecutable,
    string PythonExecutable,
    string Iw3EngineDirectory,
    string ModelsDirectory)
{
    public string NunifRootDirectory { get; init; } =
        Path.Combine(Iw3EngineDirectory, Iw3EngineBundleContract.NunifDirectoryName);

    public string Iw3PackageDirectory { get; init; } =
        Path.Combine(
            Iw3EngineDirectory,
            Iw3EngineBundleContract.NunifDirectoryName,
            Iw3EngineBundleContract.Iw3PackageDirectoryName);

    public string ModelCatalogFile { get; init; } =
        Path.Combine(ModelsDirectory, Iw3EngineBundleContract.ModelCatalogFileName);

    public string Iw3CliCapabilitiesFile { get; init; } =
        Path.Combine(Iw3EngineDirectory, Iw3EngineBundleContract.CliCapabilitiesFileName);

    public string Iw3DefaultStereoRuntimeDependencyFile { get; init; } =
        Path.Combine(
            ModelsDirectory,
            "hub",
            "checkpoints",
            Iw3EngineBundleContract.Iw3DefaultStereoRuntimeDependencyFileName);
}
