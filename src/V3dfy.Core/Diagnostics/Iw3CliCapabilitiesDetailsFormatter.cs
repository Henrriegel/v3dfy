using V3dfy.Core.Models;

namespace V3dfy.Core.Diagnostics;

public static class Iw3CliCapabilitiesDetailsFormatter
{
    private const int MaxDisplayedOptions = 8;

    public static IReadOnlyList<string> CreateLines(
        Iw3CliCapabilitiesManifest capabilities,
        bool useSpanish)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var lines = new List<string>
        {
            Text(useSpanish, "iw3 CLI capabilities", "Capacidades CLI de iw3"),
            Text(
                useSpanish,
                $"Manifest path: {capabilities.ManifestPath}",
                $"Ruta del manifiesto: {capabilities.ManifestPath}"),
            Text(
                useSpanish,
                $"Status: {StatusText(capabilities.Status, useSpanish: false)}",
                $"Estado: {StatusText(capabilities.Status, useSpanish: true)}"),
        };

        switch (capabilities.Status)
        {
            case Iw3CliCapabilitiesStatus.Missing:
                lines.Add(Text(
                    useSpanish,
                    "IW3_CLI_CAPABILITIES.json was not found. v3dfy will use the confirmed base iw3 command only.",
                    "No se encontró IW3_CLI_CAPABILITIES.json. v3dfy usará solo el comando base confirmado de iw3."));
                break;
            case Iw3CliCapabilitiesStatus.Invalid:
                lines.Add(Text(
                    useSpanish,
                    $"Capabilities metadata could not be read: {ValueOrDash(capabilities.ErrorMessage)}",
                    $"No se pudo leer la metadata de capacidades: {ValueOrDash(capabilities.ErrorMessage)}"));
                break;
            case Iw3CliCapabilitiesStatus.Placeholder:
                lines.Add(Text(
                    useSpanish,
                    "Capabilities metadata is placeholder and is not treated as verified.",
                    "La metadata de capacidades es placeholder y no se trata como verificada."));
                break;
            case Iw3CliCapabilitiesStatus.Found:
                AddFoundCapabilitiesLines(lines, capabilities, useSpanish);
                break;
        }

        return lines;
    }

    private static void AddFoundCapabilitiesLines(
        ICollection<string> lines,
        Iw3CliCapabilitiesManifest capabilities,
        bool useSpanish)
    {
        lines.Add(Text(
            useSpanish,
            $"Bundled iw3 version: {ValueOrDash(capabilities.BundledIw3Version)}",
            $"Versión iw3 incluida: {ValueOrDash(capabilities.BundledIw3Version)}"));
        lines.Add(Text(
            useSpanish,
            $"Base command verified: {YesNo(capabilities.VerifiedBaseCommand, useSpanish: false)}",
            $"Comando base verificado: {YesNo(capabilities.VerifiedBaseCommand, useSpanish: true)}"));
        lines.Add(Text(
            useSpanish,
            $"Verification source: {ValueOrDash(capabilities.VerificationSource)}",
            $"Fuente de verificación: {ValueOrDash(capabilities.VerificationSource)}"));
        lines.Add(Text(
            useSpanish,
            $"Verified at UTC: {ValueOrDash(capabilities.VerifiedAtUtc)}",
            $"Verificado en UTC: {ValueOrDash(capabilities.VerifiedAtUtc)}"));
        lines.Add(Text(
            useSpanish,
            $"Verified options: {FormatOptions(capabilities.VerifiedOptions, useSpanish: false)}",
            $"Opciones verificadas: {FormatOptions(capabilities.VerifiedOptions, useSpanish: true)}"));
        lines.Add(Text(
            useSpanish,
            $"Unverified options: {FormatOptions(capabilities.UnverifiedOptions, useSpanish: false)}",
            $"Opciones no verificadas: {FormatOptions(capabilities.UnverifiedOptions, useSpanish: true)}"));
        lines.Add(Text(
            useSpanish,
            "Capabilities metadata is diagnostic only; unverified options are not added to the command.",
            "La metadata de capacidades es solo diagnóstica; las opciones no verificadas no se agregan al comando."));
    }

    private static string FormatOptions(
        IReadOnlyList<string> options,
        bool useSpanish)
    {
        if (options.Count == 0)
        {
            return "0 (-)";
        }

        var displayedOptions = options
            .Take(MaxDisplayedOptions)
            .ToArray();
        var remainingCount = options.Count - displayedOptions.Length;
        var suffix = remainingCount > 0
            ? useSpanish ? $", +{remainingCount} más" : $", +{remainingCount} more"
            : string.Empty;

        return $"{options.Count} ({string.Join(", ", displayedOptions)}{suffix})";
    }

    private static string StatusText(
        Iw3CliCapabilitiesStatus status,
        bool useSpanish) => status switch
    {
        Iw3CliCapabilitiesStatus.Missing => useSpanish ? "Faltante" : "Missing",
        Iw3CliCapabilitiesStatus.Invalid => useSpanish ? "Inválido" : "Invalid",
        Iw3CliCapabilitiesStatus.Placeholder => "Placeholder",
        Iw3CliCapabilitiesStatus.Found => useSpanish ? "Encontrado" : "Found",
        _ => status.ToString(),
    };

    private static string YesNo(bool value, bool useSpanish) =>
        value
            ? useSpanish ? "Sí" : "Yes"
            : "No";

    private static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

    private static string Text(
        bool useSpanish,
        string english,
        string spanish) => useSpanish ? spanish : english;
}
