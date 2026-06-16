namespace V3dfy.Core.Image;

public static class ImageParallaxExportPathBuilder
{
    private const string Mp4Extension = ".mp4";

    public static ImageParallaxExportOutputPaths CreateOutputPaths(
        string sourcePath,
        string outputDirectory,
        string? selectedModelName = null,
        string? duration = null,
        Func<string, bool>? pathExists = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        pathExists ??= File.Exists;

        var baseName = CreateBaseFileName(sourcePath, selectedModelName, duration);
        var outputPath = CreateCollisionSafePath(outputDirectory, baseName, Mp4Extension, pathExists);
        return new(outputPath, [outputPath]);
    }

    public static string CreateBaseFileName(
        string sourcePath,
        string? selectedModelName = null,
        string? duration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var sourceBase = ImageStereoExportPathBuilder.SanitizeFileNamePart(
            Path.GetFileNameWithoutExtension(sourcePath));
        var modelSuffix = string.IsNullOrWhiteSpace(selectedModelName)
            ? string.Empty
            : $"-{ImageStereoExportPathBuilder.SanitizeFileNamePart(selectedModelName)}";
        var durationSuffix = string.IsNullOrWhiteSpace(duration)
            ? string.Empty
            : $"-{ImageStereoExportPathBuilder.SanitizeFileNamePart(duration)}";

        return $"{sourceBase}{modelSuffix}-parallax-2d{durationSuffix}";
    }

    private static string CreateCollisionSafePath(
        string outputDirectory,
        string fileNameWithoutExtension,
        string extension,
        Func<string, bool> pathExists)
    {
        var candidate = Path.Combine(outputDirectory, fileNameWithoutExtension + extension);
        var suffix = 2;
        while (pathExists(candidate))
        {
            candidate = Path.Combine(outputDirectory, $"{fileNameWithoutExtension}-{suffix}{extension}");
            suffix++;
        }

        return candidate;
    }
}
