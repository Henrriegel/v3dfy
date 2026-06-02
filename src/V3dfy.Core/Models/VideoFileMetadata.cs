namespace V3dfy.Core.Models;

public sealed record VideoFileMetadata(
    TimeSpan? Duration,
    string? FormatName,
    long? OverallBitRate);
