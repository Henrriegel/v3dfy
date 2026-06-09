using V3dfy.Core.Preview;

namespace V3dfy.Infrastructure.Paths;

public sealed class LocalAppDataPreviewCachePathProvider : IPreviewCachePathProvider
{
    public string GetPreviewCacheDirectory()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "Local");
        }

        return Path.Combine(localAppData, "v3dfy", "previews");
    }
}
