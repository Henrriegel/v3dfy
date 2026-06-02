namespace V3dfy.Core.Models;

public sealed record AudioStreamInfo(
    int Index,
    string? CodecName,
    int? Channels,
    string? ChannelLayout,
    int? SampleRate,
    string? Language);
