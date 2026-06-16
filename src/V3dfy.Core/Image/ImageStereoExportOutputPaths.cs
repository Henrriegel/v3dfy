namespace V3dfy.Core.Image;

public sealed record ImageStereoExportOutputPaths(
    string PrimaryOutputPath,
    IReadOnlyList<string> GeneratedFiles);
