using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed class LocalAppDataModelPackImportWorkPathProvider : IModelPackImportWorkPathProvider
{
    public ModelPackImportWorkPaths CreateWorkPaths()
    {
        var rootDirectory = Path.Combine(
            GetLocalAppDataRoot(),
            "v3dfy",
            "model-pack-imports",
            Guid.NewGuid().ToString("N"));

        return new ModelPackImportWorkPaths(
            RootDirectory: rootDirectory,
            StagingRoot: Path.Combine(rootDirectory, "staging"),
            ResultPath: Path.Combine(rootDirectory, "model-pack-result.json"),
            LogPath: Path.Combine(rootDirectory, "model-pack-log.json"));
    }

    private static string GetLocalAppDataRoot()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return localAppData;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "Local");
    }
}
