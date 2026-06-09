namespace V3dfy.Core.Preview;

public static class PreviewCachePathSafety
{
    public static IReadOnlyList<string> GetPathsOutsideCache(PreviewCachePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return paths.AllPaths
            .Where(path => !IsPathInsideRoot(paths.CacheDirectory, path))
            .ToArray();
    }

    public static bool AreAllPathsInsideCache(PreviewCachePaths paths) =>
        GetPathsOutsideCache(paths).Count == 0;

    public static bool IsPathInsideRoot(string rootDirectory, string path)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) ||
            string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(path);

        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith(
                root + Path.AltDirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
