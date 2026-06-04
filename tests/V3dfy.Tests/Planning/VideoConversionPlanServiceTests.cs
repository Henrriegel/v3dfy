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
        ModelsDirectory: @"C:\v3dfy\engine\iw3\nunif\iw3\pretrained_models");

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
    public void Create_CustomOutputPath_UsesExactPath()
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            CustomOutputPath = @"D:\Converted\ManualName.custom",
        });

        Assert.Equal(@"D:\Converted\ManualName.custom", plan.SuggestedOutputPath);
        Assert.Contains(@"D:\Converted\ManualName.custom", plan.CommandPreview);
    }

    [Fact]
    public void Create_SelectedQuality_UpdatesPlanWithoutAddingUnconfirmedCommandFlag()
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            QualityPreset = AiQualityPreset.HighQuality,
        });

        Assert.Equal(AiQualityPreset.HighQuality, plan.QualityPreset);
        Assert.DoesNotContain("--preset", plan.CommandPreview);
    }

    [Fact]
    public void Create_SelectedIntensity_UpdatesPlanWithoutAddingUnconfirmedCommandFlag()
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            Intensity = ThreeDIntensity.High,
        });

        Assert.Equal(ThreeDIntensity.High, plan.Intensity);
        Assert.DoesNotContain("-d", plan.CommandPreview);
    }

    [Fact]
    public void Create_SelectedHalfSideBySide_UpdatesPlanOutputWithoutAddingUnconfirmedCommandFlag()
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            ThreeDOutputFormat = ThreeDOutputFormat.HalfSideBySide,
        });

        Assert.Equal(ThreeDOutputFormat.HalfSideBySide, plan.ThreeDOutputFormat);
        Assert.EndsWith(".v3dfy.3d.hsbs.mp4", plan.SuggestedOutputPath);
        Assert.DoesNotContain("--half-sbs", plan.CommandPreview);
    }

    [Theory]
    [InlineData(ThreeDOutputFormat.HalfTopBottom, ".v3dfy.3d.htab.mp4")]
    [InlineData(ThreeDOutputFormat.HalfSideBySide, ".v3dfy.3d.hsbs.mp4")]
    [InlineData(ThreeDOutputFormat.FullSideBySide, ".v3dfy.3d.sbs.mp4")]
    [InlineData(ThreeDOutputFormat.Anaglyph, ".v3dfy.3d.anaglyph.mp4")]
    public void Create_AutomaticOutputPath_UsesLayoutSuffix(
        ThreeDOutputFormat outputFormat,
        string expectedSuffix)
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            ThreeDOutputFormat = outputFormat,
        });

        Assert.EndsWith(expectedSuffix, plan.SuggestedOutputPath);
    }

    [Theory]
    [InlineData(OutputContainer.MP4, ".mp4")]
    [InlineData(OutputContainer.MKV, ".mkv")]
    public void Create_AutomaticOutputPath_UsesSelectedContainerExtension(
        OutputContainer outputContainer,
        string expectedExtension)
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            OutputContainer = outputContainer,
        });

        Assert.EndsWith(expectedExtension, plan.SuggestedOutputPath);
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
        Assert.DoesNotContain("--half-tb", plan.CommandPreview);
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

    [Fact]
    public void Create_NoSelectedModel_LeavesModelMetadataEmpty()
    {
        var plan = CreatePlan();

        Assert.Null(plan.SelectedLocalModel);
        Assert.DoesNotContain(
            plan.Steps,
            step => step.EnglishText.Contains("selected local model", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_SelectedCatalogManagedModel_AddsFriendlyModelMetadataAndStep()
    {
        var plan = CreatePlan(selectedLocalModel: CatalogManagedModel());

        Assert.NotNull(plan.SelectedLocalModel);
        Assert.Equal("Default depth model", plan.SelectedLocalModel.DisplayName);
        Assert.Equal("depth/default-depth.onnx", plan.SelectedLocalModel.RelativePath);
        Assert.Equal(LocalModelPlanSource.CatalogMetadata, plan.SelectedLocalModel.Source);
        Assert.Equal("Catalog metadata", plan.SelectedLocalModel.EnglishSourceText);
        Assert.Equal("Cat\u00e1logo", plan.SelectedLocalModel.SpanishSourceText);
        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains("Default depth model", StringComparison.Ordinal) &&
                    step.EnglishText.Contains("depth/default-depth.onnx", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_SelectedUnmanagedModel_AddsFilenameModelMetadataAndStep()
    {
        var plan = CreatePlan(selectedLocalModel: UnmanagedModel());

        Assert.NotNull(plan.SelectedLocalModel);
        Assert.Equal("local-depth.safetensors", plan.SelectedLocalModel.DisplayName);
        Assert.Equal("local-depth.safetensors", plan.SelectedLocalModel.RelativePath);
        Assert.Equal(LocalModelPlanSource.UnmanagedLocalFile, plan.SelectedLocalModel.Source);
        Assert.Equal("Unmanaged local file", plan.SelectedLocalModel.EnglishSourceText);
        Assert.Equal("Archivo local no administrado", plan.SelectedLocalModel.SpanishSourceText);
        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains("local-depth.safetensors", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_SelectedModel_DoesNotChangeCommandPreviewWithoutSafeModelArgument()
    {
        var planWithoutModel = CreatePlan();
        var planWithModel = CreatePlan(selectedLocalModel: CatalogManagedModel());

        Assert.Equal(planWithoutModel.CommandPreview, planWithModel.CommandPreview);
    }

    [Fact]
    public void Create_SelectedModel_DoesNotEnableConversionWhenBundleIsMissing()
    {
        var plan = CreatePlan(
            healthStatus: CompleteHealth() with
            {
                Python = ToolHealthStatus.Missing,
                Iw3EngineDirectory = ToolHealthStatus.Missing,
                ModelsDirectory = ToolHealthStatus.Missing,
            },
            selectedLocalModel: CatalogManagedModel());

        Assert.True(plan.IsDryRun);
        Assert.Equal(ConversionDryRunReason.MissingLocalAiBundle, plan.DryRunReason);
        Assert.NotNull(plan.SelectedLocalModel);
    }

    private VideoConversionPlan CreatePlan(
        string inputPath = @"C:\Videos\Movie.mp4",
        VideoConversionPlanOptions? options = null,
        EngineHealthStatus? healthStatus = null,
        TargetDevicePreset? targetPreset = null,
        LocalModelSelectionCandidate? selectedLocalModel = null)
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
            healthStatus ?? CompleteHealth(),
            selectedLocalModel);
    }

    private static LocalModelSelectionCandidate CatalogManagedModel() => new(
        Id: "depth-default",
        DisplayName: "Default depth model",
        RelativePath: "depth/default-depth.onnx",
        FileName: "default-depth.onnx",
        Extension: ".onnx",
        ModelType: "depth-estimation",
        Purpose: "2D to 3D depth generation",
        IsCatalogManaged: true);

    private static LocalModelSelectionCandidate UnmanagedModel() => new(
        Id: "local-depth.safetensors",
        DisplayName: "local-depth.safetensors",
        RelativePath: "local-depth.safetensors",
        FileName: "local-depth.safetensors",
        Extension: ".safetensors",
        ModelType: string.Empty,
        Purpose: string.Empty,
        IsCatalogManaged: false);

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
