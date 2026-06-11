namespace V3dfy.Core.Models;

public sealed record ModelPackManifest
{
    public const string FileName = "MODEL_PACK.json";
    public const int SupportedSchemaVersion = 1;
    public const string ExpectedTargetRoot = "iw3-pretrained-models";

    public int SchemaVersion { get; init; }

    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string TargetRoot { get; init; } = string.Empty;

    public IReadOnlyList<string> CompatibleIw3Versions { get; init; } = [];

    public string MinV3dfyVersion { get; init; } = string.Empty;

    public IReadOnlyList<ModelPackModel> Models { get; init; } = [];

    public IReadOnlyList<ModelPackFile> Files { get; init; } = [];

    public IReadOnlyList<string> Licenses { get; init; } = [];
}

public sealed record ModelPackModel
{
    public string MappingKey { get; init; } = string.Empty;

    public string Iw3DepthModelName { get; init; } = string.Empty;

    public string File { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string Category { get; init; } = string.Empty;
}

public sealed record ModelPackFile
{
    public string Path { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string Role { get; init; } = string.Empty;
}
