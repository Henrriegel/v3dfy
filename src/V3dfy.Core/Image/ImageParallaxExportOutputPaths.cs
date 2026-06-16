namespace V3dfy.Core.Image;

public sealed record ImageParallaxExportOutputPaths(
    string PrimaryOutputPath,
    IReadOnlyList<string> GeneratedFiles);
