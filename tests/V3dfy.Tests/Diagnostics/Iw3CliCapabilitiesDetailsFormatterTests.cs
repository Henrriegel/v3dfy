using V3dfy.Core.Diagnostics;
using V3dfy.Core.Localization;
using V3dfy.Core.Models;

namespace V3dfy.Tests.Diagnostics;

public sealed class Iw3CliCapabilitiesDetailsFormatterTests
{
    [Fact]
    public void CreateLines_MissingCapabilities_ExplainsBaseCommandFallback()
    {
        var capabilities = Iw3CliCapabilitiesManifest.Missing(
            @"C:\app\engine\iw3\IW3_CLI_CAPABILITIES.json");

        var lines = Iw3CliCapabilitiesDetailsFormatter.CreateLines(
            capabilities,
            English);

        Assert.Contains("iw3 CLI capabilities", lines);
        Assert.Contains(
            @"Manifest path: C:\app\engine\iw3\IW3_CLI_CAPABILITIES.json",
            lines);
        Assert.Contains("Status: Missing", lines);
        Assert.Contains(
            "IW3_CLI_CAPABILITIES.json was not found. v3dfy will use the confirmed base iw3 command only.",
            lines);
    }

    [Fact]
    public void CreateLines_InvalidCapabilities_ShowsErrorSafely()
    {
        var capabilities = Iw3CliCapabilitiesManifest.Invalid(
            @"C:\app\engine\iw3\IW3_CLI_CAPABILITIES.json",
            "JSON root must be an object.");

        var lines = Iw3CliCapabilitiesDetailsFormatter.CreateLines(
            capabilities,
            English);

        Assert.Contains("Status: Invalid", lines);
        Assert.Contains(
            "Capabilities metadata could not be read: JSON root must be an object.",
            lines);
    }

    [Fact]
    public void CreateLines_PlaceholderCapabilities_AreNotTreatedAsVerified()
    {
        var capabilities = Iw3CliCapabilitiesManifest.Placeholder(
            @"C:\app\engine\iw3\IW3_CLI_CAPABILITIES.json");

        var lines = Iw3CliCapabilitiesDetailsFormatter.CreateLines(
            capabilities,
            English);

        Assert.Contains("Status: Placeholder", lines);
        Assert.Contains(
            "Capabilities metadata is placeholder and is not treated as verified.",
            lines);
    }

    [Fact]
    public void CreateLines_FoundCapabilities_ShowsVerificationSummary()
    {
        var capabilities = new Iw3CliCapabilitiesManifest(
            ManifestPath: @"C:\app\engine\iw3\IW3_CLI_CAPABILITIES.json",
            Status: Iw3CliCapabilitiesStatus.Found,
            ErrorMessage: null,
            BundledIw3Version: "1.2.3",
            VerifiedBaseCommand: true,
            VerifiedOptions: ["-i", "-o"],
            UnverifiedOptions: ["selected model", "quality preset"],
            VerificationSource: "python -m iw3 -h",
            VerifiedAtUtc: "2026-06-04T00:00:00Z",
            Notes: string.Empty);

        var lines = Iw3CliCapabilitiesDetailsFormatter.CreateLines(
            capabilities,
            English);

        Assert.Contains("Status: Found", lines);
        Assert.Contains("Bundled iw3 version: 1.2.3", lines);
        Assert.Contains("Base command verified: Yes", lines);
        Assert.Contains("Verification source: python -m iw3 -h", lines);
        Assert.Contains("Verified at UTC: 2026-06-04T00:00:00Z", lines);
        Assert.Contains("Verified options: 2 (-i, -o)", lines);
        Assert.Contains("Unverified options: 2 (selected model, quality preset)", lines);
        Assert.Contains(
            "Capabilities metadata is diagnostic only; unverified options are not added to the command.",
            lines);
    }

    [Fact]
    public void CreateLines_SpanishCapabilities_UsesLocalizedStatusAndLabels()
    {
        var capabilities = new Iw3CliCapabilitiesManifest(
            ManifestPath: @"C:\app\engine\iw3\IW3_CLI_CAPABILITIES.json",
            Status: Iw3CliCapabilitiesStatus.Found,
            ErrorMessage: null,
            BundledIw3Version: "1.2.3",
            VerifiedBaseCommand: true,
            VerifiedOptions: ["-i"],
            UnverifiedOptions: ["calidad"],
            VerificationSource: "python -m iw3 -h",
            VerifiedAtUtc: "2026-06-04T00:00:00Z",
            Notes: string.Empty);

        var lines = Iw3CliCapabilitiesDetailsFormatter.CreateLines(
            capabilities,
            Spanish);

        Assert.Contains("Capacidades CLI de iw3", lines);
        Assert.Contains("Estado: Encontrado", lines);
        Assert.Contains("Comando base verificado: Si", lines);
        Assert.Contains("Opciones verificadas: 1 (-i)", lines);
        Assert.Contains("Opciones no verificadas: 1 (calidad)", lines);
    }

