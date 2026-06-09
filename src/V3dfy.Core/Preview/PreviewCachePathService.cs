using V3dfy.Core.Models;

namespace V3dfy.Core.Preview;

public sealed class PreviewCachePathService
{
    private readonly IPreviewCachePathProvider _pathProvider;

    public PreviewCachePathService(IPreviewCachePathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public PreviewCachePaths CreatePaths(
        PreviewConfigurationSnapshot configuration,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var cacheDirectory = Path.GetFullPath(_pathProvider.GetPreviewCacheDirectory());
        var sourceBaseName = SanitizeFileName(
            Path.GetFileNameWithoutExtension(configuration.SourcePath));
        var modelKey = SanitizeFileName(configuration.ModelKey);
        var layout = GetLayoutSuffix(configuration.ThreeDOutputFormat);
        var intensity = configuration.Intensity.ToString().ToLowerInvariant();
        var timestamp = createdAtUtc.UtcDateTime.ToString("yyyyMMddHHmmss");
        var baseName = $"{sourceBaseName}.{modelKey}.{layout}.{intensity}.{timestamp}";
        var extension = configuration.OutputContainer.ToString().ToLowerInvariant();

        var paths = new PreviewCachePaths(
            CacheDirectory: cacheDirectory,
            ShortSourcePath: Path.Combine(cacheDirectory, $"{baseName}.source.mkv"),
            PartialShortSourcePath: Path.Combine(cacheDirectory, $"{baseName}.source.v3dfy-partial.mkv"),
            PreviewOutputPath: Path.Combine(cacheDirectory, $"{baseName}.preview.{extension}"),
            PartialPreviewOutputPath: Path.Combine(cacheDirectory, $"{baseName}.preview.v3dfy-partial.{extension}"));

        if (!paths.AreAllPathsInsideCache)
        {
            throw new InvalidOperationException(
                "Preview cache path construction produced one or more paths outside the preview cache directory.");
        }

        return paths;
    }

    private static string GetLayoutSuffix(ThreeDOutputFormat format) => format switch
    {
        ThreeDOutputFormat.HalfTopBottom => "htab",
        ThreeDOutputFormat.HalfSideBySide => "hsbs",
        ThreeDOutputFormat.Anaglyph => "anaglyph",
        _ => format.ToString().ToLowerInvariant(),
    };

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "preview";
        }

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = value
            .Select(character => invalid.Contains(character) ? '-' : character)
            .ToArray();
        var sanitized = new string(chars).Trim('.', ' ', '-');

        return string.IsNullOrWhiteSpace(sanitized)
            ? "preview"
            : sanitized;
    }
}
