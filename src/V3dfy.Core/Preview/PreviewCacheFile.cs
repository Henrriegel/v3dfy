namespace V3dfy.Core.Preview;

public sealed record PreviewCacheFile(
    string Path,
    DateTimeOffset LastWriteTimeUtc);
