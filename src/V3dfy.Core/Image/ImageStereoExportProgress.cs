namespace V3dfy.Core.Image;

public sealed record ImageStereoExportProgress(
    int ProgressPercent,
    string EnglishMessage,
    string SpanishMessage);
