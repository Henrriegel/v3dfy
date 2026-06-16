namespace V3dfy.Core.Image;

public static class ImageStereoExportPathBuilder
{
    private const string PngExtension = ".png";

    public static ImageStereoExportOutputPaths CreateOutputPaths(
        string sourcePath,
        string outputDirectory,
        ImageStereoExportFormat format,
        string? selectedModelName = null,
        Func<string, bool>? pathExists = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        pathExists ??= File.Exists;

        var baseName = CreateBaseFileName(sourcePath, format, selectedModelName);
        if (format != ImageStereoExportFormat.LeftRightPair)
        {
            var outputPath = CreateCollisionSafePath(outputDirectory, baseName, PngExtension, pathExists);
            return new(outputPath, [outputPath]);
        }

        var leftPath = CreateCollisionSafePath(outputDirectory, $"{baseName}_left", PngExtension, pathExists);
        var rightPath = CreateCollisionSafePath(outputDirectory, $"{baseName}_right", PngExtension, pathExists);
        return new(leftPath, [leftPath, rightPath]);
    }

    public static string CreateBaseFileName(
        string sourcePath,
        ImageStereoExportFormat format,
        string? selectedModelName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var sourceBase = SanitizeFileNamePart(Path.GetFileNameWithoutExtension(sourcePath));
        var formatSuffix = GetFormatSuffix(format);
        var modelSuffix = string.IsNullOrWhiteSpace(selectedModelName)
            ? string.Empty
            : $"-{SanitizeFileNamePart(selectedModelName)}";

        return $"{sourceBase}{modelSuffix}-{formatSuffix}";
    }

    public static string SanitizeFileNamePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "image";
        }

        var characters = new List<char>(value.Length);
        var previousWasSeparator = false;
        foreach (var rawCharacter in value.Trim())
        {
            var character = char.IsLetterOrDigit(rawCharacter) ? rawCharacter : rawCharacter switch
            {
                '-' or '_' => rawCharacter,
                _ => '-',
            };

            if (character == '-')
            {
                if (previousWasSeparator || characters.Count == 0)
                {
                    continue;
                }

                previousWasSeparator = true;
                characters.Add(character);
                continue;
            }

            previousWasSeparator = false;
            characters.Add(character);
        }

        while (characters.Count > 0 && characters[^1] == '-')
        {
            characters.RemoveAt(characters.Count - 1);
        }

        return characters.Count == 0 ? "image" : new string(characters.ToArray());
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

    private static string GetFormatSuffix(ImageStereoExportFormat format) => format switch
    {
        ImageStereoExportFormat.SideBySide => "sbs",
        ImageStereoExportFormat.HalfTopBottom => "tab",
        ImageStereoExportFormat.Anaglyph => "anaglyph",
        ImageStereoExportFormat.LeftRightPair => "lr-pair",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };
}
