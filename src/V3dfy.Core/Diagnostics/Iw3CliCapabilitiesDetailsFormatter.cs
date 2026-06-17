using V3dfy.Core.Localization;
using V3dfy.Core.Models;

namespace V3dfy.Core.Diagnostics;

public static class Iw3CliCapabilitiesDetailsFormatter
{
    private const int MaxDisplayedOptions = 8;

    public static IReadOnlyList<string> CreateLines(
        Iw3CliCapabilitiesManifest capabilities,
        LocalizedTextProvider localize)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(localize);

        var lines = new List<string>
        {
            localize(LocalizationKeys.TechnicalDetailsIw3CapabilitiesTitle),
            localize(
                LocalizationKeys.TechnicalDetailsIw3CapabilitiesManifestPathFormat,
                ("path", capabilities.ManifestPath)),
            localize(
                LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusFormat,
                ("status", StatusText(capabilities.Status, localize))),
        };

        switch (capabilities.Status)
        {
            case Iw3CliCapabilitiesStatus.Missing:
                lines.Add(localize(LocalizationKeys.TechnicalDetailsIw3CapabilitiesMissing));
                break;
            case Iw3CliCapabilitiesStatus.Invalid:
                lines.Add(localize(
                    LocalizationKeys.TechnicalDetailsIw3CapabilitiesInvalidFormat,
                    ("message", ValueOrDash(capabilities.ErrorMessage))));
                break;
            case Iw3CliCapabilitiesStatus.Placeholder:
                lines.Add(localize(LocalizationKeys.TechnicalDetailsIw3CapabilitiesPlaceholder));
                break;
            case Iw3CliCapabilitiesStatus.Found:
                AddFoundCapabilitiesLines(lines, capabilities, localize);
                break;
        }

        return lines;
    }

    private static void AddFoundCapabilitiesLines(
        ICollection<string> lines,
        Iw3CliCapabilitiesManifest capabilities,
        LocalizedTextProvider localize)
    {
        lines.Add(localize(
            LocalizationKeys.TechnicalDetailsIw3CapabilitiesBundledVersionFormat,
            ("version", ValueOrDash(capabilities.BundledIw3Version))));
        lines.Add(localize(
            LocalizationKeys.TechnicalDetailsIw3CapabilitiesBaseCommandVerifiedFormat,
            ("value", YesNo(capabilities.VerifiedBaseCommand, localize))));
        lines.Add(localize(
            LocalizationKeys.TechnicalDetailsIw3CapabilitiesVerificationSourceFormat,
            ("source", ValueOrDash(capabilities.VerificationSource))));
        lines.Add(localize(
            LocalizationKeys.TechnicalDetailsIw3CapabilitiesVerifiedAtUtcFormat,
            ("timestamp", ValueOrDash(capabilities.VerifiedAtUtc))));
        lines.Add(localize(
            LocalizationKeys.TechnicalDetailsIw3CapabilitiesVerifiedOptionsFormat,
            ("options", FormatOptions(capabilities.VerifiedOptions, localize))));
        lines.Add(localize(
            LocalizationKeys.TechnicalDetailsIw3CapabilitiesUnverifiedOptionsFormat,
            ("options", FormatOptions(capabilities.UnverifiedOptions, localize))));
        lines.Add(localize(LocalizationKeys.TechnicalDetailsIw3CapabilitiesDiagnosticOnly));
    }

    private static string FormatOptions(
        IReadOnlyList<string> options,
        LocalizedTextProvider localize)
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
            ? ", " + localize(
                LocalizationKeys.TechnicalDetailsIw3CapabilitiesOptionsMoreFormat,
                ("count", remainingCount))
            : string.Empty;

        return $"{options.Count} ({string.Join(", ", displayedOptions)}{suffix})";
    }

    private static string StatusText(
        Iw3CliCapabilitiesStatus status,
        LocalizedTextProvider localize) => status switch
    {
        Iw3CliCapabilitiesStatus.Missing => localize(LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusMissing),
        Iw3CliCapabilitiesStatus.Invalid => localize(LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusInvalid),
        Iw3CliCapabilitiesStatus.Placeholder => localize(LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusPlaceholder),
        Iw3CliCapabilitiesStatus.Found => localize(LocalizationKeys.TechnicalDetailsIw3CapabilitiesStatusFound),
        _ => status.ToString(),
    };

    private static string YesNo(bool value, LocalizedTextProvider localize) =>
        value
            ? localize(LocalizationKeys.CommonYes)
            : localize(LocalizationKeys.CommonNo);

    private static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;
}
