using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Engine.Iw3.Execution;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Tests.Execution;

public sealed class LocalIw3ProcessRequestBuilderTests
{
    private static readonly InternalToolPaths Paths = TestPaths.InternalToolPaths();
    private static readonly LocalModelPlanSelection RecognizedDepthModel = new(
        "depth_anything_metric_depth_indoor.pt",
        Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
        LocalModelPlanSource.UnmanagedLocalFile);

    private readonly LocalIw3ProcessRequestBuilder _builder = new();

    public static TheoryData<string, string> KnownDepthModelData => new()
    {
        { Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath, Iw3DepthModelMapper.ZoeDAnyNDepthModelName },
        { Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorRelativePath, Iw3DepthModelMapper.ZoeDAnyKDepthModelName },
        { Iw3DepthModelMapper.ZoeDepthIndoorRelativePath, Iw3DepthModelMapper.ZoeDIndoorDepthModelName },
        { Iw3DepthModelMapper.ZoeDepthOutdoorRelativePath, Iw3DepthModelMapper.ZoeDOutdoorDepthModelName },
        { Iw3DepthModelMapper.ZoeDepthIndoorOutdoorRelativePath, Iw3DepthModelMapper.ZoeDIndoorOutdoorDepthModelName },
        { Iw3DepthModelMapper.DepthAnythingSmallRelativePath, Iw3DepthModelMapper.AnySDepthModelName },
        { Iw3DepthModelMapper.DepthAnythingBaseRelativePath, Iw3DepthModelMapper.AnyBDepthModelName },
        { Iw3DepthModelMapper.DepthAnythingV2SmallRelativePath, Iw3DepthModelMapper.AnyV2SDepthModelName },
    };

    [Fact]
    public void Build_UsesEmbeddedPythonExecutableFromRequest()
    {
        var processRequest = _builder.Build(CreateRequest());

        Assert.Equal(Paths.PythonExecutable, processRequest.ExecutablePath);
        Assert.True(Path.IsPathFullyQualified(processRequest.ExecutablePath));
        Assert.NotEqual("python.exe", processRequest.ExecutablePath);
    }

    [Fact]
    public void Build_UsesNunifRootDirectoryAsWorkingDirectory()
    {
        var processRequest = _builder.Build(CreateRequest());

        Assert.Equal(Paths.NunifRootDirectory, processRequest.WorkingDirectory);
    }

    [Fact]
    public void Build_UsesBundledRuntimeEnvironment()
    {
        var processRequest = _builder.Build(CreateRequest());

        Assert.NotNull(processRequest.EnvironmentVariables);
        Assert.Equal(Paths.NunifRootDirectory, processRequest.EnvironmentVariables["NUNIF_HOME"]);
        Assert.Equal("1", processRequest.EnvironmentVariables["PYTHONNOUSERSITE"]);
        Assert.Equal(Paths.ModelsDirectory, processRequest.EnvironmentVariables["TORCH_HOME"]);
    }

    [Fact]
    public void Build_SetsIw3EngineDirectoryAsAllowedRootForRealBundle()
    {
        var processRequest = _builder.Build(CreateRequest());

        Assert.Equal(Paths.Iw3EngineDirectory, processRequest.AllowedRootDirectory);
        Assert.StartsWith(
            Paths.Iw3EngineDirectory,
            processRequest.WorkingDirectory,
            StringComparison.OrdinalIgnoreCase);
        ProcessExecutionRequestValidator.ValidateBundledToolRequest(processRequest);
    }

