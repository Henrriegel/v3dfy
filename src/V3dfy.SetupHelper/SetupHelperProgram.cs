using System.Text.Json;
using V3dfy.Core.Models;
using V3dfy.Infrastructure.ModelPacks;

namespace V3dfy.SetupHelper;

public static class SetupHelperProgram
{
    private static readonly JsonSerializerOptions ModelPackResultJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken,
        ModelPackInstallExecutor? modelPackInstallExecutor = null)
    {
        if (args.Length > 0 &&
            string.Equals(args[0], ModelPackHelperContract.AreaArgument, StringComparison.OrdinalIgnoreCase))
        {
            return await RunModelPackCommandAsync(
                args,
                cancellationToken,
                modelPackInstallExecutor);
        }

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

    private static async Task<int> RunModelPackCommandAsync(
        string[] args,
        CancellationToken cancellationToken,
        ModelPackInstallExecutor? modelPackInstallExecutor)
    {
        var parsed = ParseModelPackInstallArguments(args);
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

        var options = parsed.Options!;
        ModelPackInstallResult result;
        try
        {
            result = await (modelPackInstallExecutor ?? new ModelPackInstallExecutor()).InstallAsync(
                new ModelPackInstallRequest(
                    options.ZipPath,
                    options.TargetPretrainedModelsRoot,
                    options.StagingRoot,
                    options.CurrentIw3Version,
                    options.CurrentV3dfyVersion),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result = CreateFailedModelPackResult(
                options,
                "Model pack installation was cancelled.");
            await WriteModelPackResultAsync(
                result,
                options.ResultPath,
                options.LogPath,
                CancellationToken.None);
            return 3;
        }
        catch (Exception ex)
        {
            result = CreateFailedModelPackResult(
                options,
                $"Unexpected model-pack helper failure: {ex.Message}");
        }

        await WriteModelPackResultAsync(
            result,
            options.ResultPath,
            options.LogPath,
            cancellationToken);

        return result.Success ? 0 : 1;
    }

    private static ModelPackInstallResult CreateFailedModelPackResult(
        ModelPackHelperInstallCommand options,
        string error) => new(
        Success: false,
        Manifest: null,
        ModelPackZipPath: options.ZipPath,
        TargetPretrainedModelsRoot: options.TargetPretrainedModelsRoot,
        StagingPath: null,
        InstalledFiles: [],
        AlreadyInstalledFiles: [],
        SkippedFiles: [],
        RollbackFilesRemoved: [],
        Errors: [error],
        Warnings: []);

    private static async Task WriteModelPackResultAsync(
        ModelPackInstallResult result,
        string? resultPath,
        string? logPath,
        CancellationToken cancellationToken)
    {
        var outputPaths = new[] { resultPath, logPath }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var outputPath in outputPaths)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Open(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);
            await JsonSerializer.SerializeAsync(
                stream,
                result,
                ModelPackResultJsonOptions,
                cancellationToken);
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

    private static ParsedModelPackArguments ParseModelPackInstallArguments(string[] args)
    {
        if (args.Any(static arg => arg is "--help" or "-h" or "/?"))
        {
            return ParsedModelPackArguments.ForHelp();
        }

        if (!ModelPackHelperContract.IsModelPackInstallCommand(args))
        {
            return ParsedModelPackArguments.ForError(
                "Model-pack helper usage is: model-pack install.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 2; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                return ParsedModelPackArguments.ForError($"Unexpected argument: {arg}");
            }

            var name = arg[2..];
            if (!IsModelPackInstallSwitch(name))
            {
                return ParsedModelPackArguments.ForError($"Unexpected model-pack argument: {arg}");
            }

            if (values.ContainsKey(name))
            {
                return ParsedModelPackArguments.ForError($"Duplicate argument: {arg}");
            }

            if (index + 1 >= args.Length)
            {
                return ParsedModelPackArguments.ForError($"Missing value for argument {arg}.");
            }

            values[name] = args[++index];
        }

        var zipPath = GetRequired(values, ModelPackHelperContract.ZipSwitch[2..]);
        var targetRoot = GetRequired(values, ModelPackHelperContract.TargetRootSwitch[2..]);
        var stagingRoot = GetRequired(values, ModelPackHelperContract.StagingRootSwitch[2..]);
        var currentIw3Version = GetRequired(values, ModelPackHelperContract.CurrentIw3VersionSwitch[2..]);
        var firstError = new[] { zipPath, targetRoot, stagingRoot, currentIw3Version }
            .FirstOrDefault(static value => value.StartsWith("ERROR:", StringComparison.Ordinal));
        if (firstError is not null)
        {
            return ParsedModelPackArguments.ForError(firstError["ERROR:".Length..]);
        }

        return ParsedModelPackArguments.ForOptions(new ModelPackHelperInstallCommand(
            zipPath,
            targetRoot,
            stagingRoot,
            currentIw3Version,
            CurrentV3dfyVersion: values.GetValueOrDefault(
                ModelPackHelperContract.CurrentV3dfyVersionSwitch[2..]),
            ResultPath: values.GetValueOrDefault(ModelPackHelperContract.ResultSwitch[2..]),
            LogPath: values.GetValueOrDefault(ModelPackHelperContract.LogSwitch[2..])));
    }

    private static bool IsModelPackInstallSwitch(string name) =>
        string.Equals(name, ModelPackHelperContract.ZipSwitch[2..], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, ModelPackHelperContract.TargetRootSwitch[2..], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, ModelPackHelperContract.StagingRootSwitch[2..], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, ModelPackHelperContract.CurrentIw3VersionSwitch[2..], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, ModelPackHelperContract.CurrentV3dfyVersionSwitch[2..], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, ModelPackHelperContract.ResultSwitch[2..], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, ModelPackHelperContract.LogSwitch[2..], StringComparison.OrdinalIgnoreCase);

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

    private sealed class ParsedModelPackArguments
    {
        private ParsedModelPackArguments()
        {
        }

        public ModelPackHelperInstallCommand? Options { get; init; }

        public string? Error { get; init; }

        public bool HelpRequested { get; init; }

        public static ParsedModelPackArguments ForHelp() => new() { HelpRequested = true };

        public static ParsedModelPackArguments ForError(string error) => new() { Error = error };

        public static ParsedModelPackArguments ForOptions(ModelPackHelperInstallCommand options) =>
            new() { Options = options };
    }

    private const string UsageText = """
Usage:
  V3dfy.SetupHelper.exe --mode web --manifest payload-manifest.json --target-dir "%ProgramFiles%\v3dfy" --work-dir "%TEMP%\v3dfy-setup"
  V3dfy.SetupHelper.exe --mode offline --manifest payload-manifest.json --target-dir "%ProgramFiles%\v3dfy" --work-dir "%TEMP%\v3dfy-setup" --parts-dir "."
  V3dfy.SetupHelper.exe model-pack install --zip model-pack.zip --target-root "%ProgramFiles%\v3dfy\engine\iw3\nunif\iw3\pretrained_models" --staging-root "%TEMP%\v3dfy-model-pack" --current-iw3-version nunif-d23721f1 --result result.json

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

Model pack options:
  model-pack install        Install a validated v3dfy model pack.
  --zip PATH                Model pack ZIP.
  --target-root PATH        iw3 pretrained_models root.
  --staging-root PATH       Temporary staging root.
  --current-iw3-version ID  Bundled iw3 version.
  --result PATH             Optional JSON result file.
  --log PATH                Optional JSON result log file.
""";
}
