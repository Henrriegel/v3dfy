using V3dfy.Core.Models;
using V3dfy.Infrastructure.ModelPacks;

namespace V3dfy.Tests.Infrastructure;

public sealed class ElevatedModelPackInstallLauncherTests : IDisposable
{
    private readonly string root = TestPaths.TempRoot(
        "elevated-model-pack-launcher",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildStartInfo_BuildsRunasProcessStartInfoWithoutStartingIt()
    {
        var request = CreateRequest();

        var startInfo = new ElevatedModelPackInstallLauncher().BuildStartInfo(request);

        Assert.Equal(request.HelperExecutablePath, startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal("runas", startInfo.Verb);
        Assert.Contains(ModelPackHelperContract.AreaArgument, startInfo.ArgumentList);
        Assert.Contains(ModelPackHelperContract.InstallArgument, startInfo.ArgumentList);
    }

    [Fact]
    public void BuildStartInfo_ArgumentsIncludeModelPackInstallInputs()
    {
        var request = CreateRequest();

        var startInfo = new ElevatedModelPackInstallLauncher().BuildStartInfo(request);

        AssertArgumentPair(startInfo.ArgumentList, ModelPackHelperContract.ZipSwitch, request.ModelPackZipPath);
        AssertArgumentPair(startInfo.ArgumentList, ModelPackHelperContract.TargetRootSwitch, request.TargetPretrainedModelsRoot);
        AssertArgumentPair(startInfo.ArgumentList, ModelPackHelperContract.StagingRootSwitch, request.StagingRoot);
        AssertArgumentPair(startInfo.ArgumentList, ModelPackHelperContract.CurrentIw3VersionSwitch, request.CurrentIw3Version);
        AssertArgumentPair(startInfo.ArgumentList, ModelPackHelperContract.ResultSwitch, request.ResultPath!);
        AssertArgumentPair(startInfo.ArgumentList, ModelPackHelperContract.LogSwitch, request.LogPath!);
    }

    [Fact]
    public void BuildStartInfo_RejectsMissingRequiredPaths()
    {
        var request = CreateRequest();
        var launcher = new ElevatedModelPackInstallLauncher();

        Assert.Throws<ArgumentException>(() => launcher.BuildStartInfo(request with { HelperExecutablePath = string.Empty }));
        Assert.Throws<ArgumentException>(() => launcher.BuildStartInfo(request with { HelperExecutablePath = Path.Combine(root, "missing.exe") }));
        Assert.Throws<ArgumentException>(() => launcher.BuildStartInfo(request with { ModelPackZipPath = string.Empty }));
        Assert.Throws<ArgumentException>(() => launcher.BuildStartInfo(request with { TargetPretrainedModelsRoot = string.Empty }));
        Assert.Throws<ArgumentException>(() => launcher.BuildStartInfo(request with { StagingRoot = string.Empty }));
        Assert.Throws<ArgumentException>(() => launcher.BuildStartInfo(request with { CurrentIw3Version = string.Empty }));
    }

    [Fact]
    public void BuildStartInfo_ArgumentListHandlesPathsWithSpaces()
    {
        var request = CreateRequest(useSpaces: true);

        var startInfo = new ElevatedModelPackInstallLauncher().BuildStartInfo(request);

        Assert.Contains(" ", request.HelperExecutablePath);
        Assert.Contains(" ", request.ModelPackZipPath);
        Assert.Contains(request.ModelPackZipPath, startInfo.ArgumentList);
        Assert.Contains(request.TargetPretrainedModelsRoot, startInfo.ArgumentList);
        Assert.Contains(request.StagingRoot, startInfo.ArgumentList);
        Assert.Contains(request.ResultPath!, startInfo.ArgumentList);
    }

    private ElevatedModelPackInstallLaunchRequest CreateRequest(bool useSpaces = false)
    {
        var baseDirectory = useSpaces
            ? Path.Combine(root, "paths with spaces")
            : root;
        Directory.CreateDirectory(baseDirectory);

        var helperPath = Path.Combine(baseDirectory, "V3dfy.SetupHelper.exe");
        var zipPath = Path.Combine(baseDirectory, "model pack.zip");
        File.WriteAllBytes(helperPath, "helper"u8.ToArray());
        File.WriteAllBytes(zipPath, "zip"u8.ToArray());

        return new ElevatedModelPackInstallLaunchRequest(
            helperPath,
            zipPath,
            Path.Combine(baseDirectory, "app", "engine", "iw3", "nunif", "iw3", "pretrained_models"),
            Path.Combine(baseDirectory, "staging root"),
            "nunif-d23721f1",
            CurrentV3dfyVersion: "0.1.0-preview.1",
            ResultPath: Path.Combine(baseDirectory, "result file.json"),
            LogPath: Path.Combine(baseDirectory, "helper log.json"));
    }

    private static void AssertArgumentPair(
        IList<string> arguments,
        string name,
        string value)
    {
        var index = arguments.IndexOf(name);
        Assert.True(index >= 0);
        Assert.True(index + 1 < arguments.Count);
        Assert.Equal(value, arguments[index + 1]);
    }
}
