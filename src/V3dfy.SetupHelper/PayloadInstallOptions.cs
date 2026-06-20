namespace V3dfy.SetupHelper;

public enum PayloadInstallMode
{
    Web,
    Offline,
}

public sealed class PayloadInstallOptions
{
    public required PayloadInstallMode Mode { get; init; }

    public required string ManifestPath { get; init; }

    public required string TargetDirectory { get; init; }

    public required string WorkDirectory { get; init; }

    public string? PartsDirectory { get; init; }

    public string? ReleaseBaseUrlOverride { get; init; }

    public string? ModelPacksManifestPath { get; init; }

    public string? ModelPacksSourceDirectory { get; init; }

    public bool KeepWorkDirectory { get; init; }

    public bool AllowTargetReplacement { get; init; }
}
