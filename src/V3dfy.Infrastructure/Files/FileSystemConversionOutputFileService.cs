using V3dfy.Core.Execution;

namespace V3dfy.Infrastructure.Files;

public sealed class FileSystemConversionOutputFileService : IConversionOutputFileService
{
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
}
