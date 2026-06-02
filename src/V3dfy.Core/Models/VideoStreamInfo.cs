namespace V3dfy.Core.Models;

public sealed record VideoStreamInfo(
    int Index,
    int? Width,
    int? Height,
    double? FrameRate,
    string? CodecName,
    string? PixelFormat,
    string? ColorTransfer,
    string? ColorSpace,
    string? ColorPrimaries,
    bool IsHdr);
