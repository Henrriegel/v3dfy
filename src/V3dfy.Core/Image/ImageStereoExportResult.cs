namespace V3dfy.Core.Image;

public sealed record ImageStereoExportResult(
    bool Success,
    bool WasBlocked,
    string EnglishSummary,
    string SpanishSummary,
    string OutputDirectory,
    IReadOnlyList<string> GeneratedFiles,
    string? PrimaryOutputPath,
    string? TechnicalDetail = null,
    int? ExitCode = null,
    string CommandPreview = "",
    string StandardOutputSummary = "",
    string StandardErrorSummary = "");
