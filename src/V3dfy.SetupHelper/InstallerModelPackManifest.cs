using System.Text.Json;

namespace V3dfy.SetupHelper;

public sealed record InstallerModelPackManifest
{
    public const int SupportedSchemaVersion = 1;

    public int SchemaVersion { get; init; }

    public string V3dfyVersion { get; init; } = string.Empty;

    public string ModelPackVersion { get; init; } = string.Empty;

    public string ReleaseTag { get; init; } = string.Empty;

    public string ModelPackReleaseBaseUrl { get; init; } = string.Empty;

    public string CurrentIw3Version { get; init; } = string.Empty;

    public string GeneratedUtc { get; init; } = string.Empty;

    public IReadOnlyList<InstallerModelPackEntry> Packs { get; init; } = [];

    public static async Task<InstallerModelPackManifest> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var manifest = await JsonSerializer.DeserializeAsync<InstallerModelPackManifest>(
                stream,
                InstallerModelPackManifestJson.Options,
                cancellationToken);
            return Validate(manifest, path);
        }
        catch (InstallerModelPackManifestException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InstallerModelPackManifestException(
                $"Could not read installer model-pack manifest '{path}': {ex.Message}",
                ex);
        }
    }

    public static InstallerModelPackManifest Parse(string json, string sourceName = "installer model-pack manifest")
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<InstallerModelPackManifest>(
                json,
                InstallerModelPackManifestJson.Options);
            return Validate(manifest, sourceName);
        }
        catch (InstallerModelPackManifestException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new InstallerModelPackManifestException(
                $"Could not parse {sourceName}: {ex.Message}",
                ex);
        }
    }

    private static InstallerModelPackManifest Validate(
        InstallerModelPackManifest? manifest,
        string sourceName)
    {
        if (manifest is null)
        {
            throw new InstallerModelPackManifestException(
                $"Installer model-pack manifest is empty: {sourceName}");
        }

        var errors = new List<string>();
        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add($"Unsupported installer model-pack manifest schemaVersion: {manifest.SchemaVersion}.");
        }

        if (manifest.Packs is null)
        {
            errors.Add("Installer model-pack manifest packs cannot be null.");
        }

        var packIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assetFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (pack, index) in (manifest.Packs ?? []).Select((pack, index) => (pack, index)))
        {
            ValidatePack(pack, index, packIds, assetFileNames, errors);
        }

        if (errors.Count > 0)
        {
            throw new InstallerModelPackManifestException(
                $"Invalid installer model-pack manifest '{sourceName}': {string.Join(" ", errors)}");
        }

        return manifest with
        {
            Packs = [.. (manifest.Packs ?? []).Where(static pack => pack is not null)],
        };
    }

    private static void ValidatePack(
        InstallerModelPackEntry? pack,
        int index,
        HashSet<string> packIds,
        HashSet<string> assetFileNames,
        List<string> errors)
    {
        var prefix = $"packs[{index}]";
        if (pack is null)
        {
            errors.Add($"{prefix} cannot be null.");
            return;
        }

        Require(pack.PackId, $"{prefix}.packId", errors);
        if (!string.IsNullOrWhiteSpace(pack.PackId) && !packIds.Add(pack.PackId))
        {
            errors.Add($"Duplicate packId in installer model-pack manifest: {pack.PackId}.");
        }

        if (!pack.InstallerSelectable)
        {
            return;
        }

        Require(pack.DisplayName, $"{prefix}.displayName", errors);
        Require(pack.AssetFileName, $"{prefix}.assetFileName", errors);
        Require(pack.RelativeArtifactPath, $"{prefix}.relativeArtifactPath", errors);
        Require(pack.Url, $"{prefix}.url", errors);
        Require(pack.ZipSha256, $"{prefix}.zipSha256", errors);

        if (!string.IsNullOrWhiteSpace(pack.AssetFileName))
        {
            if (!IsSimpleFileName(pack.AssetFileName))
            {
                errors.Add($"{prefix}.assetFileName must be a simple file name: {pack.AssetFileName}.");
            }
            else if (!assetFileNames.Add(pack.AssetFileName))
            {
                errors.Add($"Duplicate assetFileName in installer model-pack manifest: {pack.AssetFileName}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(pack.RelativeArtifactPath) &&
            !IsSafeRelativePath(pack.RelativeArtifactPath))
        {
            errors.Add($"{prefix}.relativeArtifactPath must be a relative artifact path: {pack.RelativeArtifactPath}.");
        }

        if (pack.ZipSizeBytes <= 0)
        {
            errors.Add($"{prefix}.zipSizeBytes must be positive.");
        }

        if (!string.IsNullOrWhiteSpace(pack.Url) && !IsHttpUrl(pack.Url))
        {
            errors.Add($"{prefix}.url must be an absolute HTTP or HTTPS URL: {pack.Url}.");
        }
    }

    private static void Require(string value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required.");
        }
    }

    private static bool IsSimpleFileName(string value) =>
        !Path.IsPathRooted(value) &&
        !value.Contains('/', StringComparison.Ordinal) &&
        !value.Contains('\\', StringComparison.Ordinal) &&
        !HasDriveRoot(value) &&
        string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal);

    private static bool IsSafeRelativePath(string value) =>
        !Path.IsPathRooted(value) &&
        !HasDriveRoot(value) &&
        !value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Any(static segment => segment is "." or "..");

    private static bool HasDriveRoot(string value) =>
        value.Length >= 2 &&
        char.IsAsciiLetter(value[0]) &&
        value[1] == ':';

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

public sealed record InstallerModelPackEntry
{
    public string PackId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string BestUseEnglish { get; init; } = string.Empty;

    public string BestUseSpanish { get; init; } = string.Empty;

    public string AssetFileName { get; init; } = string.Empty;

    public string RelativeArtifactPath { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string ZipSha256 { get; init; } = string.Empty;

    public long ZipSizeBytes { get; init; }

    public string CheckpointPath { get; init; } = string.Empty;

    public string CheckpointSha256 { get; init; } = string.Empty;

    public long CheckpointSizeBytes { get; init; }

    public IReadOnlyList<string> Iw3DepthModelNames { get; init; } = [];

    public IReadOnlyList<string> MappingKeys { get; init; } = [];

    public bool InstallerSelectable { get; init; }

    public bool DefaultSelected { get; init; }

    public string License { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public string ModelCardUrl { get; init; } = string.Empty;

    public IReadOnlyList<string> RecommendedFor { get; init; } = [];

    public string SizeCategory { get; init; } = string.Empty;
}

public sealed class InstallerModelPackManifestException : Exception
{
    public InstallerModelPackManifestException(string message)
        : base(message)
    {
    }

    public InstallerModelPackManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal static class InstallerModelPackManifestJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
