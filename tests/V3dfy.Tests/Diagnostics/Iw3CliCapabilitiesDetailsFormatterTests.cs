using V3dfy.Core.Diagnostics;
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
            useSpanish: false);

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
            useSpanish: false);

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
            useSpanish: false);

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
            useSpanish: false);

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
            useSpanish: true);

        Assert.Contains("Capacidades CLI de iw3", lines);
        Assert.Contains("Estado: Encontrado", lines);
        Assert.Contains("Comando base verificado: Sí", lines);
        Assert.Contains("Opciones verificadas: 1 (-i)", lines);
        Assert.Contains("Opciones no verificadas: 1 (calidad)", lines);
    }
}
