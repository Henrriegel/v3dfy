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
    public void Create_MkvOption_SuggestsMkvOutput()
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            OutputContainer = OutputContainer.MKV,
        });

        Assert.Equal(OutputContainer.MKV, plan.OutputContainer);
        Assert.EndsWith(".v3dfy.3d.htab.mkv", plan.SuggestedOutputPath);
    }

    [Fact]
    public void Create_SelectedQuality_UpdatesPlanAndCommandPreview()
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            QualityPreset = AiQualityPreset.HighQuality,
        });

        Assert.Equal(AiQualityPreset.HighQuality, plan.QualityPreset);
        Assert.Contains("--preset slow", plan.CommandPreview);
    }

    [Fact]
    public void Create_SelectedIntensity_UpdatesPlanAndCommandPreview()
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            Intensity = ThreeDIntensity.High,
        });

        Assert.Equal(ThreeDIntensity.High, plan.Intensity);
        Assert.Contains("-d 2.0", plan.CommandPreview);
    }

    [Fact]
    public void Create_SelectedHalfSideBySide_UpdatesPlanOutputAndCommandPreview()
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            ThreeDOutputFormat = ThreeDOutputFormat.HalfSideBySide,
        });

        Assert.Equal(ThreeDOutputFormat.HalfSideBySide, plan.ThreeDOutputFormat);
        Assert.EndsWith(".v3dfy.3d.hsbs.mp4", plan.SuggestedOutputPath);
        Assert.Contains("--half-sbs", plan.CommandPreview);
    }

    [Fact]
    public void Create_GeneralPreset_UsesSelectedPresetInPlanStep()
    {
        var plan = CreatePlan(targetPreset: TargetDevicePresets.General3dVideo);

        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains("General 3D video", StringComparison.Ordinal));
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
        VideoConversionPlanOptions? options = null,
        EngineHealthStatus? healthStatus = null,
        TargetDevicePreset? targetPreset = null)
    {
        targetPreset ??= TargetDevicePresets.Lg3dFullHd2012;
        var analysis = CreateAnalysis(inputPath);
        var recommendation = new VideoConversionRecommendationService().Recommend(
            analysis,
            targetPreset);

        return _service.Create(
            analysis,
            recommendation,
            targetPreset,
            options ?? DefaultOptions(),
            Paths,
            healthStatus ?? CompleteHealth());
    }

    private static VideoConversionPlanOptions DefaultOptions() => new(
        OutputContainer: OutputContainer.MP4,
        QualityPreset: AiQualityPreset.Balanced,
        Intensity: ThreeDIntensity.Medium,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom);

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
