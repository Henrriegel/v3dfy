using V3dfy.Core.Preview;

namespace V3dfy.Infrastructure.Files;

public sealed class FileSystemPreviewCacheFileService : IPreviewCacheFileService
{
    public void EnsureDirectory(string directory) => Directory.CreateDirectory(directory);

    public bool Exists(string path) => File.Exists(path);

    public void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void Move(string sourcePath, string destinationPath, bool overwrite) =>
        File.Move(sourcePath, destinationPath, overwrite);

    public IReadOnlyList<PreviewCacheFile> EnumerateFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new PreviewCacheFile(
                path,
                File.GetLastWriteTimeUtc(path)))
            .ToArray();
    }
}
