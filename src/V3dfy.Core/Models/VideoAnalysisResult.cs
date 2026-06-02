namespace V3dfy.Core.Models;

public sealed record VideoAnalysisResult(
    string InputPath,
    VideoFileMetadata File,
    VideoStreamInfo? Video,
    IReadOnlyList<AudioStreamInfo> AudioStreams,
    IReadOnlyList<SubtitleStreamInfo> SubtitleStreams,
    IReadOnlyList<VideoCompatibilityWarning> Warnings);
