using V3dfy.Core.Image;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Engine.Iw3.Execution;

public sealed class Iw3ImageStereoExporter : IImageStereoExporter
{
    private const string BlockedEnglishSummary =
        "Bundled iw3 image stereo export is not ready.";
    private const string BlockedSpanishSummary =
        "La exportacion estereoscopica de imagen con iw3 incluido no esta lista.";

    private readonly ILocalProcessRunner _processRunner;
    private readonly Iw3ImageStereoCommandBuilder _commandBuilder;

    public Iw3ImageStereoExporter(
        ILocalProcessRunner? processRunner = null,
        Iw3ImageStereoCommandBuilder? commandBuilder = null)
    {
        _processRunner = processRunner ?? new BundledLocalProcessRunner();
        _commandBuilder = commandBuilder ?? new Iw3ImageStereoCommandBuilder();
    }

    public async Task<ImageStereoExportResult> ExportAsync(
        ImageStereoExportRequest request,
        IProgress<ImageStereoExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var readiness = EvaluateReadiness(request);
        if (!readiness.CanExport)
        {
            return CreateBlockedResult(request, readiness);
        }

        var outputPaths = ImageStereoExportPathBuilder.CreateOutputPaths(
            request.SourcePath,
            request.OutputDirectory,
            request.OutputFormat,
            GetModelFileNameForOutput(request.SelectedLocalModel),
            File.Exists);
        var command = _commandBuilder.Build(
            request,
            outputPaths.PrimaryOutputPath,
            request.SelectedLocalModel!);
        var processRequest = new ProcessExecutionRequest(
            ExecutablePath: command.ExecutablePath,
            Arguments: command.Arguments,
            WorkingDirectory: request.ExpectedToolPaths.NunifRootDirectory,
            EnvironmentVariables: Iw3BundledRuntimeEnvironment.Create(request.ExpectedToolPaths),
            AllowedRootDirectory: request.ExpectedToolPaths.Iw3EngineDirectory);

        progress?.Report(new(
            0,
            "Starting bundled iw3 image stereo export.",
            "Iniciando exportacion estereoscopica de imagen con iw3 incluido."));
        progress?.Report(new(
            5,
            $"Bundled Python: {request.ExpectedToolPaths.PythonExecutable}",
            $"Python incluido: {request.ExpectedToolPaths.PythonExecutable}"));
        progress?.Report(new(
            10,
            $"iw3 command: {command.FullCommandPreview}",
            $"Comando iw3: {command.FullCommandPreview}"));

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            request.CancellationToken);
        var processResult = await _processRunner.RunAsync(
            processRequest,
            linkedCancellationTokenSource.Token);

        var success = processResult.Status == ProcessExecutionStatus.Completed &&
            processResult.ExitCode == 0 &&
            outputPaths.GeneratedFiles.All(File.Exists);
        var stdoutSummary = SummarizeProcessText(processResult.StandardOutput);
        var stderrSummary = SummarizeProcessText(processResult.StandardError);

        progress?.Report(new(
            success ? 100 : 0,
            success
                ? "Bundled iw3 image stereo export completed."
                : $"Bundled iw3 image stereo export failed with exit code {processResult.ExitCode}.",
            success
                ? "Exportacion estereoscopica de imagen con iw3 incluido completada."
                : $"La exportacion estereoscopica de imagen con iw3 incluido fallo con codigo {processResult.ExitCode}."));

