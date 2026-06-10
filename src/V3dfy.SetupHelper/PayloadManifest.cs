using System.Text.Json;

namespace V3dfy.SetupHelper;

public sealed class PayloadManifest
{
    public string ProductName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string ReleaseBaseUrl { get; init; } = string.Empty;

    public string ZipFileName { get; init; } = string.Empty;

    public string ZipSha256 { get; init; } = string.Empty;

    public long ZipSizeBytes { get; init; }

    public IReadOnlyList<PayloadPart> Parts { get; init; } = [];

    public IReadOnlyList<string> RequiredInstalledPaths { get; init; } = [];

    public static async Task<PayloadManifest> LoadAsync(string manifestPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<PayloadManifest>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken);

        if (manifest is null)
        {
            throw new PayloadInstallException("The payload manifest is empty or invalid JSON.");
        }

        manifest.Validate();
        return manifest;
    }

    public void Validate()
    {
        RequireValue(ProductName, "productName");
        RequireValue(Version, "version");
        RequireValue(ZipFileName, "zipFileName");
        RequireHash(ZipSha256, "zipSha256");

        if (ZipSizeBytes <= 0)
        {
            throw new PayloadInstallException("The payload manifest must include a positive zipSizeBytes value.");
        }

        if (Parts.Count == 0)
        {
            throw new PayloadInstallException("The payload manifest must include at least one payload part.");
        }

        foreach (var part in Parts)
        {
            part.Validate();
        }

        if (RequiredInstalledPaths.Count == 0)
        {
            throw new PayloadInstallException("The payload manifest must include requiredInstalledPaths.");
        }
    }

    private static void RequireValue(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PayloadInstallException($"The payload manifest is missing {propertyName}.");
        }
    }

    private static void RequireHash(string value, string propertyName)
    {
        RequireValue(value, propertyName);

        if (value.Length != 64 || value.Any(static c => !Uri.IsHexDigit(c)))
        {
            throw new PayloadInstallException($"The payload manifest contains an invalid {propertyName} value.");
        }
    }
}

public sealed class PayloadPart
{
    public string FileName { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string Url { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FileName))
        {
            throw new PayloadInstallException("A payload part is missing fileName.");
        }

        if (Path.GetFileName(FileName) != FileName)
        {
            throw new PayloadInstallException($"Payload part names must not include directories: {FileName}");
        }

        if (Sha256.Length != 64 || Sha256.Any(static c => !Uri.IsHexDigit(c)))
        {
            throw new PayloadInstallException($"Payload part {FileName} contains an invalid sha256 value.");
        }

        if (SizeBytes <= 0)
        {
            throw new PayloadInstallException($"Payload part {FileName} must include a positive sizeBytes value.");
        }
    }
}
