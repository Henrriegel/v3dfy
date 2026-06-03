using System.IO;

namespace V3dfy.Core.Planning;

public sealed class ConversionOutputPathState
{
    public string? CustomOutputPath { get; private set; }

    public bool HasCustomOutputPath => !string.IsNullOrWhiteSpace(CustomOutputPath);

    public bool CommitOutputPathText(string value, out string? normalizedPath)
    {
        normalizedPath = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

        if (string.Equals(CustomOutputPath, normalizedPath, StringComparison.Ordinal))
        {
            return false;
        }

        CustomOutputPath = normalizedPath;
        return true;
    }

    public bool SetCustomOutputPath(string outputPath)
    {
        if (string.Equals(CustomOutputPath, outputPath, StringComparison.Ordinal))
        {
            return false;
        }

        CustomOutputPath = outputPath;
        return true;
    }

    public bool ResetCustomOutputPath()
    {
        if (CustomOutputPath is null)
        {
            return false;
        }

        CustomOutputPath = null;
        return true;
    }

    public void ClearCustomOutputPath() => CustomOutputPath = null;

    public string? GetInitialOutputDirectory(string? automaticOutputPath)
    {
        var outputPath = string.IsNullOrWhiteSpace(CustomOutputPath)
            ? automaticOutputPath
            : CustomOutputPath;

        return string.IsNullOrWhiteSpace(outputPath)
            ? null
            : Path.GetDirectoryName(outputPath);
    }
}
