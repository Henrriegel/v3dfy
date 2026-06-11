namespace V3dfy.Infrastructure.ModelPacks;

public interface IModelPackInstallFileOperations
{
    bool FileExists(string path);

    long GetFileLength(string path);

    Stream OpenRead(string path);

    void CreateDirectory(string path);

    Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken);

    void MoveFile(string sourcePath, string destinationPath);

    void DeleteFile(string path);

    void DeleteDirectory(string path, bool recursive);
}
