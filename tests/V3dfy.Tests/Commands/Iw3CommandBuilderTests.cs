using V3dfy.Core.Models;
using V3dfy.Engine.Iw3.Commands;

namespace V3dfy.Tests.Commands;

public sealed class Iw3CommandBuilderTests
{
    private static readonly InternalToolPaths Paths = new(
        FfmpegExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffmpeg.exe",
        FfprobeExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffprobe.exe",
        PythonExecutable: @"C:\v3dfy\engine\iw3\python\python.exe",
        Iw3EngineDirectory: @"C:\v3dfy\engine\iw3",
        ModelsDirectory: @"C:\v3dfy\engine\iw3\models");

    [Fact]
    public void Build_HalfTopBottom_AddsHalfTbArgument()
    {
        var command = Build();

        Assert.Contains("--half-tb", command.Arguments);
    }

    [Fact]
    public void Build_MediumIntensity_AddsDepthOnePointFive()
    {
        var command = Build();

        AssertArgumentValue(command.Arguments, "-d", "1.5");
    }

    [Fact]
    public void Build_BalancedQuality_AddsMediumPreset()
    {
        var command = Build();

        AssertArgumentValue(command.Arguments, "--preset", "medium");
    }

    [Fact]
    public void Build_PreviewContainsExecutableModuleInputAndOutput()
    {
        var command = Build();

        Assert.Contains("python.exe", command.FullCommandPreview);
        Assert.Contains("-m iw3", command.FullCommandPreview);
        Assert.Contains("\"C:\\videos\\input video.mp4\"", command.FullCommandPreview);
        Assert.Contains("\"C:\\videos\\output video.mp4\"", command.FullCommandPreview);
    }

    [Fact]
    public void Build_EnablesDryRun_WhenEngineHealthIsIncomplete()
    {
        var command = Build(healthStatus: MissingHealth());

        Assert.True(command.DryRun);
    }

    [Theory]
    [InlineData(ThreeDOutputFormat.HalfTopBottom, "--half-tb")]
    [InlineData(ThreeDOutputFormat.HalfSideBySide, "--half-sbs")]
    [InlineData(ThreeDOutputFormat.FullSideBySide, "--sbs")]
    [InlineData(ThreeDOutputFormat.Anaglyph, "--anaglyph")]
    public void Build_MapsOutputFormat(ThreeDOutputFormat format, string expectedArgument)
    {
        var command = Build(outputFormat: format);

        Assert.Contains(expectedArgument, command.Arguments);
    }

    [Theory]
    [InlineData(ThreeDIntensity.Low, null, "1.0")]
    [InlineData(ThreeDIntensity.Medium, null, "1.5")]
    [InlineData(ThreeDIntensity.High, null, "2.0")]
    [InlineData(ThreeDIntensity.Custom, 1.75, "1.75")]
    public void Build_MapsIntensity(
        ThreeDIntensity intensity,
        double? customDepth,
        string expectedDepth)
    {
        var command = Build(intensity: intensity, customDepth: customDepth);

        AssertArgumentValue(command.Arguments, "-d", expectedDepth);
    }

    [Theory]
    [InlineData(AiQualityPreset.Fast, "fast")]
    [InlineData(AiQualityPreset.Balanced, "medium")]
    [InlineData(AiQualityPreset.HighQuality, "slow")]
    public void Build_MapsQualityPreset(AiQualityPreset preset, string expectedPreset)
    {
        var command = Build(qualityPreset: preset);

        AssertArgumentValue(command.Arguments, "--preset", expectedPreset);
    }

    private static Iw3Command Build(
        ThreeDOutputFormat outputFormat = ThreeDOutputFormat.HalfTopBottom,
        ThreeDIntensity intensity = ThreeDIntensity.Medium,
        double? customDepth = null,
        AiQualityPreset qualityPreset = AiQualityPreset.Balanced,
        EngineHealthStatus? healthStatus = null)
    {
        var request = new ConversionRequest(
            InputPath: @"C:\videos\input video.mp4",
            OutputPath: @"C:\videos\output video.mp4",
            OutputContainer: OutputContainer.MP4,
            ThreeDOutputFormat: outputFormat,
            AiQualityPreset: qualityPreset,
            ThreeDIntensity: intensity,
            CustomDepth: customDepth);

        return new Iw3CommandBuilder().Build(request, Paths, healthStatus ?? CompleteHealth());
    }

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

    private static void AssertArgumentValue(
        IReadOnlyList<string> arguments,
        string argument,
        string expectedValue)
    {
        var argumentIndex = Assert.Single(
            arguments.Select((value, index) => (value, index)),
            item => item.value == argument).index;

        Assert.Equal(expectedValue, arguments[argumentIndex + 1]);
    }
}