    [Fact]
    public void CreateLines_UsesLocalizationKeysInsteadOfSpanishBoolean()
    {
        var source = File.ReadAllText(ReadRepoPath(
            "src",
            "V3dfy.Core",
            "Diagnostics",
            "Iw3CliCapabilitiesDetailsFormatter.cs"));

        Assert.Contains("LocalizedTextProvider localize", source);
        Assert.Contains("LocalizationKeys.TechnicalDetailsIw3CapabilitiesTitle", source);
        Assert.DoesNotContain("bool useSpanish", source);
        Assert.DoesNotContain("private static string Text", source);
    }

    private static string English(
        string key,
        params (string Key, object? Value)[] placeholders) =>
        Localize(EnglishStrings, key, placeholders);

    private static string Spanish(
        string key,
        params (string Key, object? Value)[] placeholders) =>
        Localize(SpanishStrings, key, placeholders);

    private static string Localize(
        IReadOnlyDictionary<string, string> strings,
        string key,
        params (string Key, object? Value)[] placeholders)
    {
        var value = strings.TryGetValue(key, out var text)
            ? text
            : $"[Missing: {key}]";

        foreach (var placeholder in placeholders)
        {
            value = value.Replace(
                "{" + placeholder.Key + "}",
                Convert.ToString(placeholder.Value) ?? string.Empty,
                StringComparison.Ordinal);
        }

        return value;
    }

    private static string ReadRepoPath(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "v3dfy.slnx")))
            {
                return Path.Combine([directory.FullName, .. relativePath]);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static readonly IReadOnlyDictionary<string, string> EnglishStrings =
        new Dictionary<string, string>
        {
            [LocalizationKeys.CommonYes] = "Yes",
            [LocalizationKeys.CommonNo] = "No",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesTitle] = "iw3 CLI capabilities",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesManifestPathFormat] = "Manifest path: {path}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusFormat] = "Status: {status}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesMissing] =
                "IW3_CLI_CAPABILITIES.json was not found. v3dfy will use the confirmed base iw3 command only.",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesInvalidFormat] =
                "Capabilities metadata could not be read: {message}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesPlaceholder] =
                "Capabilities metadata is placeholder and is not treated as verified.",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesBundledVersionFormat] =
                "Bundled iw3 version: {version}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesBaseCommandVerifiedFormat] =
                "Base command verified: {value}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesVerificationSourceFormat] =
                "Verification source: {source}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesVerifiedAtUtcFormat] =
                "Verified at UTC: {timestamp}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesVerifiedOptionsFormat] =
                "Verified options: {options}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesUnverifiedOptionsFormat] =
                "Unverified options: {options}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesDiagnosticOnly] =
                "Capabilities metadata is diagnostic only; unverified options are not added to the command.",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesOptionsMoreFormat] = "+{count} more",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusMissing] = "Missing",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusInvalid] = "Invalid",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusPlaceholder] = "Placeholder",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusFound] = "Found",
        };

    private static readonly IReadOnlyDictionary<string, string> SpanishStrings =
        new Dictionary<string, string>
        {
            [LocalizationKeys.CommonYes] = "Si",
            [LocalizationKeys.CommonNo] = "No",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesTitle] = "Capacidades CLI de iw3",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesManifestPathFormat] =
                "Ruta del manifiesto: {path}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusFormat] = "Estado: {status}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesMissing] =
                "No se encontro IW3_CLI_CAPABILITIES.json. v3dfy usara solo el comando base confirmado de iw3.",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesInvalidFormat] =
                "No se pudo leer la metadata de capacidades: {message}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesPlaceholder] =
                "La metadata de capacidades es un marcador y no se trata como verificada.",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesBundledVersionFormat] =
                "Version iw3 incluida: {version}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesBaseCommandVerifiedFormat] =
                "Comando base verificado: {value}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesVerificationSourceFormat] =
                "Fuente de verificacion: {source}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesVerifiedAtUtcFormat] =
                "Verificado en UTC: {timestamp}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesVerifiedOptionsFormat] =
                "Opciones verificadas: {options}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesUnverifiedOptionsFormat] =
                "Opciones no verificadas: {options}",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesDiagnosticOnly] =
                "La metadata de capacidades es solo diagnostica; las opciones no verificadas no se agregan al comando.",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesOptionsMoreFormat] = "+{count} mas",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusMissing] = "Faltante",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusInvalid] = "No valido",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusPlaceholder] = "Marcador",
            [LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusFound] = "Encontrado",
        };
}
