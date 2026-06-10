namespace V3dfy.SetupHelper;

public static class SetupHelperProgram
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var parsed = ParseArguments(args);
        if (parsed.HelpRequested)
        {
            Console.WriteLine(UsageText);
            return 0;
        }

        if (parsed.Error is not null)
        {
            Console.Error.WriteLine(parsed.Error);
            Console.Error.WriteLine(UsageText);
            return 2;
        }

        if (parsed.UiRequested)
        {
            return SetupHelperUiRunner.Run(parsed.Options!, parsed.LogPath, cancellationToken);
        }

        using var log = new SetupLog(parsed.LogPath);
        try
        {
            await new PayloadInstaller().InstallAsync(
                parsed.Options!,
                log,
                cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            log.Error("Installation was cancelled.");
            return 3;
        }
        catch (PayloadInstallException ex)
        {
            log.Error(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            log.Error($"Unexpected setup helper failure: {ex.Message}");
            return 1;
        }
    }

    private static ParsedArguments ParseArguments(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg is "--help" or "-h" or "/?")
            {
                return ParsedArguments.ForHelp();
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                return ParsedArguments.ForError($"Unexpected argument: {arg}");
            }

            var name = arg[2..];
            if (string.Equals(name, "keep-work", StringComparison.OrdinalIgnoreCase))
            {
                flags.Add(name);
                continue;
            }

            if (string.Equals(name, "ui", StringComparison.OrdinalIgnoreCase))
            {
                flags.Add(name);
                continue;
            }

            if (index + 1 >= args.Length)
            {
                return ParsedArguments.ForError($"Missing value for argument {arg}.");
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
            return ParsedArguments.ForError(firstError["ERROR:".Length..]);
        }

        if (!Enum.TryParse<PayloadInstallMode>(modeText, ignoreCase: true, out var mode))
        {
            return ParsedArguments.ForError("Argument --mode must be web or offline.");
        }

        if (mode == PayloadInstallMode.Offline && !values.ContainsKey("parts-dir"))
        {
            return ParsedArguments.ForError("Offline mode requires --parts-dir.");
        }

        var options = new PayloadInstallOptions
        {
            Mode = mode,
            ManifestPath = manifestPath,
            TargetDirectory = targetDirectory,
            WorkDirectory = workDirectory,
            PartsDirectory = values.GetValueOrDefault("parts-dir"),
            ReleaseBaseUrlOverride = values.GetValueOrDefault("release-base-url"),
            KeepWorkDirectory = flags.Contains("keep-work"),
        };

        return ParsedArguments.ForOptions(options, values.GetValueOrDefault("log"), flags.Contains("ui"));
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : $"ERROR:Missing required argument --{name}.";

    private sealed class ParsedArguments
    {
        private ParsedArguments()
        {
        }

        public PayloadInstallOptions? Options { get; init; }

        public string? LogPath { get; init; }

        public string? Error { get; init; }

        public bool HelpRequested { get; init; }

        public bool UiRequested { get; init; }

        public static ParsedArguments ForHelp() => new() { HelpRequested = true };

        public static ParsedArguments ForError(string error) => new() { Error = error };

        public static ParsedArguments ForOptions(PayloadInstallOptions options, string? logPath, bool uiRequested) =>
            new() { Options = options, LogPath = logPath, UiRequested = uiRequested };
    }

    private const string UsageText = """
Usage:
  V3dfy.SetupHelper.exe --mode web --manifest payload-manifest.json --target-dir "%ProgramFiles%\v3dfy" --work-dir "%TEMP%\v3dfy-setup"
  V3dfy.SetupHelper.exe --mode offline --manifest payload-manifest.json --target-dir "%ProgramFiles%\v3dfy" --work-dir "%TEMP%\v3dfy-setup" --parts-dir "."

Options:
  --mode web|offline        Select web download or same-folder offline parts.
  --manifest PATH           Payload manifest JSON.
  --target-dir PATH         Final install directory.
  --work-dir PATH           Temporary download/rebuild directory.
  --parts-dir PATH          Directory containing .part files for offline mode.
  --release-base-url URL    Optional web download base URL override.
  --log PATH                Optional helper log file.
  --keep-work               Keep temporary files for diagnostics.
  --ui                      Show the classic setup progress and log window.
""";
}
