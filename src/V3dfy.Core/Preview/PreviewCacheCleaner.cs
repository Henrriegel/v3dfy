namespace V3dfy.Core.Preview;

public sealed class PreviewCacheCleaner
{
    private readonly IPreviewCacheFileService _fileService;

    public PreviewCacheCleaner(IPreviewCacheFileService fileService)
    {
        _fileService = fileService;
    }

    public int DeleteStaleFiles(
        string cacheDirectory,
        TimeSpan maxAge,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        _fileService.EnsureDirectory(cacheDirectory);
        var deleted = 0;
        foreach (var file in _fileService.EnumerateFiles(cacheDirectory))
        {
            if (!PreviewCachePathSafety.IsPathInsideRoot(cacheDirectory, file.Path) ||
                now - file.LastWriteTimeUtc < maxAge)
            {
                continue;
            }

            _fileService.DeleteIfExists(file.Path);
            deleted++;
        }

        return deleted;
    }

    public int DeletePreviewFiles(
        string cacheDirectory,
        IEnumerable<string?> paths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(paths);

        var deleted = 0;
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (path is not null && PreviewCachePathSafety.IsPathInsideRoot(cacheDirectory, path))
            {
                _fileService.DeleteIfExists(path);
                deleted++;
            }
        }

        return deleted;
    }

    public int DeletePartialFiles(string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        _fileService.EnsureDirectory(cacheDirectory);
        var deleted = 0;
        foreach (var file in _fileService.EnumerateFiles(cacheDirectory))
        {
            if (!IsPreviewPartialFilePath(cacheDirectory, file.Path))
            {
                continue;
            }

            _fileService.DeleteIfExists(file.Path);
            deleted++;
        }

        return deleted;
    }

    public static bool IsPreviewPartialFilePath(
        string cacheDirectory,
        string path)
    {
        if (!PreviewCachePathSafety.IsPathInsideRoot(cacheDirectory, path))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        return fileName.Contains("v3dfy-partial", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPathInsideRoot(string rootDirectory, string path) =>
        PreviewCachePathSafety.IsPathInsideRoot(rootDirectory, path);
}