        return new(
            Success: success,
            WasBlocked: false,
            EnglishSummary: success
                ? "Bundled iw3 image stereo export completed."
                : "Bundled iw3 image stereo export failed.",
            SpanishSummary: success
                ? "Exportacion estereoscopica de imagen con iw3 incluido completada."
                : "La exportacion estereoscopica de imagen con iw3 incluido fallo.",
            OutputDirectory: request.OutputDirectory,
            GeneratedFiles: success ? outputPaths.GeneratedFiles : [],
            PrimaryOutputPath: success ? outputPaths.PrimaryOutputPath : null,
            TechnicalDetail: success
                ? null
                : $"Process status: {processResult.Status}. stdout: {stdoutSummary}. stderr: {stderrSummary}",
            ExitCode: processResult.ExitCode,
            CommandPreview: command.FullCommandPreview,
            StandardOutputSummary: stdoutSummary,
            StandardErrorSummary: stderrSummary);
    }

    public static Iw3ImageStereoExportReadiness EvaluateReadiness(
        ImageStereoExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<Iw3ImageStereoExportReadinessIssue>();
        AddFileIssue(
            issues,
            request.SourcePath,
            "source image file",
            "archivo de imagen origen");
        AddDirectoryIssue(
            issues,
            request.OutputDirectory,
            "image output directory",
            "carpeta de salida de imagen");
        AddFileIssue(
            issues,
            request.ExpectedToolPaths.PythonExecutable,
            "embedded Python executable",
            "ejecutable Python incluido");
        AddDirectoryIssue(
            issues,
            request.ExpectedToolPaths.NunifRootDirectory,
            "nunif root directory",
            "carpeta raiz de nunif");
        AddFileIssue(
            issues,
            Path.Combine(request.ExpectedToolPaths.Iw3PackageDirectory, "__main__.py"),
            "iw3 package entry file",
            "archivo de entrada del paquete iw3");
        AddDirectoryIssue(
            issues,
            request.ExpectedToolPaths.ModelsDirectory,
            "iw3 pretrained models directory",
            "carpeta de modelos pretrained de iw3");
        AddFileIssue(
            issues,
            request.ExpectedToolPaths.Iw3DefaultStereoRuntimeDependencyFile,
            "iw3 stereo runtime dependency",
            "dependencia de runtime estereo iw3");

        if (request.SelectedLocalModel is null)
        {
            issues.Add(new(
                "Select a bundled local depth model before exporting.",
                "Selecciona un modelo local de profundidad incluido antes de exportar."));
        }
        else if (!Iw3DepthModelMapper.TryMap(request.SelectedLocalModel, out var mapping) ||
            mapping is null)
        {
            issues.Add(new(
                "Selected local model is not mapped to a verified iw3 depth model.",
                "El modelo local seleccionado no esta mapeado a un modelo de profundidad iw3 verificado."));
        }
        else
        {
            if (mapping.MediaCapability == Iw3DepthModelMediaCapability.VideoOnly)
            {
                issues.Add(new(
                    "Selected local model is not verified for image input.",
                    "El modelo local seleccionado no esta verificado para imagenes."));
            }

            var modelPath = ResolveModelPath(request.ExpectedToolPaths.ModelsDirectory, mapping.ModelRelativePath);
            if (modelPath is null || !File.Exists(modelPath))
            {
                issues.Add(new(
                    $"Selected bundled model file is missing: {mapping.ModelRelativePath}",
                    $"Falta el archivo del modelo incluido seleccionado: {mapping.ModelRelativePath}"));
            }
        }

        if (request.OutputFormat == ImageStereoExportFormat.Anaglyph &&
            !IsSupportedAnaglyphMode(request.AnaglyphMode))
        {
            issues.Add(new(
                "The selected anaglyph mode is not supported by the bundled iw3 exporter. Use Red/Cyan.",
                "El modo anaglifo seleccionado no es compatible con el exportador iw3 incluido. Usa rojo/cian."));
        }

        if (request.SwapEyes &&
            !HasVerifiedOption(request.DependencyHealth.Iw3CliCapabilities, Iw3CliContract.CrossEyedSwitch))
        {
            issues.Add(new(
                "Swap eyes requires the bundled iw3 --cross-eyed option to be verified.",
                "Intercambiar ojos requiere que la opcion iw3 incluida --cross-eyed este verificada."));
        }

        var missingImageCapabilityOptions = GetMissingImageCapabilityOptions(
            request.DependencyHealth.Iw3CliCapabilities,
            request.OutputFormat);
        if (missingImageCapabilityOptions.Count > 0)
        {
            issues.Add(new(
                "Bundled iw3 image export is blocked because IW3_CLI_CAPABILITIES.json does not verify " +
                $"{string.Join(", ", missingImageCapabilityOptions)}.",
                "La exportacion de imagen iw3 incluida esta bloqueada porque IW3_CLI_CAPABILITIES.json no verifica " +
                $"{string.Join(", ", missingImageCapabilityOptions)}."));
        }

        return new(issues.Count == 0, issues);
    }

    private static ImageStereoExportResult CreateBlockedResult(
        ImageStereoExportRequest request,
        Iw3ImageStereoExportReadiness readiness)
    {
        var englishDetail = string.Join(" ", readiness.Issues.Select(issue => issue.EnglishMessage));
        var spanishDetail = string.Join(" ", readiness.Issues.Select(issue => issue.SpanishMessage));
        return new(
            Success: false,
            WasBlocked: true,
            EnglishSummary: $"{BlockedEnglishSummary} {englishDetail}",
            SpanishSummary: $"{BlockedSpanishSummary} {spanishDetail}",
            OutputDirectory: request.OutputDirectory,
            GeneratedFiles: [],
            PrimaryOutputPath: null,
            TechnicalDetail: englishDetail);
    }

    public static bool HasVerifiedImageCapability(
        Iw3CliCapabilitiesManifest capabilities,
        ImageStereoExportFormat format) =>
        GetMissingImageCapabilityOptions(capabilities, format).Count == 0;

    public static IReadOnlyList<string> GetMissingImageCapabilityOptions(
        Iw3CliCapabilitiesManifest capabilities,
        ImageStereoExportFormat format)
    {
        if (format == ImageStereoExportFormat.LeftRightPair)
        {
            return ["a verified left/right still-image export switch"];
        }

        if (!capabilities.HasVerifiedCapabilities)
        {
            return ["verifiedBaseCommand=true"];
        }

        var requiredLayoutToken = Iw3ImageStereoCommandBuilder.GetLayoutCapabilityToken(format);
        var hasExplicitImageTokens =
            capabilities.VerifiedOptions.Contains("image:single-input", StringComparer.OrdinalIgnoreCase) &&
            capabilities.VerifiedOptions.Contains("image:output-file", StringComparer.OrdinalIgnoreCase) &&
            capabilities.VerifiedOptions.Contains(requiredLayoutToken, StringComparer.OrdinalIgnoreCase);
        if (hasExplicitImageTokens)
        {
            return [];
        }

        var requiredLayoutSwitch = GetRequiredLayoutSwitch(format);
        var missing = new List<string>();
        AddMissingOptionGroup(
            missing,
            capabilities,
            $"{Iw3CliContract.InputSwitch} or {Iw3CliContract.InputLongSwitch}",
            Iw3CliContract.InputSwitch,
            Iw3CliContract.InputLongSwitch);
        AddMissingOptionGroup(
            missing,
            capabilities,
            $"{Iw3CliContract.OutputSwitch} or {Iw3CliContract.OutputLongSwitch}",
            Iw3CliContract.OutputSwitch,
            Iw3CliContract.OutputLongSwitch);
        AddMissingOption(missing, capabilities, Iw3CliContract.DepthModelSwitch);
        AddMissingOptionGroup(missing, capabilities, $"{Iw3CliContract.DivergenceSwitch} or -d", Iw3CliContract.DivergenceSwitch, "-d");
        AddMissingOptionGroup(missing, capabilities, $"{Iw3CliContract.ConvergenceSwitch} or -c", Iw3CliContract.ConvergenceSwitch, "-c");
        AddMissingOption(missing, capabilities, requiredLayoutSwitch);

        return missing;
    }

    private static string GetRequiredLayoutSwitch(ImageStereoExportFormat format) => format switch
    {
        ImageStereoExportFormat.SideBySide => Iw3CliContract.HalfSideBySideSwitch,
        ImageStereoExportFormat.HalfTopBottom => Iw3CliContract.HalfTopBottomSwitch,
        ImageStereoExportFormat.Anaglyph => Iw3CliContract.AnaglyphSwitch,
        _ => string.Empty,
    };

    private static bool HasAnyVerifiedOption(
        Iw3CliCapabilitiesManifest capabilities,
        params string[] options) =>
        options.Any(option => HasVerifiedOption(capabilities, option));

    private static void AddMissingOption(
        ICollection<string> missing,
        Iw3CliCapabilitiesManifest capabilities,
        string option)
    {
        if (!HasVerifiedOption(capabilities, option))
        {
            missing.Add(option);
        }
    }

    private static void AddMissingOptionGroup(
        ICollection<string> missing,
        Iw3CliCapabilitiesManifest capabilities,
        string displayName,
        params string[] options)
    {
        if (!HasAnyVerifiedOption(capabilities, options))
        {
            missing.Add(displayName);
        }
    }

    private static bool HasVerifiedOption(
        Iw3CliCapabilitiesManifest capabilities,
        string option) =>
        capabilities.VerifiedOptions.Contains(option, StringComparer.OrdinalIgnoreCase);

    private static bool IsSupportedAnaglyphMode(string anaglyphMode) =>
        string.IsNullOrWhiteSpace(anaglyphMode) ||
        anaglyphMode.Equals("Red/Cyan", StringComparison.OrdinalIgnoreCase);

    private static string GetModelFileNameForOutput(LocalModelPlanSelection? selectedModel) =>
        string.IsNullOrWhiteSpace(selectedModel?.MappingKey)
            ? selectedModel?.DisplayName ?? "model"
            : selectedModel.MappingKey!;

    private static void AddFileIssue(
        ICollection<Iw3ImageStereoExportReadinessIssue> issues,
        string path,
        string englishLabel,
        string spanishLabel)
    {
        if (!File.Exists(path))
        {
            issues.Add(new(
                $"Missing bundled {englishLabel}: {path}",
                $"Falta {spanishLabel} incluido: {path}"));
        }
    }

    private static void AddDirectoryIssue(
        ICollection<Iw3ImageStereoExportReadinessIssue> issues,
        string path,
        string englishLabel,
        string spanishLabel)
    {
        if (!Directory.Exists(path))
        {
            issues.Add(new(
                $"Missing bundled {englishLabel}: {path}",
                $"Falta {spanishLabel} incluida: {path}"));
        }
    }

    private static string? ResolveModelPath(string modelsDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(modelsDirectory) ||
            string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Contains(".."))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(
            [modelsDirectory, .. relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)]));
        var root = Path.GetFullPath(modelsDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }

    private static string SummarizeProcessText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Take(8);
        return string.Join(" | ", lines);
    }
}

public sealed record Iw3ImageStereoExportReadiness(
    bool CanExport,
    IReadOnlyList<Iw3ImageStereoExportReadinessIssue> Issues);

public sealed record Iw3ImageStereoExportReadinessIssue(
    string EnglishMessage,
    string SpanishMessage);
