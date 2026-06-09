namespace V3dfy.Core.Preview;

public sealed record PreviewCachePaths(
    string CacheDirectory,
    string ShortSourcePath,
    string PartialShortSourcePath,
    string PreviewOutputPath,
    string PartialPreviewOutputPath)
{
    public IReadOnlyList<string> AllPaths =>
    [
        ShortSourcePath,
        PartialShortSourcePath,
        PreviewOutputPath,
        PartialPreviewOutputPath,
    ];

    public IReadOnlyList<string> PartialPaths =>
    [
        PartialShortSourcePath,
        PartialPreviewOutputPath,
    ];

    public IReadOnlyList<string> PathsOutsideCache =>
        PreviewCachePathSafety.GetPathsOutsideCache(this);

    public bool AreAllPathsInsideCache =>
        PreviewCachePathSafety.AreAllPathsInsideCache(this);
}
