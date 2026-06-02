using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Recommendations;
using V3dfy.Engine.Iw3.Planning;

namespace V3dfy.Tests.Planning;

public sealed class VideoConversionPlanServiceTests
{
    private static readonly InternalToolPaths Paths = new(
        FfmpegExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffmpeg.exe",
        FfprobeExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffprobe.exe",
        PythonExecutable: @"C:\v3dfy\engine\iw3\python\python.exe",
        Iw3EngineDirectory: @"C:\v3dfy\engine\iw3",
        ModelsDirectory: @"C:\v3dfy\engine\iw3\models");

    private readonly VideoConversionPlanService _service = new();

    [Fact]
    public void Create_Mp4Input_SuggestsTvCompatibleOutputPath()
    {
        var plan = CreatePlan(@"C:\Videos\Movie.mp4");

        Assert.Equal(@"C:\Videos\Movie.v3dfy.3d.htab.mp4", plan.SuggestedOutputPath);
    }

    [Fact]
    public void Create_MkvInput_StillSuggestsMp4Output()
    {
        var plan = CreatePlan(@"C:\Videos\Movie.mkv");

        Assert.Equal(OutputContainer.MP4, plan.OutputContainer);
        Assert.EndsWith(".v3dfy.3d.htab.mp4", plan.SuggestedOutputPath);
    }

    [Fact]
    public void Create_UsesLgTvRecommendation()
    {
        var plan = CreatePlan();

        Assert.Equal(1920, plan.Width);
        Assert.Equal(1080, plan.Height);
        Assert.Equal("H.264", plan.VideoCodec);
        Assert.Equal("AAC or AC3", plan.AudioCodec);
        Assert.Equal(ThreeDOutputFormat.HalfTopBottom, plan.ThreeDOutputFormat);
        Assert.Contains("--half-tb", plan.CommandPreview);
    }

    [Fact]
    public void Create_MissingLocalBundle_ProducesDryRun()
    {
        var plan = CreatePlan(healthStatus: CompleteHealth() with
        {
            Python = ToolHealthStatus.Missing,
            Iw3EngineDirectory = ToolHealthStatus.Missing,
            ModelsDirectory = ToolHealthStatus.Missing,
        });

        Assert.True(plan.IsDryRun);
        Assert.Equal(ConversionDryRunReason.MissingLocalAiBundle, plan.DryRunReason);
    }

    [Fact]
    public void Create_CompleteHealth_ProducesReadyPlan()
    {
        var plan = CreatePlan();

        Assert.False(plan.IsDryRun);
        Assert.Equal(ConversionDryRunReason.None, plan.DryRunReason);
    }

    private VideoConversionPlan CreatePlan(
        string inputPath = @"C:\Videos\Movie.mp4",
        EngineHealthStatus? healthStatus = null)
    {
        var analysis = CreateAnalysis(inputPath);
        var recommendation = new VideoConversionRecommendationService().Recommend(
            analysis,
            TargetDevicePresets.Lg3dFullHd2012);

        return _service.Create(
            analysis,
            recommendation,
            TargetDevicePresets.Lg3dFullHd2012,
            Paths,
            healthStatus ?? CompleteHealth());
    }

    private static VideoAnalysisResult CreateAnalysis(string inputPath) => new(
        InputPath: inputPath,
        File: new VideoFileMetadata(TimeSpan.FromMinutes(10), "mov,mp4", 5_800_000),
        Video: new VideoStreamInfo(0, 1920, 1080, 23.976, "h264", "yuv420p", null, null, null, false),
        AudioStreams: [new AudioStreamInfo(1, "aac", 2, "stereo", 48000, "eng")],
        SubtitleStreams: [],
        Warnings: []);

    private static EngineHealthStatus CompleteHealth() => new(
        Ffmpeg: ToolHealthStatus.Found,
        Ffprobe: ToolHealthStatus.Found,
        Python: ToolHealthStatus.Found,
        Iw3EngineDirectory: ToolHealthStatus.Found,
        ModelsDirectory: ToolHealthStatus.Found);
}
