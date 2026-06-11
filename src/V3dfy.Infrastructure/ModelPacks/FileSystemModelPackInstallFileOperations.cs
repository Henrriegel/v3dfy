namespace V3dfy.Infrastructure.ModelPacks;

public sealed class FileSystemModelPackInstallFileOperations : IModelPackInstallFileOperations
{
    public bool FileExists(string path) =>
        File.Exists(path);

    public long GetFileLength(string path) =>
        new FileInfo(path).Length;

    public Stream OpenRead(string path) =>
        File.OpenRead(path);

    public void CreateDirectory(string path) =>
        Directory.CreateDirectory(path);

    public async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 1024 * 1024;
        await using var source = File.OpenRead(sourcePath);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            useAsync: true);
        await source.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    public void MoveFile(string sourcePath, string destinationPath) =>
        File.Move(sourcePath, destinationPath, overwrite: false);

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive);
        }
    }
}
