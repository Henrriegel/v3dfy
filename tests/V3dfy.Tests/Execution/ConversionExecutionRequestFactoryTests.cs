using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Readiness;

namespace V3dfy.Tests.Execution;

public sealed class ConversionExecutionRequestFactoryTests
{
    private static readonly InternalToolPaths Paths = new(
        FfmpegExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffmpeg.exe",
        FfprobeExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffprobe.exe",
        PythonExecutable: @"C:\v3dfy\engine\iw3\python\python.exe",
        Iw3EngineDirectory: @"C:\v3dfy\engine\iw3",
        ModelsDirectory: @"C:\v3dfy\engine\iw3\nunif\iw3\pretrained_models");

    private readonly ConversionExecutionRequestFactory _factory = new();

    [Fact]
    public void Create_IncludesSourceOutputPathsPresetAndSelectedOptions()
    {
        var options = Options() with
        {
            OutputContainer = OutputContainer.MKV,
            QualityPreset = AiQualityPreset.HighQuality,
            Intensity = ThreeDIntensity.High,
            ThreeDOutputFormat = ThreeDOutputFormat.HalfSideBySide,
        };
        var plan = CreatePlan(options: options);

        var request = _factory.Create(
            plan,
            TargetDevicePresets.General3dVideo,
            options,
            Paths);

        Assert.Same(plan, request.Plan);
        Assert.Equal(@"C:\Videos\Movie.mp4", request.SourcePath);
        Assert.Equal(@"C:\Videos\Movie.v3dfy.3d.hsbs.mkv", request.OutputPath);
        Assert.Equal(TargetDevicePresets.General3dVideo, request.SelectedPreset);
        Assert.Equal(OutputContainer.MKV, request.OutputContainer);
        Assert.Equal(ThreeDOutputFormat.HalfSideBySide, request.ThreeDOutputFormat);
        Assert.Equal(ThreeDIntensity.High, request.Intensity);
        Assert.Equal(AiQualityPreset.HighQuality, request.QualityPreset);
        Assert.Equal(Paths, request.ExpectedToolPaths);
        Assert.Equal(VideoConversionPlanStatus.DryRun, request.PlanStatus);
        Assert.Equal(ConversionDryRunReason.MissingLocalAiBundle, request.DryRunReason);
        Assert.True(request.IsDryRun);
    }

    [Fact]
    public void Create_IncludesSelectedCatalogManagedModelMetadata()
    {
        var selectedModel = new LocalModelPlanSelection(
            "Default depth model",
            "depth/default-depth.onnx",
            LocalModelPlanSource.CatalogMetadata);
        var plan = CreatePlan(selectedModel: selectedModel);

        var request = _factory.Create(
            plan,
            TargetDevicePresets.General3dVideo,
            Options(),
            Paths);

        Assert.Equal(selectedModel, request.SelectedLocalModel);
        Assert.Equal("Default depth model", request.SelectedLocalModel?.DisplayName);
        Assert.Equal("depth/default-depth.onnx", request.SelectedLocalModel?.RelativePath);
        Assert.Equal(LocalModelPlanSource.CatalogMetadata, request.SelectedLocalModel?.Source);
    }

    [Fact]
    public void Create_IncludesSelectedUnmanagedModelMetadata()
    {
        var selectedModel = new LocalModelPlanSelection(
            "local-depth.safetensors",
            "local-depth.safetensors",
            LocalModelPlanSource.UnmanagedLocalFile);
        var plan = CreatePlan(selectedModel: selectedModel);

        var request = _factory.Create(
            plan,
            TargetDevicePresets.General3dVideo,
            Options(),
            Paths);

        Assert.Equal(selectedModel, request.SelectedLocalModel);
        Assert.Equal("local-depth.safetensors", request.SelectedLocalModel?.DisplayName);
        Assert.Equal(LocalModelPlanSource.UnmanagedLocalFile, request.SelectedLocalModel?.Source);
    }

    [Fact]
    public void Create_WhenNoModelIsSelected_LeavesSelectedModelEmpty()
    {
        var request = _factory.Create(
            CreatePlan(),
            TargetDevicePresets.General3dVideo,
            Options(),
            Paths);

        Assert.Null(request.SelectedLocalModel);
    }

    [Fact]
    public void Create_ReadyRequestCanPassStartGateWhenReadinessIsComplete()
    {
        var request = _factory.Create(
            CreatePlan(
                status: VideoConversionPlanStatus.Ready,
                dryRunReason: ConversionDryRunReason.None),
            TargetDevicePresets.General3dVideo,
            Options(),
            Paths);
        var gate = new ConversionExecutionFeatureGate();

        var startGate = gate.EvaluateStart(
            hasCompletedAnalysis: true,
            hasConversionPlan: true,
            readiness: CompleteReadiness());

        Assert.False(request.IsDryRun);
        Assert.True(startGate.CanStart);
        Assert.Equal(ConversionExecutionBlocker.None, startGate.Blocker);
    }

    [Fact]
    public void Create_PreservesCommandPreviewWithoutAddingModelSyntax()
    {
        var plan = CreatePlan(
            selectedModel: new LocalModelPlanSelection(
                "Default depth model",
                "depth/default-depth.onnx",
                LocalModelPlanSource.CatalogMetadata));

        var request = _factory.Create(
            plan,
            TargetDevicePresets.General3dVideo,
            Options(),
            Paths);

        Assert.Equal(plan.CommandPreview, request.CommandPreview);
        Assert.DoesNotContain("depth/default-depth.onnx", request.CommandPreview);
    }

    private static VideoConversionPlan CreatePlan(
        VideoConversionPlanOptions? options = null,
        LocalModelPlanSelection? selectedModel = null,
        VideoConversionPlanStatus status = VideoConversionPlanStatus.DryRun,
        ConversionDryRunReason dryRunReason = ConversionDryRunReason.MissingLocalAiBundle)
    {
        options ??= Options();

        return new(
            SourcePath: @"C:\Videos\Movie.mp4",
            SuggestedOutputPath: GetOutputPath(options),
            OutputContainer: options.OutputContainer,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: options.ThreeDOutputFormat,
            QualityPreset: options.QualityPreset,
            Intensity: options.Intensity,
            Status: status,
            DryRunReason: dryRunReason,
            Steps:
            [
                new("Read the analyzed source video.", "Leer el video de origen analizado."),
            ],
            CommandPreview: "iw3 local engine dry-run preview")
        {
            SelectedLocalModel = selectedModel,
        };
    }

    private static string GetOutputPath(VideoConversionPlanOptions options)
    {
        var extension = options.OutputContainer switch
        {
            OutputContainer.MKV => "mkv",
            _ => "mp4",
        };
        var layoutSuffix = options.ThreeDOutputFormat switch
        {
            ThreeDOutputFormat.HalfSideBySide => "hsbs",
            ThreeDOutputFormat.Anaglyph => "anaglyph",
            _ => "htab",
        };

        return $@"C:\Videos\Movie.v3dfy.3d.{layoutSuffix}.{extension}";
    }

    private static VideoConversionPlanOptions Options() => new(
        OutputContainer: OutputContainer.MP4,
        QualityPreset: AiQualityPreset.Balanced,
        Intensity: ThreeDIntensity.Medium,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom);

    private static ConversionReadiness CompleteReadiness() => new(
        CanConvert: true,
        EnglishStatus: "ready",
        SpanishStatus: "listo",
        Issues: [],
        EnglishRequiredComponentsSummary: "required",
        SpanishRequiredComponentsSummary: "requeridos");
}
