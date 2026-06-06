namespace V3dfy.Core.Execution;

public interface IConversionOutputFileService
{
    bool Exists(string path);

    void DeleteIfExists(string path);

    void Move(string sourcePath, string destinationPath, bool overwrite);
}
