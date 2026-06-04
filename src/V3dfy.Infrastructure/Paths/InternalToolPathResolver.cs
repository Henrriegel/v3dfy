using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.Paths;

public sealed class InternalToolPathResolver
{
    public const string FfmpegExecutableRelativePath = "tools/ffmpeg/win-x64/ffmpeg.exe";
    public const string FfprobeExecutableRelativePath = "tools/ffmpeg/win-x64/ffprobe.exe";

    private readonly string _applicationBaseDirectory;

    public InternalToolPathResolver(string applicationBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        _applicationBaseDirectory = Path.GetFullPath(applicationBaseDirectory);
    }

    public InternalToolPaths Resolve() => new(
        FfmpegExecutable: ResolvePath(FfmpegExecutableRelativePath),
        FfprobeExecutable: ResolvePath(FfprobeExecutableRelativePath),
        PythonExecutable: ResolvePath(Iw3EngineBundleContract.PythonExecutableRelativePath),
        Iw3EngineDirectory: ResolvePath(Iw3EngineBundleContract.EngineDirectoryRelativePath),
        ModelsDirectory: ResolvePath(Iw3EngineBundleContract.ModelsDirectoryRelativePath))
    {
        NunifRootDirectory = ResolvePath(Iw3EngineBundleContract.NunifRootDirectoryRelativePath),
        Iw3PackageDirectory = ResolvePath(Iw3EngineBundleContract.Iw3PackageDirectoryRelativePath),
        ModelCatalogFile = ResolvePath(Iw3EngineBundleContract.ModelCatalogRelativePath),
        Iw3CliCapabilitiesFile = ResolvePath(Iw3EngineBundleContract.CliCapabilitiesRelativePath),
    };

    private string ResolvePath(string relativePath) =>
        Path.GetFullPath(Path.Combine(
            [_applicationBaseDirectory, .. SplitRelativePath(relativePath)]));

    private static string[] SplitRelativePath(string relativePath) =>
        relativePath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);
}
