using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Recommendations;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Engine.Iw3.Planning;

namespace V3dfy.Tests.Planning;

public sealed class VideoConversionPlanServiceTests
{
    private static readonly InternalToolPaths Paths = TestPaths.InternalToolPaths();

    private readonly VideoConversionPlanService _service = new();

    public static TheoryData<string, string, string> KnownDepthModelData
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var entry in Iw3DepthModelMapper.RegistryEntries.Where(static entry => entry.IsReadySelectable))
            {
                data.Add(
                    entry.Key,
                    entry.ExpectedRelativePaths[0],
                    entry.DepthModelName);
            }

            return data;
        }
    }

    [Fact]
    public void Create_Mp4Input_SuggestsTvCompatibleOutputPath()
    {
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");

        var plan = CreatePlan(sourcePath);

        Assert.Equal(TestPaths.SourceRoot("Movie.v3dfy.3d.htab.mp4"), plan.SuggestedOutputPath);
        Assert.False(plan.SuggestedOutputPath.StartsWith(
            TestPaths.RuntimeRoot(),
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_MkvInput_StillSuggestsMp4Output()
    {
        var plan = CreatePlan(TestPaths.SourceRoot("Movie.mkv"));

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
            CustomOutputPath = TestPaths.OutputRoot("ManualName.custom"),
        });

        Assert.Equal(TestPaths.OutputRoot("ManualName.custom"), plan.SuggestedOutputPath);
        Assert.Contains(TestPaths.OutputRoot("ManualName.custom"), plan.CommandPreview);
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
    public void Create_SelectedHalfSideBySide_UpdatesPlanOutputAndCommandFlag()
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            ThreeDOutputFormat = ThreeDOutputFormat.HalfSideBySide,
        });

        Assert.Equal(ThreeDOutputFormat.HalfSideBySide, plan.ThreeDOutputFormat);
        Assert.EndsWith(".v3dfy.3d.hsbs.mp4", plan.SuggestedOutputPath);
        Assert.Contains("--half-sbs", plan.CommandPreview);
    }

    [Theory]
    [InlineData(ThreeDOutputFormat.HalfTopBottom, ".v3dfy.3d.htab.mp4")]
    [InlineData(ThreeDOutputFormat.HalfSideBySide, ".v3dfy.3d.hsbs.mp4")]
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
    [InlineData(ThreeDOutputFormat.HalfTopBottom, ".v3dfy.3d.htab.mp4", "--half-tb", "Half Top-Bottom")]
    [InlineData(ThreeDOutputFormat.HalfSideBySide, ".v3dfy.3d.hsbs.mp4", "--half-sbs", "Half Side-by-Side")]
    [InlineData(ThreeDOutputFormat.Anaglyph, ".v3dfy.3d.anaglyph.mp4", "--anaglyph", "Anaglyph")]
    public void Create_SelectedLayout_SuffixPlanTextAndCommandPreviewAgree(
        ThreeDOutputFormat outputFormat,
        string expectedSuffix,
        string expectedCommandFlag,
        string expectedPlanText)
    {
        var plan = CreatePlan(options: DefaultOptions() with
        {
            ThreeDOutputFormat = outputFormat,
        });

        Assert.Equal(outputFormat, plan.ThreeDOutputFormat);
        Assert.EndsWith(expectedSuffix, plan.SuggestedOutputPath);
        Assert.Contains(expectedCommandFlag, plan.CommandPreview);
        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains(expectedPlanText, StringComparison.Ordinal));
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
    public void Create_RecommendedPreset_UsesSelectedPresetInPlanStep()
    {
        var plan = CreatePlan(targetPreset: TargetDevicePresets.Recommended3dTv);

        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains("Recommended 3D TV", StringComparison.Ordinal));
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
    public void Create_LgCompatibilityCopyOption_AddsPostProcessStepWithoutIw3EncodingFlags()
    {
        var plan = CreatePlan(
            targetPreset: TargetDevicePresets.Lg3dFullHd2012,
            options: DefaultOptions() with
            {
                ThreeDOutputFormat = ThreeDOutputFormat.HalfSideBySide,
                CreateLgCompatibilityCopy = true,
            });

        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains(
                "After the primary iw3 output succeeds",
                StringComparison.Ordinal));
        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains("yuv420p", StringComparison.Ordinal));
        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains(
                "copied audio from the primary output",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            plan.Steps,
            step => step.EnglishText.Contains("AAC, yuv420p", StringComparison.Ordinal));
        Assert.DoesNotContain("--pix_fmt", plan.CommandPreview);
        Assert.DoesNotContain("--video-codec", plan.CommandPreview);
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
    public void Create_MissingIw3RuntimeDependency_ProducesDryRun()
    {
        var plan = CreatePlan(healthStatus: CompleteHealth() with
        {
            Iw3RuntimeDependencies = ToolHealthStatus.Missing,
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
    public void Create_SelectedModelWithoutSafeDepthArgument_AddsOutputSlugOnly()
    {
        var planWithoutModel = CreatePlan();
        var planWithModel = CreatePlan(selectedLocalModel: CatalogManagedModel());

        Assert.DoesNotContain("--depth-model", planWithModel.CommandPreview);
        Assert.NotEqual(planWithoutModel.CommandPreview, planWithModel.CommandPreview);
        Assert.EndsWith(
            "Movie.default-depth-model.v3dfy.3d.htab.mp4",
            planWithModel.SuggestedOutputPath);
    }

    [Fact]
    public void Create_RecognizedDepthModel_AddsVerifiedDepthModelToCommandPreview()
    {
        var plan = CreatePlan(selectedLocalModel: RecognizedDepthModel());

        Assert.NotNull(plan.SelectedLocalModel);
        Assert.Equal("Depth Anything Metric Indoor", plan.SelectedLocalModel.DisplayName);
        Assert.Equal(
            Iw3DepthModelMapper.ZoeDAnyNDepthModelName,
            plan.SelectedLocalModel.Iw3DepthModelName);
        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains(
                $"iw3 depth model: {Iw3DepthModelMapper.ZoeDAnyNDepthModelName}",
                StringComparison.Ordinal));
        Assert.Contains("--depth-model ZoeD_Any_N", plan.CommandPreview);
        Assert.DoesNotContain("--model", plan.CommandPreview);
        Assert.DoesNotContain(
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
            plan.CommandPreview);
        Assert.EndsWith(
            "Movie.depth-anything-metric-indoor.v3dfy.3d.htab.mp4",
            plan.SuggestedOutputPath);
    }

    [Fact]
    public void Create_RecognizedDepthAnythingV2Small_AddsMappingKeySlugToAutomaticOutputPath()
    {
        var plan = CreatePlan(
            inputPath: TestPaths.SourceRoot("sample_video.mp4"),
            selectedLocalModel: RecognizedDepthModel(
                Iw3DepthModelMapper.DepthAnythingV2SmallRelativePath,
                Iw3DepthModelMapper.AnyV2SDepthModelName,
                Iw3DepthModelMapper.DepthAnythingV2SmallKey));

        Assert.Equal(
            TestPaths.SourceRoot("sample_video.depth-anything-v2-small.v3dfy.3d.htab.mp4"),
            plan.SuggestedOutputPath);
        Assert.Contains(plan.SuggestedOutputPath, plan.CommandPreview);
    }

    [Fact]
    public void Create_SelectedModel_CustomOutputPathRemainsExact()
    {
        var customOutputPath = TestPaths.OutputRoot("ManualName.custom");

        var plan = CreatePlan(
            options: DefaultOptions() with
            {
                CustomOutputPath = customOutputPath,
            },
            selectedLocalModel: RecognizedDepthModel(
                Iw3DepthModelMapper.DepthAnythingV2SmallRelativePath,
                Iw3DepthModelMapper.AnyV2SDepthModelName,
                Iw3DepthModelMapper.DepthAnythingV2SmallKey));

        Assert.Equal(customOutputPath, plan.SuggestedOutputPath);
        Assert.DoesNotContain("depth-anything-v2-small.v3dfy", plan.SuggestedOutputPath);
    }

    [Fact]
    public void CreateSuggestedOutputPath_SelectedModelSlugIsNotDuplicated()
    {
        var outputPath = VideoConversionPlanService.CreateSuggestedOutputPath(
            TestPaths.SourceRoot("sample_video.depth-anything-v2-small.mp4"),
            OutputContainer.MP4,
            ThreeDOutputFormat.HalfTopBottom,
            RecognizedDepthModel(
                Iw3DepthModelMapper.DepthAnythingV2SmallRelativePath,
                Iw3DepthModelMapper.AnyV2SDepthModelName,
                Iw3DepthModelMapper.DepthAnythingV2SmallKey));

        Assert.Equal(
            TestPaths.SourceRoot("sample_video.depth-anything-v2-small.v3dfy.3d.htab.mp4"),
            outputPath);
    }

    [Theory]
    [MemberData(nameof(KnownDepthModelData))]
    public void Create_RecognizedDepthModels_AddMatchingDepthModelToCommandPreview(
        string key,
        string relativePath,
        string depthModelName)
    {
        var plan = CreatePlan(selectedLocalModel: RecognizedDepthModel(
            relativePath,
            depthModelName,
            key));

        Assert.NotNull(plan.SelectedLocalModel);
        Assert.Equal(depthModelName, plan.SelectedLocalModel.Iw3DepthModelName);
        Assert.Contains($"--depth-model {depthModelName}", plan.CommandPreview);
        Assert.Contains(
            plan.Steps,
            step => step.EnglishText.Contains(
                $"iw3 depth model: {depthModelName}",
                StringComparison.Ordinal));
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
        string? inputPath = null,
        VideoConversionPlanOptions? options = null,
        EngineHealthStatus? healthStatus = null,
        TargetDevicePreset? targetPreset = null,
        LocalModelSelectionCandidate? selectedLocalModel = null)
    {
        targetPreset ??= TargetDevicePresets.Lg3dFullHd2012;
        var analysis = CreateAnalysis(inputPath ?? TestPaths.SourceRoot("Movie.mp4"));
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

    private static LocalModelSelectionCandidate RecognizedDepthModel(
        string? relativePath = null,
        string? depthModelName = null,
        string? mappingKey = null)
    {
        relativePath ??= Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath;
        var fileName = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();

        return new(
        Id: relativePath,
        DisplayName: fileName,
        RelativePath: relativePath,
        FileName: fileName,
        Extension: ".pt",
        ModelType: string.Empty,
        Purpose: string.Empty,
        IsCatalogManaged: false,
        Iw3DepthModelName: depthModelName,
        MappingKey: mappingKey);
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
