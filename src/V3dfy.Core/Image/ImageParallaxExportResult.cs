namespace V3dfy.Core.Image;

public sealed record ImageParallaxExportResult(
    bool Success,
    bool WasBlocked,
    string EnglishSummary,
    string SpanishSummary,
    string OutputDirectory,
    IReadOnlyList<string> GeneratedFiles,
    string? PrimaryOutputPath,
    string? DepthMapPath = null,
    string? TechnicalDetail = null,
    int? DepthExportExitCode = null,
    int? FrameGenerationExitCode = null,
    int? FfmpegExitCode = null,
    string DepthExportCommandPreview = "",
    string FrameGenerationCommandPreview = "",
    string FfmpegCommandPreview = "",
    string StandardOutputSummary = "",
    string StandardErrorSummary = "");
