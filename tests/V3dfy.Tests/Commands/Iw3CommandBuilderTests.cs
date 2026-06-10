using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Engine.Iw3.Commands;

namespace V3dfy.Tests.Commands;

public sealed class Iw3CommandBuilderTests
{
    private static readonly InternalToolPaths Paths = TestPaths.InternalToolPaths();
    private static readonly LocalModelPlanSelection RecognizedDepthModel = new(
        "depth_anything_metric_depth_indoor.pt",
        Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
        LocalModelPlanSource.UnmanagedLocalFile);

    [Fact]
    public void Build_UsesConfirmedBaseStructuredArguments()
    {
        var command = Build();

        Assert.Equal(
            [
                Iw3CliContract.PythonModuleSwitch,
                Iw3CliContract.ModuleName,
                Iw3CliContract.InputSwitch,
                InputPath(),
                Iw3CliContract.OutputSwitch,
                OutputPath(),
                Iw3CliContract.HalfTopBottomSwitch,
            ],
            command.Arguments);
    }

    [Fact]
    public void Build_RecognizedDepthAnythingMetricDepthIndoorMapsToZoeDAnyN()
    {
        var command = Build(selectedLocalModel: RecognizedDepthModel);

        Assert.Equal(
            [
                Iw3CliContract.PythonModuleSwitch,
                Iw3CliContract.ModuleName,
                Iw3CliContract.InputSwitch,
                InputPath(),
                Iw3CliContract.OutputSwitch,
                OutputPath(),
                Iw3CliContract.HalfTopBottomSwitch,
                Iw3CliContract.DepthModelSwitch,
                Iw3DepthModelMapper.ZoeDAnyNDepthModelName,
            ],
            command.Arguments);
        Assert.DoesNotContain("--model", command.Arguments);
        Assert.DoesNotContain(
            command.Arguments,
            argument => argument.Contains(
                Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ExposesUnconfirmedPlanningOptionsAsMetadata()
    {
        var command = Build();

        Assert.Contains("selected model", command.UnconfirmedPlanningOptions);
        Assert.DoesNotContain("3D layout", command.UnconfirmedPlanningOptions);
        Assert.Contains("video codec", command.UnconfirmedPlanningOptions);
        Assert.Contains("quality preset", command.UnconfirmedPlanningOptions);
        Assert.Contains("3D intensity/depth", command.UnconfirmedPlanningOptions);
        Assert.Contains("scene detection", command.UnconfirmedPlanningOptions);
        Assert.Contains("normalization", command.UnconfirmedPlanningOptions);
        Assert.Contains("convergence/divergence", command.UnconfirmedPlanningOptions);
    }

    [Fact]
    public void Build_UnconfirmedEncodingOptionsDoNotBecomeExecutableArguments()
    {
        var command = Build(
            intensity: ThreeDIntensity.High,
            qualityPreset: AiQualityPreset.HighQuality);

        Assert.DoesNotContain("--video-codec", command.Arguments);
        Assert.DoesNotContain("--preset", command.Arguments);
        Assert.DoesNotContain("--scene-detect", command.Arguments);
        Assert.DoesNotContain("--ema-normalize", command.Arguments);
        Assert.DoesNotContain("-d", command.Arguments);
        Assert.DoesNotContain("-c", command.Arguments);
    }

    [Fact]
    public void Build_PreviewContainsExecutableModuleInputAndOutput()
    {
        var command = Build();

        Assert.Contains("python.exe", command.FullCommandPreview);
        Assert.Contains("-m iw3", command.FullCommandPreview);
        Assert.Contains(Quote(InputPath()), command.FullCommandPreview);
        Assert.Contains(Quote(OutputPath()), command.FullCommandPreview);
        Assert.Contains("--half-tb", command.FullCommandPreview);
        Assert.DoesNotContain("--preset", command.FullCommandPreview);
        Assert.DoesNotContain("--video-codec", command.FullCommandPreview);
    }

    [Fact]
    public void Build_EnablesDryRun_WhenEngineHealthIsIncomplete()
    {
        var command = Build(healthStatus: MissingHealth());

        Assert.True(command.DryRun);
    }

    [Fact]
    public void Build_UsesEmbeddedPythonPathAndDoesNotRelyOnPathOrShell()
    {
        var command = Build();

        Assert.Equal(Paths.PythonExecutable, command.ExecutablePath);
        Assert.NotEqual("python.exe", command.ExecutablePath);
        Assert.DoesNotContain("cmd.exe", command.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell", command.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ThreeDOutputFormat.HalfTopBottom, "--half-tb")]
    [InlineData(ThreeDOutputFormat.HalfSideBySide, "--half-sbs")]
    [InlineData(ThreeDOutputFormat.Anaglyph, "--anaglyph")]
    public void Build_OutputFormatBecomesVerifiedExecutableArgument(
        ThreeDOutputFormat format,
        string verifiedArgument)
    {
        var command = Build(outputFormat: format);

        Assert.Contains(verifiedArgument, command.Arguments);
    }

    [Fact]
    public void Build_FullSideBySideIsRejectedBecauseNoVerifiedIw3FlagExists()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => Build(outputFormat: ThreeDOutputFormat.FullSideBySide));

        Assert.Contains("no verified direct SBS flag", exception.Message);
    }

    [Theory]
    [InlineData(ThreeDIntensity.Low, null)]
    [InlineData(ThreeDIntensity.Medium, null)]
    [InlineData(ThreeDIntensity.High, null)]
    [InlineData(ThreeDIntensity.Custom, 1.75)]
    public void Build_IntensityDoesNotBecomeExecutableArgumentUntilConfirmed(
        ThreeDIntensity intensity,
        double? customDepth)
    {
        var command = Build(intensity: intensity, customDepth: customDepth);

        Assert.DoesNotContain("-d", command.Arguments);
    }

    [Theory]
    [InlineData(AiQualityPreset.Fast)]
    [InlineData(AiQualityPreset.Balanced)]
    [InlineData(AiQualityPreset.HighQuality)]
    public void Build_QualityDoesNotBecomeExecutableArgumentUntilConfirmed(
        AiQualityPreset preset)
    {
        var command = Build(qualityPreset: preset);

        Assert.DoesNotContain("--preset", command.Arguments);
    }

    private static Iw3Command Build(
        ThreeDOutputFormat outputFormat = ThreeDOutputFormat.HalfTopBottom,
        ThreeDIntensity intensity = ThreeDIntensity.Medium,
        double? customDepth = null,
        AiQualityPreset qualityPreset = AiQualityPreset.Balanced,
        EngineHealthStatus? healthStatus = null,
        LocalModelPlanSelection? selectedLocalModel = null)
    {
        var request = new ConversionRequest(
            InputPath: InputPath(),
            OutputPath: OutputPath(),
            OutputContainer: OutputContainer.MP4,
            ThreeDOutputFormat: outputFormat,
            AiQualityPreset: qualityPreset,
            ThreeDIntensity: intensity,
            CustomDepth: customDepth);

        return new Iw3CommandBuilder().Build(
            request,
            Paths,
            healthStatus ?? CompleteHealth(),
            selectedLocalModel);
    }

    private static string InputPath() => TestPaths.SourceRoot("input video.mp4");

    private static string OutputPath() => TestPaths.OutputRoot("output video.mp4");

    private static string Quote(string path) => $"\"{path}\"";

    private static EngineHealthStatus CompleteHealth() => new(
        Ffmpeg: ToolHealthStatus.Found,
        Ffprobe: ToolHealthStatus.Found,
        Python: ToolHealthStatus.Found,
        Iw3EngineDirectory: ToolHealthStatus.Found,
        ModelsDirectory: ToolHealthStatus.Found);

    private static EngineHealthStatus MissingHealth() => CompleteHealth() with
    {
        Iw3EngineDirectory = ToolHealthStatus.Missing,
    };

}
