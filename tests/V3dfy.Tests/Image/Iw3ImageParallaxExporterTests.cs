using System.Globalization;
using V3dfy.Core.Image;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Engine.Iw3.Execution;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Tests.Image;

public sealed class Iw3ImageParallaxExporterTests
{
    [Fact]
    public void EvaluateReadiness_BlocksWhenBundledDepthCapabilityIsMissing()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile();
        var verifiedOptions = CreateParallaxVerifiedOptions()
            .Where(option => !string.Equals(option, Iw3CliContract.ExportDepthOnlySwitch, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var request = CreateRequest(
            paths,
            sourcePath,
            Path.GetDirectoryName(sourcePath)!,
            CreateCapabilities(paths, verifiedOptions));

        var readiness = Iw3ImageParallaxExporter.EvaluateReadiness(request);

        Assert.False(readiness.CanExport);
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains(Iw3CliContract.ExportDepthOnlySwitch, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateReadiness_BlocksMissingBundledHelperAndModel()
    {
        var paths = CreateReadyBundle(includeMappedModel: false, includeHelper: false);
        var sourcePath = CreateSourceImageFile();
        var request = CreateRequest(
            paths,
            sourcePath,
            Path.GetDirectoryName(sourcePath)!,
            CreateCapabilities(paths));

        var readiness = Iw3ImageParallaxExporter.EvaluateReadiness(request);

        Assert.False(readiness.CanExport);
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains("v3dfy parallax helper script", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(readiness.Issues, issue =>
            issue.EnglishMessage.Contains("Selected bundled model file is missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExportAsync_UsesBundledIw3DepthHelperAndFfmpeg()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile("fondo.png");
        var outputDirectory = Path.GetDirectoryName(sourcePath)!;
        var runner = new RecordingParallaxProcessRunner();
        var exporter = new Iw3ImageParallaxExporter(runner);
        var request = CreateRequest(paths, sourcePath, outputDirectory, CreateCapabilities(paths));

        var result = await exporter.ExportAsync(request);

        Assert.True(result.Success);
        Assert.Equal(3, runner.Requests.Count);

        var depthRequest = runner.Requests[0];
        Assert.Equal(paths.PythonExecutable, depthRequest.ExecutablePath);
        Assert.Equal(paths.NunifRootDirectory, depthRequest.WorkingDirectory);
        Assert.Equal(paths.Iw3EngineDirectory, depthRequest.AllowedRootDirectory);
        Assert.NotNull(depthRequest.EnvironmentVariables);
        Assert.Equal(paths.NunifRootDirectory, depthRequest.EnvironmentVariables["NUNIF_HOME"]);
        Assert.Contains(Iw3CliContract.PythonModuleSwitch, depthRequest.Arguments);
        Assert.Contains(Iw3CliContract.ModuleName, depthRequest.Arguments);
        Assert.Contains(Iw3CliContract.ExportSwitch, depthRequest.Arguments);
        Assert.Contains(Iw3CliContract.ExportDepthOnlySwitch, depthRequest.Arguments);
        Assert.Contains(Iw3CliContract.ExportDepthFitSwitch, depthRequest.Arguments);
        Assert.Contains(Iw3CliContract.DepthModelSwitch, depthRequest.Arguments);
        Assert.Contains(Iw3DepthModelMapper.ZoeDAnyNDepthModelName, depthRequest.Arguments);

        var frameRequest = runner.Requests[1];
        Assert.Equal(paths.PythonExecutable, frameRequest.ExecutablePath);
        Assert.Equal(paths.Iw3EngineDirectory, frameRequest.AllowedRootDirectory);
        Assert.Contains(paths.V3dfyParallaxHelperScript, frameRequest.Arguments);
        Assert.Contains("--source", frameRequest.Arguments);
        Assert.Contains(sourcePath, frameRequest.Arguments);
        Assert.Contains("--depth", frameRequest.Arguments);
        Assert.Contains("--frames-dir", frameRequest.Arguments);
        Assert.Contains("--intensity", frameRequest.Arguments);
        Assert.Contains("Medium", frameRequest.Arguments);

        var ffmpegRequest = runner.Requests[2];
        Assert.Equal(paths.FfmpegExecutable, ffmpegRequest.ExecutablePath);
        Assert.Equal(Path.GetDirectoryName(paths.FfmpegExecutable), ffmpegRequest.AllowedRootDirectory);
        Assert.Contains("-framerate", ffmpegRequest.Arguments);
        Assert.Contains("-c:v", ffmpegRequest.Arguments);
        Assert.Contains("libx264", ffmpegRequest.Arguments);

        Assert.Equal(outputDirectory, result.OutputDirectory);
        Assert.Equal(
            Path.Combine(outputDirectory, "fondo-depth-anything-metric-indoor-parallax-2d-6-seconds.mp4"),
            result.PrimaryOutputPath);
        Assert.True(File.Exists(result.PrimaryOutputPath));
    }

    [Fact]
    public async Task ExportAsync_ReportsLiveParallaxFrameCountProgress()
    {
        var paths = CreateReadyBundle();
        var sourcePath = CreateSourceImageFile("fondo.png");
        var outputDirectory = Path.GetDirectoryName(sourcePath)!;
        var runner = new DelayedFrameProgressParallaxProcessRunner();
        var exporter = new Iw3ImageParallaxExporter(runner);
        var request = CreateRequest(paths, sourcePath, outputDirectory, CreateCapabilities(paths)) with
        {
            Duration = "1 second",
        };
        var progress = new RecordingParallaxProgress();

        var result = await exporter.ExportAsync(request, progress);

        Assert.True(result.Success);
        var frameProgressMessages = progress.Messages
            .Where(message => message.EnglishMessage.StartsWith("Parallax frames:", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(frameProgressMessages);
        Assert.Contains(frameProgressMessages, message =>
            message.EnglishMessage.Contains(" / 24", StringComparison.Ordinal));
        Assert.Contains(frameProgressMessages, message =>
            message.EnglishMessage.Contains("24 / 24", StringComparison.Ordinal));
        Assert.True(frameProgressMessages.Length <= 4);
    }

    [Fact]
    public async Task ExportAsync_BlocksMissingModelWithoutLaunchingBundledProcesses()
    {
        var paths = CreateReadyBundle(includeMappedModel: false);
        var sourcePath = CreateSourceImageFile();
        var runner = new RecordingParallaxProcessRunner();
        var exporter = new Iw3ImageParallaxExporter(runner);
        var request = CreateRequest(paths, sourcePath, Path.GetDirectoryName(sourcePath)!, CreateCapabilities(paths));

        var result = await exporter.ExportAsync(request);

        Assert.False(result.Success);
        Assert.True(result.WasBlocked);
        Assert.Empty(runner.Requests);
        Assert.Contains("Selected bundled model file is missing", result.EnglishSummary);
    }

    [Fact]
    public void PathBuilder_UsesSameFolderSourceModelParallaxNameAndCollisionSuffix()
    {
        var outputDirectory = TestPaths.OutputRoot(Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        var sourcePath = Path.Combine(outputDirectory, "fondo raro.png");
        CreateFile(sourcePath);
        CreateFile(Path.Combine(outputDirectory, "fondo-raro-depth-anything-metric-indoor-parallax-2d-6-seconds.mp4"));

        var outputPaths = ImageParallaxExportPathBuilder.CreateOutputPaths(
            sourcePath,
            outputDirectory,
            "depth-anything-metric-indoor",
            "6 seconds",
            File.Exists);

        Assert.Equal(
            Path.Combine(outputDirectory, "fondo-raro-depth-anything-metric-indoor-parallax-2d-6-seconds-2.mp4"),
            outputPaths.PrimaryOutputPath);
    }

    [Fact]
    public void ExporterSource_DoesNotUseExternalPythonPipDownloadsOrDevIntakeFolders()
    {
        var exporterSource = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Execution", "Iw3ImageParallaxExporter.cs");
        var commandSource = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Commands", "Iw3ImageDepthExportCommandBuilder.cs");
        var helperSource = ReadRepoFile("engine", "iw3", "v3dfy", "parallax2d.py");

        Assert.Contains("request.ExpectedToolPaths.PythonExecutable", exporterSource);
        Assert.Contains("request.ExpectedToolPaths.FfmpegExecutable", exporterSource);
        Assert.Contains("Iw3BundledRuntimeEnvironment.Create(request.ExpectedToolPaths)", exporterSource);
        Assert.Contains("AllowedRootDirectory: request.ExpectedToolPaths.Iw3EngineDirectory", exporterSource);
        Assert.Contains("request.ExpectedToolPaths.PythonExecutable", commandSource);
        Assert.DoesNotContain("C:\\v3dfy-iw3-intake", exporterSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\v3dfy-iw3-intake", commandSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pip", exporterSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("download", exporterSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subprocess", helperSource, StringComparison.OrdinalIgnoreCase);
    }

    private static InternalToolPaths CreateReadyBundle(
        bool includeMappedModel = true,
        bool includeHelper = true)
    {
        var paths = TestPaths.InternalToolPaths(Guid.NewGuid().ToString("N"));
        CreateFile(paths.PythonExecutable);
        CreateFile(paths.FfmpegExecutable);
        Directory.CreateDirectory(paths.NunifRootDirectory);
        CreateFile(Path.Combine(paths.Iw3PackageDirectory, "__main__.py"));
        Directory.CreateDirectory(paths.ModelsDirectory);
        CreateFile(paths.Iw3DefaultStereoRuntimeDependencyFile);
        if (includeHelper)
        {
            CreateFile(paths.V3dfyParallaxHelperScript);
        }

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

    private static ImageParallaxExportRequest CreateRequest(
        InternalToolPaths paths,
        string sourcePath,
        string outputDirectory,
        Iw3CliCapabilitiesManifest capabilities) =>
        new(
            SourcePath: sourcePath,
            OutputDirectory: outputDirectory,
            DepthIntensity: "Medium",
            MotionDirection: "Left to right",
            ZoomAmplitude: "Subtle",
            Duration: "6 seconds",
            Smoothing: "Enabled",
            LayerBehavior: "Foreground / mid / background",
            FramesPerSecond: 24,
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
            VerifiedOptions: verifiedOptions ?? CreateParallaxVerifiedOptions(),
            UnverifiedOptions: [],
            VerificationSource: "test",
            VerifiedAtUtc: "2026-06-16T00:00:00Z",
            Notes: "Unit test manifest.");

    private static IReadOnlyList<string> CreateParallaxVerifiedOptions() =>
    [
        "-i",
        "--input",
        "-o",
        "--output",
        "--depth-model",
        "--export",
        "--export-depth-only",
        "--export-depth-fit",
    ];

    private static string ReadRepoFile(params string[] relativeSegments)
    {
        var repoRoot = FindRepoRoot();
        return File.ReadAllText(Path.Combine([repoRoot, .. relativeSegments]));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class RecordingParallaxProcessRunner : ILocalProcessRunner
    {
        public List<ProcessExecutionRequest> Requests { get; } = [];

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var arguments = request.Arguments.ToList();

            if (arguments.Contains(Iw3CliContract.ExportDepthOnlySwitch, StringComparer.OrdinalIgnoreCase))
            {
                var outputDirectory = GetArgumentAfter(arguments, Iw3CliContract.OutputSwitch);
                var sourcePath = GetArgumentAfter(arguments, Iw3CliContract.InputSwitch);
                CreateFile(Path.Combine(
                    outputDirectory,
                    "depth",
                    Path.GetFileNameWithoutExtension(sourcePath) + ".png"));
            }
            else if (arguments.Contains("--frames-dir", StringComparer.OrdinalIgnoreCase))
            {
                var framesDirectory = GetArgumentAfter(arguments, "--frames-dir");
                CreateFile(Path.Combine(framesDirectory, "frame_00000.png"));
            }
            else
            {
                CreateFile(arguments[^1]);
            }

            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new ProcessExecutionResult(
                ExitCode: 0,
                StandardOutput: "parallax step completed",
                StandardError: string.Empty,
                OutputLines: [],
                Status: ProcessExecutionStatus.Completed,
                StartedAt: now,
                EndedAt: now));
        }

        private static string GetArgumentAfter(IReadOnlyList<string> arguments, string switchName)
        {
            var index = arguments.ToList().FindIndex(argument =>
                string.Equals(argument, switchName, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < arguments.Count
                ? arguments[index + 1]
                : throw new InvalidOperationException($"Missing argument after {switchName}.");
        }
    }

    private sealed class DelayedFrameProgressParallaxProcessRunner : ILocalProcessRunner
    {
        public async Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            var arguments = request.Arguments.ToList();

            if (arguments.Contains(Iw3CliContract.ExportDepthOnlySwitch, StringComparer.OrdinalIgnoreCase))
            {
                var outputDirectory = GetArgumentAfter(arguments, Iw3CliContract.OutputSwitch);
                var sourcePath = GetArgumentAfter(arguments, Iw3CliContract.InputSwitch);
                CreateFile(Path.Combine(
                    outputDirectory,
                    "depth",
                    Path.GetFileNameWithoutExtension(sourcePath) + ".png"));
            }
            else if (arguments.Contains("--frames-dir", StringComparer.OrdinalIgnoreCase))
            {
                var framesDirectory = GetArgumentAfter(arguments, "--frames-dir");
                var frameCount = int.Parse(
                    GetArgumentAfter(arguments, "--frame-count"),
                    CultureInfo.InvariantCulture);
                for (var index = 0; index < Math.Min(8, frameCount); index++)
                {
                    CreateFile(Path.Combine(framesDirectory, $"frame_{index:00000}.png"));
                }

                await Task.Delay(TimeSpan.FromMilliseconds(2300), cancellationToken);
                for (var index = 8; index < frameCount; index++)
                {
                    CreateFile(Path.Combine(framesDirectory, $"frame_{index:00000}.png"));
                }
            }
            else
            {
                CreateFile(arguments[^1]);
            }

            var now = DateTimeOffset.UtcNow;
            return new ProcessExecutionResult(
                ExitCode: 0,
                StandardOutput: "parallax step completed",
                StandardError: string.Empty,
                OutputLines: [],
                Status: ProcessExecutionStatus.Completed,
                StartedAt: now,
                EndedAt: now);
        }

        private static string GetArgumentAfter(IReadOnlyList<string> arguments, string switchName)
        {
            var index = arguments.ToList().FindIndex(argument =>
                string.Equals(argument, switchName, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < arguments.Count
                ? arguments[index + 1]
                : throw new InvalidOperationException($"Missing argument after {switchName}.");
        }
    }

    private sealed class RecordingParallaxProgress : IProgress<ImageParallaxExportProgress>
    {
        public List<ImageParallaxExportProgress> Messages { get; } = [];

        public void Report(ImageParallaxExportProgress value) => Messages.Add(value);
    }
}
