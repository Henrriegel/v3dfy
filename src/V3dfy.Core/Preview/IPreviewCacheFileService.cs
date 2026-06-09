namespace V3dfy.Core.Preview;

public interface IPreviewCacheFileService
{
    void EnsureDirectory(string directory);

    bool Exists(string path);

    void DeleteIfExists(string path);

    void Move(string sourcePath, string destinationPath, bool overwrite);

    IReadOnlyList<PreviewCacheFile> EnumerateFiles(string directory);
}
