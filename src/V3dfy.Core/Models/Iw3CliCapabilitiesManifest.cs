namespace V3dfy.Core.Models;

public sealed record Iw3CliCapabilitiesManifest(
    string ManifestPath,
    Iw3CliCapabilitiesStatus Status,
    string? ErrorMessage,
    string BundledIw3Version,
    bool VerifiedBaseCommand,
    IReadOnlyList<string> VerifiedOptions,
    IReadOnlyList<string> UnverifiedOptions,
    string VerificationSource,
    string VerifiedAtUtc,
    string Notes)
{
    public bool HasVerifiedCapabilities =>
        Status == Iw3CliCapabilitiesStatus.Found && VerifiedBaseCommand;

    public static Iw3CliCapabilitiesManifest Missing(string manifestPath) => new(
        ManifestPath: manifestPath,
        Status: Iw3CliCapabilitiesStatus.Missing,
        ErrorMessage: null,
        BundledIw3Version: string.Empty,
        VerifiedBaseCommand: false,
        VerifiedOptions: [],
        UnverifiedOptions: [],
        VerificationSource: string.Empty,
        VerifiedAtUtc: string.Empty,
        Notes: string.Empty);

    public static Iw3CliCapabilitiesManifest Invalid(
        string manifestPath,
        string errorMessage) => Missing(manifestPath) with
    {
        Status = Iw3CliCapabilitiesStatus.Invalid,
        ErrorMessage = errorMessage,
    };

    public static Iw3CliCapabilitiesManifest Placeholder(
        string manifestPath) => Missing(manifestPath) with
    {
        Status = Iw3CliCapabilitiesStatus.Placeholder,
    };
}