    [Fact]
    public void Build_UsesStructuredArgumentListWithoutShellWrapper()
    {
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");
        var outputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var processRequest = _builder.Build(CreateRequest(
            sourcePath: sourcePath,
            outputPath: outputPath));

        Assert.Equal(
            [
                Iw3CliContract.PythonModuleSwitch,
                Iw3CliContract.ModuleName,
                Iw3CliContract.InputSwitch,
                sourcePath,
                Iw3CliContract.OutputSwitch,
                outputPath,
                Iw3CliContract.HalfTopBottomSwitch,
                Iw3CliContract.DepthModelSwitch,
                Iw3DepthModelMapper.ZoeDAnyNDepthModelName,
            ],
            processRequest.Arguments);
        Assert.DoesNotContain("cmd.exe", processRequest.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell", processRequest.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            processRequest.Arguments,
            argument => argument.Contains("-m iw3", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ReusesIw3CommandBuilderArgumentsAndPreservesPreviewShape()
    {
        var request = CreateRequest();
        var processRequest = _builder.Build(request);
        var command = new Iw3CommandBuilder().Build(
            CreateConversionRequest(request),
            Paths,
            CompleteHealth(),
            request.SelectedLocalModel);

        Assert.Equal(command.Arguments, processRequest.Arguments);
        Assert.Equal(request.CommandPreview, command.FullCommandPreview);
    }

    [Fact]
    public void Build_RecognizedSelectedLocalModelAddsVerifiedDepthModelArgumentOnly()
    {
        var processRequest = _builder.Build(CreateRequest());

        Assert.DoesNotContain(
            processRequest.Arguments,
            argument => argument.Contains(
                Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
                StringComparison.Ordinal));
        Assert.Contains(Iw3CliContract.DepthModelSwitch, processRequest.Arguments);
        Assert.Contains(Iw3DepthModelMapper.ZoeDAnyNDepthModelName, processRequest.Arguments);
        Assert.DoesNotContain("--model", processRequest.Arguments);
    }

    [Theory]
    [MemberData(nameof(KnownDepthModelData))]
    public void Build_RecognizedSelectedLocalModelsAddMatchingVerifiedDepthModelArgument(
        string relativePath,
        string depthModelName)
    {
        var processRequest = _builder.Build(CreateRequest(selectedModel: new(
            FileName(relativePath),
            relativePath,
            LocalModelPlanSource.UnmanagedLocalFile)));

        AssertContainsAdjacentArguments(
            processRequest.Arguments,
            Iw3CliContract.DepthModelSwitch,
            depthModelName);
        Assert.DoesNotContain("--model", processRequest.Arguments);
        Assert.DoesNotContain(
            processRequest.Arguments,
            argument => argument.Contains(relativePath, StringComparison.Ordinal));
    }

    [Fact]
    public void Build_UnmappedSelectedLocalModelIsRejectedBeforeProcessDataIsReturned()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => _builder.Build(CreateRequest(
                selectedModel: new(
                    "Default depth model",
                    "depth/default-depth.onnx",
                    LocalModelPlanSource.CatalogMetadata))));

        Assert.Contains(
            "Selected local model is not mapped",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Build_UsesVerifiedLayoutArgumentButKeepsEncodingOptionsUnconfirmed()
    {
        var processRequest = _builder.Build(CreateRequest(
            outputFormat: ThreeDOutputFormat.Anaglyph,
            qualityPreset: AiQualityPreset.HighQuality,
            intensity: ThreeDIntensity.High));

        Assert.Contains(Iw3CliContract.AnaglyphSwitch, processRequest.Arguments);
        Assert.DoesNotContain("--preset", processRequest.Arguments);
        Assert.DoesNotContain("-d", processRequest.Arguments);
        Assert.DoesNotContain("--video-codec", processRequest.Arguments);
        Assert.DoesNotContain("--scene-detect", processRequest.Arguments);
        Assert.DoesNotContain("--ema-normalize", processRequest.Arguments);
        Assert.DoesNotContain("-c", processRequest.Arguments);
    }

    [Fact]
    public void Build_DryRunRequestCreatesDataOnly()
    {
        var processRequest = _builder.Build(CreateRequest());

        Assert.Equal(Paths.PythonExecutable, processRequest.ExecutablePath);
        Assert.Contains("iw3", processRequest.Arguments);
    }

    [Fact]
    public void Build_InvalidRequestIsRejectedBeforeProcessDataIsReturned()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => _builder.Build(CreateRequest(sourcePath: string.Empty)));

        Assert.Contains("invalid conversion execution request", exception.Message);
        Assert.Contains("Source video path is required", exception.Message);
    }

    [Fact]
    public async Task LocalIw3ConversionExecutor_StillBlocksDryRunAndDoesNotUseProcessRequest()
    {
        var processRequestBuilder = new CountingProcessRequestBuilder();
        var processRunner = new FakeProcessRunner();
        var executor = new LocalIw3ConversionExecutor(
            processRequestBuilder: processRequestBuilder,
            processRunner: processRunner);

        var result = await executor.ExecuteAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Contains("dry-run", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, processRequestBuilder.BuildCallCount);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "No Python, iw3, FFmpeg conversion, or model process was started.",
                StringComparison.Ordinal));
        Assert.Equal(0, processRunner.RunCallCount);
    }

    [Fact]
    public void LocalIw3ConversionExecutor_ExposesProcessRunnerDependencyForTestability()
    {
        var constructorParameterTypes = typeof(LocalIw3ConversionExecutor)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name);

        Assert.Contains(
            constructorParameterTypes,
            parameterTypeName => parameterTypeName.Contains("ProcessRunner", StringComparison.Ordinal));
    }

    private static ConversionExecutionRequest CreateRequest(
        string? sourcePath = null,
        string? outputPath = null,
        LocalModelPlanSelection? selectedModel = null,
        ThreeDOutputFormat outputFormat = ThreeDOutputFormat.HalfTopBottom,
        AiQualityPreset qualityPreset = AiQualityPreset.Balanced,
        ThreeDIntensity intensity = ThreeDIntensity.Medium,
        VideoConversionPlanStatus planStatus = VideoConversionPlanStatus.DryRun,
        ConversionDryRunReason dryRunReason = ConversionDryRunReason.MissingLocalAiBundle,
        bool isDryRun = true)
    {
        selectedModel ??= RecognizedDepthModel;
        sourcePath ??= TestPaths.SourceRoot("Movie.mp4");
        outputPath ??= TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");

        var options = new VideoConversionPlanOptions(
            OutputContainer: OutputContainer.MP4,
            QualityPreset: qualityPreset,
            Intensity: intensity,
            ThreeDOutputFormat: outputFormat);
        var commandPreview =
            string.IsNullOrWhiteSpace(sourcePath) ||
            string.IsNullOrWhiteSpace(outputPath)
                ? "iw3 local engine dry-run preview"
                : new Iw3CommandBuilder().Build(
                    new ConversionRequest(
                        InputPath: sourcePath,
                        OutputPath: outputPath,
                        OutputContainer: options.OutputContainer,
                        ThreeDOutputFormat: options.ThreeDOutputFormat,
                        AiQualityPreset: options.QualityPreset,
                        ThreeDIntensity: options.Intensity),
                    Paths,
                    CompleteHealth(),
                    selectedModel).FullCommandPreview;
        var plan = new VideoConversionPlan(
            SourcePath: sourcePath,
            SuggestedOutputPath: outputPath,
            OutputContainer: options.OutputContainer,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: options.ThreeDOutputFormat,
            QualityPreset: options.QualityPreset,
            Intensity: options.Intensity,
            Status: planStatus,
            DryRunReason: dryRunReason,
            Steps:
            [
                new("Read the analyzed source video.", "Leer el video de origen analizado."),
            ],
            CommandPreview: commandPreview)
        {
            SelectedLocalModel = selectedModel,
        };

        return new(
            Plan: plan,
            SourcePath: sourcePath,
            OutputPath: outputPath,
            SelectedPreset: TargetDevicePresets.General3dVideo,
            Options: options,
            ExpectedToolPaths: Paths,
            SelectedLocalModel: selectedModel,
            CommandPreview: commandPreview,
            PlanStatus: planStatus,
            DryRunReason: dryRunReason,
            IsDryRun: isDryRun);
    }

    private static ConversionRequest CreateConversionRequest(
        ConversionExecutionRequest request) => new(
        InputPath: request.SourcePath,
        OutputPath: request.OutputPath,
        OutputContainer: request.OutputContainer,
        ThreeDOutputFormat: request.ThreeDOutputFormat,
        AiQualityPreset: request.QualityPreset,
        ThreeDIntensity: request.Intensity);

    private static EngineHealthStatus CompleteHealth() => new(
        Ffmpeg: ToolHealthStatus.Found,
        Ffprobe: ToolHealthStatus.Found,
        Python: ToolHealthStatus.Found,
        Iw3EngineDirectory: ToolHealthStatus.Found,
        ModelsDirectory: ToolHealthStatus.Found);

    private static void AssertContainsAdjacentArguments(
        IReadOnlyList<string> arguments,
        string name,
        string value)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index] == name && arguments[index + 1] == value)
            {
                return;
            }
        }

        Assert.Fail(
            $"Expected adjacent arguments '{name} {value}' in: {string.Join(' ', arguments)}");
    }

    private static string FileName(string relativePath) =>
        relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();

    private sealed class CountingProcessRequestBuilder : LocalIw3ProcessRequestBuilder
    {
        public int BuildCallCount { get; private set; }

        public override ProcessExecutionRequest Build(ConversionExecutionRequest request)
        {
            BuildCallCount++;
            return base.Build(request);
        }
    }

    private sealed class FakeProcessRunner : ILocalProcessRunner
    {
        public int RunCallCount { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new ProcessExecutionResult(
                ExitCode: 0,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                OutputLines: [],
                Status: ProcessExecutionStatus.Completed,
                StartedAt: now,
                EndedAt: now));
        }
    }
}
