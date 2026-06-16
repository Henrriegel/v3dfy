using V3dfy.Core.Image;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Engine.Iw3.Execution;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Tests.Image;

public sealed class Iw3ImageStereoExporterTests
{
    [Fact]
    public void EvaluateReadiness_BlocksWhenBundledEngineFilesAreMissing()
    {
        var paths = TestPaths.InternalToolPaths(Guid.NewGuid().ToString("N"));
        var sourcePath = TestPaths.SourceRoot(Guid.NewGuid().ToString("N"), "source.png");
        var outputDirectory = Path.GetDirectoryName(sourcePath)!;
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(sourcePath, "not a real image; readiness only checks file presence");

        var request = CreateRequest(paths, sourcePath, outputDirectory, CreateCapabilities(paths));

        var readiness = Iw3ImageStereoExporter.EvaluateReadiness(request);

        Assert.False(readiness.CanExport);
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains("embedded Python executable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains("iw3 package entry file", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains("Selected bundled model file is missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateReadiness_BlocksWhenImageCliCapabilityIsNotVerified()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile();
        var capabilities = CreateCapabilities(paths, ["-i", "-o", "--depth-model"]);
        var request = CreateRequest(paths, sourcePath, Path.GetDirectoryName(sourcePath)!, capabilities);

        var readiness = Iw3ImageStereoExporter.EvaluateReadiness(request);

        Assert.False(readiness.CanExport);
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains("does not verify", StringComparison.OrdinalIgnoreCase) &&
            issue.EnglishMessage.Contains(Iw3CliContract.HalfSideBySideSwitch, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExportAsync_UsesBundledPythonIw3ModelAndSameFolderOutput()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile("fondo.png");
        var outputDirectory = Path.GetDirectoryName(sourcePath)!;
        var runner = new RecordingProcessRunner(createOutputFile: true);
        var exporter = new Iw3ImageStereoExporter(runner);
        var request = CreateRequest(paths, sourcePath, outputDirectory, CreateCapabilities(paths));

        var result = await exporter.ExportAsync(request);

        Assert.True(result.Success);
        Assert.Single(runner.Requests);
        var processRequest = runner.Requests[0];
        Assert.Equal(paths.PythonExecutable, processRequest.ExecutablePath);
        Assert.Equal(paths.NunifRootDirectory, processRequest.WorkingDirectory);
        Assert.Equal(paths.Iw3EngineDirectory, processRequest.AllowedRootDirectory);
        Assert.NotNull(processRequest.EnvironmentVariables);
        Assert.Equal(paths.NunifRootDirectory, processRequest.EnvironmentVariables["NUNIF_HOME"]);
        Assert.Equal("1", processRequest.EnvironmentVariables["PYTHONNOUSERSITE"]);
        Assert.Equal(paths.ModelsDirectory, processRequest.EnvironmentVariables["TORCH_HOME"]);
        Assert.Contains(Iw3CliContract.PythonModuleSwitch, processRequest.Arguments);
        Assert.Contains(Iw3CliContract.ModuleName, processRequest.Arguments);
        Assert.Contains(Iw3CliContract.InputSwitch, processRequest.Arguments);
        Assert.Contains(sourcePath, processRequest.Arguments);
        Assert.Contains(Iw3CliContract.OutputSwitch, processRequest.Arguments);
        Assert.DoesNotContain("--format", processRequest.Arguments);
        Assert.DoesNotContain("png", processRequest.Arguments);
        Assert.Contains(Iw3CliContract.DivergenceSwitch, processRequest.Arguments);
        Assert.Contains("1", processRequest.Arguments);
        Assert.Contains(Iw3CliContract.ConvergenceSwitch, processRequest.Arguments);
        Assert.Contains("0.5", processRequest.Arguments);
        Assert.Contains(Iw3CliContract.DepthModelSwitch, processRequest.Arguments);
        Assert.Contains(Iw3DepthModelMapper.ZoeDAnyNDepthModelName, processRequest.Arguments);
        Assert.Contains(Iw3CliContract.HalfSideBySideSwitch, processRequest.Arguments);
        Assert.Equal(outputDirectory, result.OutputDirectory);
        Assert.Equal(Path.Combine(outputDirectory, "fondo-depth-anything-metric-indoor-sbs.png"), result.PrimaryOutputPath);
        Assert.True(File.Exists(result.PrimaryOutputPath));
    }

    [Theory]
    [InlineData(ImageStereoExportFormat.SideBySide, "--half-sbs", "sbs")]
    [InlineData(ImageStereoExportFormat.HalfTopBottom, "--half-tb", "tab")]
    [InlineData(ImageStereoExportFormat.Anaglyph, "--anaglyph", "anaglyph")]
    public async Task ExportAsync_UsesSelectedStereoFormatSwitchAndFilenameSuffix(
        ImageStereoExportFormat format,
        string expectedSwitch,
        string expectedSuffix)
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile("fondo.png");
        var outputDirectory = Path.GetDirectoryName(sourcePath)!;
        var runner = new RecordingProcessRunner(createOutputFile: true);
        var exporter = new Iw3ImageStereoExporter(runner);
        var request = CreateRequest(
            paths,
            sourcePath,
            outputDirectory,
            CreateCapabilities(paths),
            format);

        var result = await exporter.ExportAsync(request);

        Assert.True(result.Success);
        var arguments = runner.Requests.Single().Arguments;
        Assert.Contains(expectedSwitch, arguments);
        if (!string.Equals(expectedSwitch, Iw3CliContract.HalfSideBySideSwitch, StringComparison.Ordinal))
        {
            Assert.DoesNotContain(Iw3CliContract.HalfSideBySideSwitch, arguments);
        }

        Assert.EndsWith($"-{expectedSuffix}.png", result.PrimaryOutputPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_BlocksMissingModelWithoutLaunchingIw3()
    {
        var paths = CreateReadyBundle(includeMappedModel: false);
        var sourcePath = CreateSourceImageFile();
        var runner = new RecordingProcessRunner(createOutputFile: false);
        var exporter = new Iw3ImageStereoExporter(runner);
        var request = CreateRequest(paths, sourcePath, Path.GetDirectoryName(sourcePath)!, CreateCapabilities(paths));

        var result = await exporter.ExportAsync(request);

        Assert.False(result.Success);
        Assert.True(result.WasBlocked);
        Assert.Empty(runner.Requests);
        Assert.Contains("Selected bundled model file is missing", result.EnglishSummary);
    }

    [Fact]
    public void EvaluateReadiness_BlocksUnsupportedLeftRightPair()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile();
        var request = CreateRequest(
            paths,
            sourcePath,
            Path.GetDirectoryName(sourcePath)!,
            CreateCapabilities(paths),
            ImageStereoExportFormat.LeftRightPair);

        var readiness = Iw3ImageStereoExporter.EvaluateReadiness(request);

        Assert.False(readiness.CanExport);
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains("left/right", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateReadiness_CandidateStyleManifestEnablesSbsWithoutImageTokensOrFormatSwitch()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile();
        var request = CreateRequest(
            paths,
            sourcePath,
            Path.GetDirectoryName(sourcePath)!,
            CreateCapabilities(paths));

        var readiness = Iw3ImageStereoExporter.EvaluateReadiness(request);

        Assert.True(readiness.CanExport);
        Assert.Empty(readiness.Issues);
    }

    [Fact]
    public void EvaluateReadiness_MissingExactSbsSwitchBlocksWithSpecificToken()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile();
        var verifiedOptions = CreateCandidateVerifiedOptions()
            .Where(option => !string.Equals(option, Iw3CliContract.HalfSideBySideSwitch, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var request = CreateRequest(
            paths,
            sourcePath,
            Path.GetDirectoryName(sourcePath)!,
            CreateCapabilities(paths, verifiedOptions));

        var readiness = Iw3ImageStereoExporter.EvaluateReadiness(request);

        Assert.False(readiness.CanExport);
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains(Iw3CliContract.HalfSideBySideSwitch, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateReadiness_MissingDepthModelSwitchBlocksWithSpecificToken()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile();
        var verifiedOptions = CreateCandidateVerifiedOptions()
            .Where(option => !string.Equals(option, Iw3CliContract.DepthModelSwitch, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var request = CreateRequest(
            paths,
            sourcePath,
            Path.GetDirectoryName(sourcePath)!,
            CreateCapabilities(paths, verifiedOptions));

        var readiness = Iw3ImageStereoExporter.EvaluateReadiness(request);

        Assert.False(readiness.CanExport);
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains(Iw3CliContract.DepthModelSwitch, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(ImageStereoExportFormat.HalfTopBottom, "--half-tb")]
    [InlineData(ImageStereoExportFormat.Anaglyph, "--anaglyph")]
    public void EvaluateReadiness_TabAndAnaglyphUseTheirOwnExactSwitches(
        ImageStereoExportFormat format,
        string requiredSwitch)
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile();
        var request = CreateRequest(
            paths,
            sourcePath,
            Path.GetDirectoryName(sourcePath)!,
            CreateCapabilities(paths),
            format);

        var readiness = Iw3ImageStereoExporter.EvaluateReadiness(request);

        Assert.True(readiness.CanExport);

        var missingFormatSwitchOptions = CreateCandidateVerifiedOptions()
            .Where(option => !string.Equals(option, requiredSwitch, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var blockedRequest = CreateRequest(
            paths,
            sourcePath,
            Path.GetDirectoryName(sourcePath)!,
            CreateCapabilities(paths, missingFormatSwitchOptions),
            format);

        var blockedReadiness = Iw3ImageStereoExporter.EvaluateReadiness(blockedRequest);

        Assert.False(blockedReadiness.CanExport);
        Assert.Contains(blockedReadiness.Issues, issue =>
            issue.EnglishMessage.Contains(requiredSwitch, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateReadiness_BlocksUnverifiedAnaglyphMode()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile();
        var request = CreateRequest(
            paths,
            sourcePath,
            Path.GetDirectoryName(sourcePath)!,
            CreateCapabilities(paths),
            ImageStereoExportFormat.Anaglyph,
            anaglyphMode: "Green/Magenta");

        var readiness = Iw3ImageStereoExporter.EvaluateReadiness(request);

        Assert.False(readiness.CanExport);
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains("Use Red/Cyan", StringComparison.OrdinalIgnoreCase));
    }

    private static InternalToolPaths CreateReadyBundle(bool includeMappedModel = true)
    {
        var paths = TestPaths.InternalToolPaths(Guid.NewGuid().ToString("N"));
        CreateFile(paths.PythonExecutable);
        Directory.CreateDirectory(paths.NunifRootDirectory);
        CreateFile(Path.Combine(paths.Iw3PackageDirectory, "__main__.py"));
        Directory.CreateDirectory(paths.ModelsDirectory);
        CreateFile(paths.Iw3DefaultStereoRuntimeDependencyFile);

        if (includeMappedModel)
        {
            CreateFile(Path.Combine(
                paths.ModelsDirectory,
                Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath));
        }

        return paths;
    }

    private static string CreateSourceImageFile(string fileName = "source.png")
    {
        var sourcePath = TestPaths.SourceRoot(Guid.NewGuid().ToString("N"), fileName);
        CreateFile(sourcePath);
        return sourcePath;
    }

    private static void CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
    }

    private static ImageStereoExportRequest CreateRequest(
        InternalToolPaths paths,
        string sourcePath,
        string outputDirectory,
        Iw3CliCapabilitiesManifest capabilities,
        ImageStereoExportFormat format = ImageStereoExportFormat.SideBySide,
        string anaglyphMode = "Red/Cyan") =>
        new(
            SourcePath: sourcePath,
            OutputDirectory: outputDirectory,
            OutputFormat: format,
            EyeSeparationPercent: 4d,
            Convergence: "Neutral",
            SwapEyes: false,
            AnaglyphMode: anaglyphMode,
            ExpectedToolPaths: paths,
            DependencyHealth: CreateHealth(paths, capabilities),
            SelectedLocalModel: CreateSelectedModel());

    private static EngineDependencyHealth CreateHealth(
        InternalToolPaths paths,
        Iw3CliCapabilitiesManifest capabilities) =>
        new(
            Ffmpeg: new(ToolHealthStatus.Found, ToolHealthDetailKind.BundledFileFound, paths.FfmpegExecutable),
            Ffprobe: new(ToolHealthStatus.Found, ToolHealthDetailKind.BundledFileFound, paths.FfprobeExecutable),
            Python: new(ToolHealthStatus.Found, ToolHealthDetailKind.BundledFileFound, paths.PythonExecutable),
            Iw3EngineDirectory: new(ToolHealthStatus.Found, ToolHealthDetailKind.EngineBundleFound, paths.Iw3EngineDirectory),
            ModelsDirectory: new(ToolHealthStatus.Found, ToolHealthDetailKind.ModelFilesFound, paths.ModelsDirectory),
            ModelInventory: LocalModelInventory.Empty(paths.ModelsDirectory),
            Iw3CliCapabilities: capabilities)
        {
            Iw3RuntimeDependencies = new(
                ToolHealthStatus.Found,
                ToolHealthDetailKind.Iw3RuntimeDependenciesFound,
                paths.Iw3DefaultStereoRuntimeDependencyFile),
        };

    private static LocalModelPlanSelection CreateSelectedModel() => new(
        DisplayName: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorEnglishName,
        RelativePath: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
        Source: LocalModelPlanSource.CatalogMetadata,
        FileName: "depth_anything_metric_depth_indoor.pt",
        Iw3DepthModelName: Iw3DepthModelMapper.ZoeDAnyNDepthModelName,
        MappingKey: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey);

    private static Iw3CliCapabilitiesManifest CreateCapabilities(
        InternalToolPaths paths,
        IReadOnlyList<string>? verifiedOptions = null) =>
        new(
            ManifestPath: paths.Iw3CliCapabilitiesFile,
            Status: Iw3CliCapabilitiesStatus.Found,
            ErrorMessage: null,
            BundledIw3Version: "test",
            VerifiedBaseCommand: true,
            VerifiedOptions: verifiedOptions ?? CreateCandidateVerifiedOptions(),
            UnverifiedOptions: [],
            VerificationSource: "test",
            VerifiedAtUtc: "2026-06-16T00:00:00Z",
            Notes: "Unit test manifest.");

    private static IReadOnlyList<string> CreateCandidateVerifiedOptions() =>
    [
        "-i",
        "--input",
        "-o",
        "--output",
        "--half-sbs",
        "--tb",
        "--half-tb",
        "--anaglyph",
        "--depth-model",
        "-d",
        "--divergence",
        "-c",
        "--convergence",
        "-vf",
        "--video-format",
        "-vc",
        "--video-codec",
        "--crf",
        "--preset",
        "--max-output-width",
        "--max-output-height",
        "--scene-detect",
        "--low-vram",
    ];

    private sealed class RecordingProcessRunner(bool createOutputFile) : ILocalProcessRunner
    {
        public List<ProcessExecutionRequest> Requests { get; } = [];

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            if (createOutputFile)
            {
                var arguments = request.Arguments.ToList();
                var outputIndex = arguments.IndexOf(Iw3CliContract.OutputSwitch);
                if (outputIndex >= 0 && outputIndex + 1 < arguments.Count)
                {
                    CreateFile(arguments[outputIndex + 1]);
                }
            }

            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new ProcessExecutionResult(
                ExitCode: 0,
                StandardOutput: "iw3 image export completed",
                StandardError: string.Empty,
                OutputLines: [],
                Status: ProcessExecutionStatus.Completed,
                StartedAt: now,
                EndedAt: now));
        }
    }
}
