namespace V3dfy.SetupHelper;

public static class PayloadInstallArgumentParser
{
    public static PayloadInstallArgumentParseResult Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (arg is "--help" or "-h" or "/?")
            {
                return PayloadInstallArgumentParseResult.ForHelp();
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                return PayloadInstallArgumentParseResult.ForError($"Unexpected argument: {arg}");
            }

            var name = arg[2..];
            if (IsFlag(name))
            {
                flags.Add(name);
                continue;
            }

            if (!IsValueArgument(name))
            {
                return PayloadInstallArgumentParseResult.ForError($"Unexpected argument: {arg}");
            }

            if (values.ContainsKey(name))
            {
                return PayloadInstallArgumentParseResult.ForError($"Duplicate argument: {arg}");
            }

            if (index + 1 >= args.Count)
            {
                return PayloadInstallArgumentParseResult.ForError($"Missing value for argument {arg}.");
            }

            values[name] = args[++index];
        }

        var modeText = GetRequired(values, "mode");
        var manifestPath = GetRequired(values, "manifest");
        var targetDirectory = GetRequired(values, "target-dir");
        var workDirectory = GetRequired(values, "work-dir");
        var firstError = new[] { modeText, manifestPath, targetDirectory, workDirectory }
            .FirstOrDefault(static value => value.StartsWith("ERROR:", StringComparison.Ordinal));
        if (firstError is not null)
        {
            return PayloadInstallArgumentParseResult.ForError(firstError["ERROR:".Length..]);
        }

        if (!Enum.TryParse<PayloadInstallMode>(modeText, ignoreCase: true, out var mode))
        {
            return PayloadInstallArgumentParseResult.ForError("Argument --mode must be web or offline.");
        }

        if (mode == PayloadInstallMode.Offline && !values.ContainsKey("parts-dir"))
        {
            return PayloadInstallArgumentParseResult.ForError("Offline mode requires --parts-dir.");
        }

        var partsDirectory = values.GetValueOrDefault("parts-dir");
        var modelPacksManifestPath = values.GetValueOrDefault("model-packs-manifest");
        var modelPacksSourceDirectory = values.GetValueOrDefault("model-packs-source-dir");
        if (mode == PayloadInstallMode.Offline &&
            !string.IsNullOrWhiteSpace(modelPacksManifestPath) &&
            string.IsNullOrWhiteSpace(modelPacksSourceDirectory))
        {
            modelPacksSourceDirectory = partsDirectory;
        }

        var options = new PayloadInstallOptions
        {
            Mode = mode,
            ManifestPath = manifestPath,
            TargetDirectory = targetDirectory,
            WorkDirectory = workDirectory,
            PartsDirectory = partsDirectory,
            ReleaseBaseUrlOverride = values.GetValueOrDefault("release-base-url"),
            ModelPacksManifestPath = modelPacksManifestPath,
            ModelPacksSourceDirectory = modelPacksSourceDirectory,
            KeepWorkDirectory = flags.Contains("keep-work"),
            AllowTargetReplacement = flags.Contains("replace-existing"),
        };

        return PayloadInstallArgumentParseResult.ForOptions(
            options,
            values.GetValueOrDefault("log"),
            flags.Contains("ui"));
    }

    private static bool IsFlag(string name) =>
        string.Equals(name, "keep-work", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "replace-existing", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "ui", StringComparison.OrdinalIgnoreCase);

    private static bool IsValueArgument(string name) =>
        string.Equals(name, "mode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "manifest", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "target-dir", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "work-dir", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "parts-dir", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "release-base-url", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "log", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "model-packs-manifest", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "model-packs-source-dir", StringComparison.OrdinalIgnoreCase);

    private static string GetRequired(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : $"ERROR:Missing required argument --{name}.";
}

public sealed class PayloadInstallArgumentParseResult
{
    private PayloadInstallArgumentParseResult()
    {
    }

    public PayloadInstallOptions? Options { get; init; }

    public string? LogPath { get; init; }

    public string? Error { get; init; }

    public bool HelpRequested { get; init; }

    public bool UiRequested { get; init; }

    public static PayloadInstallArgumentParseResult ForHelp() => new() { HelpRequested = true };

    public static PayloadInstallArgumentParseResult ForError(string error) => new() { Error = error };

    public static PayloadInstallArgumentParseResult ForOptions(
        PayloadInstallOptions options,
        string? logPath,
        bool uiRequested) =>
        new()
        {
            Options = options,
            LogPath = logPath,
            UiRequested = uiRequested,
        };
}
