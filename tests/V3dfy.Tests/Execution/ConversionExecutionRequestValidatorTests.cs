using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;

namespace V3dfy.Tests.Execution;

public sealed class ConversionExecutionRequestValidatorTests
{
    private static readonly InternalToolPaths Paths = new(
        FfmpegExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffmpeg.exe",
        FfprobeExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffprobe.exe",
        PythonExecutable: @"C:\v3dfy\engine\iw3\python\python.exe",
        Iw3EngineDirectory: @"C:\v3dfy\engine\iw3",
        ModelsDirectory: @"C:\v3dfy\engine\iw3\models");

    private readonly ConversionExecutionRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidDryRunRequest_IsValidButNotExecutable()
    {
        var result = _validator.Validate(CreateRequest());

        Assert.True(result.IsValid);
        Assert.True(result.IsDryRun);
        Assert.False(result.CanStartLocalProcess);
        Assert.Equal(
            ConversionExecutionRequestModelState.NoSelectedLocalModel,
            result.ModelState);
    }

    [Fact]
    public void Validate_EmptySourcePath_IsRejected()
    {
        var result = _validator.Validate(CreateRequest(sourcePath: string.Empty));

        AssertIssue(
            result,
            ConversionExecutionRequestValidationIssueKind.SourcePathMissing);
    }

    [Fact]
    public void Validate_EmptyOutputPath_IsRejected()
    {
        var result = _validator.Validate(CreateRequest(outputPath: string.Empty));

        AssertIssue(
            result,
            ConversionExecutionRequestValidationIssueKind.OutputPathMissing);
    }

    [Fact]
    public void Validate_SourceAndOutputSamePath_IsRejected()
    {
        var result = _validator.Validate(CreateRequest(
            sourcePath: @"C:\Videos\Movie.mp4",
            outputPath: @"C:\Videos\.\Movie.mp4"));

        AssertIssue(
            result,
            ConversionExecutionRequestValidationIssueKind.SourceAndOutputPathMatch);
    }

    [Fact]
    public void Validate_MissingCommandPreview_IsRejected()
    {
        var result = _validator.Validate(CreateRequest(commandPreview: " "));

        AssertIssue(
            result,
            ConversionExecutionRequestValidationIssueKind.CommandPreviewMissing);
    }

    [Fact]
    public void Validate_NoSelectedModel_IsHandledSafely()
    {
        var result = _validator.Validate(CreateRequest(selectedModel: null));

        Assert.True(result.IsValid);
        Assert.Equal(
            ConversionExecutionRequestModelState.NoSelectedLocalModel,
            result.ModelState);
    }

    [Fact]
    public void Validate_SelectedModelWithAbsolutePath_IsRejected()
    {
        var result = _validator.Validate(CreateRequest(
            selectedModel: new(
                "Default depth model",
                @"C:\v3dfy\engine\iw3\models\depth.onnx",
                LocalModelPlanSource.CatalogMetadata)));

        AssertIssue(
            result,
            ConversionExecutionRequestValidationIssueKind.SelectedModelPathMustBeRelative);
        Assert.Equal(
            ConversionExecutionRequestModelState.InvalidSelectedLocalModel,
            result.ModelState);
    }

    [Fact]
    public void Validate_SelectedModelWithParentTraversal_IsRejected()
    {
        var result = _validator.Validate(CreateRequest(
            selectedModel: new(
                "Default depth model",
                @"depth\..\other-depth.onnx",
                LocalModelPlanSource.CatalogMetadata)));

        AssertIssue(
            result,
            ConversionExecutionRequestValidationIssueKind.SelectedModelPathContainsParentTraversal);
        Assert.Equal(
            ConversionExecutionRequestModelState.InvalidSelectedLocalModel,
            result.ModelState);
    }

    [Fact]
    public void Validate_SelectedCatalogModelWithSafeRelativePath_IsAccepted()
    {
        var result = _validator.Validate(CreateRequest(
            selectedModel: new(
                "Default depth model",
                "depth/default-depth.onnx",
                LocalModelPlanSource.CatalogMetadata)));

        Assert.True(result.IsValid);
        Assert.Equal(
            ConversionExecutionRequestModelState.SelectedLocalModel,
            result.ModelState);
    }

    [Fact]
    public void Validate_SelectedUnmanagedModelWithSafeRelativePath_IsAccepted()
    {
        var result = _validator.Validate(CreateRequest(
            selectedModel: new(
                "local-depth.safetensors",
                "local-depth.safetensors",
                LocalModelPlanSource.UnmanagedLocalFile)));

        Assert.True(result.IsValid);
        Assert.Equal(
            ConversionExecutionRequestModelState.SelectedLocalModel,
            result.ModelState);
    }

    [Fact]
    public void Validate_RelativeExpectedToolPath_IsRejected()
    {
        var result = _validator.Validate(CreateRequest(paths: Paths with
        {
            PythonExecutable = @"engine\iw3\python\python.exe",
        }));

        AssertIssue(
            result,
            ConversionExecutionRequestValidationIssueKind.ExpectedToolPathNotAbsolute);
    }

    [Fact]
    public void Validate_MissingPlanOptionsAndToolPaths_AreRejected()
    {
        var request = CreateRequest() with
        {
            Plan = null!,
            Options = null!,
            ExpectedToolPaths = null!,
        };

        var result = _validator.Validate(request);

        AssertIssue(result, ConversionExecutionRequestValidationIssueKind.PlanMissing);
        AssertIssue(result, ConversionExecutionRequestValidationIssueKind.OptionsMissing);
        AssertIssue(
            result,
            ConversionExecutionRequestValidationIssueKind.ExpectedToolPathsMissing);
    }

    [Fact]
    public void Validate_MissingSelectedPreset_IsRejected()
    {
        var request = CreateRequest() with
        {
            SelectedPreset = null!,
        };

        var result = _validator.Validate(request);

        AssertIssue(
            result,
            ConversionExecutionRequestValidationIssueKind.SelectedPresetMissing);
    }

    private static ConversionExecutionRequest CreateRequest(
        VideoConversionPlan? plan = null,
        string sourcePath = @"C:\Videos\Movie.mp4",
        string outputPath = @"C:\Videos\Movie.v3dfy.3d.htab.mp4",
        TargetDevicePreset? selectedPreset = null,
        VideoConversionPlanOptions? options = null,
        InternalToolPaths? paths = null,
        LocalModelPlanSelection? selectedModel = null,
        string commandPreview = "iw3 local engine dry-run preview",
        VideoConversionPlanStatus planStatus = VideoConversionPlanStatus.DryRun,
        ConversionDryRunReason dryRunReason = ConversionDryRunReason.MissingLocalAiBundle,
        bool isDryRun = true)
    {
        options ??= Options();
        plan ??= CreatePlan(
            sourcePath,
            outputPath,
            options,
            selectedModel,
            commandPreview,
            planStatus,
            dryRunReason);

        return new(
            Plan: plan,
            SourcePath: sourcePath,
            OutputPath: outputPath,
            SelectedPreset: selectedPreset ?? TargetDevicePresets.General3dVideo,
            Options: options,
            ExpectedToolPaths: paths ?? Paths,
            SelectedLocalModel: selectedModel,
            CommandPreview: commandPreview,
            PlanStatus: planStatus,
            DryRunReason: dryRunReason,
            IsDryRun: isDryRun);
    }

    private static VideoConversionPlan CreatePlan(
        string sourcePath,
        string outputPath,
        VideoConversionPlanOptions options,
        LocalModelPlanSelection? selectedModel,
        string commandPreview,
        VideoConversionPlanStatus status,
        ConversionDryRunReason dryRunReason) => new(
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
        Status: status,
        DryRunReason: dryRunReason,
        Steps:
        [
            new("Read the analyzed source video.", "Leer el video de origen analizado."),
        ],
        CommandPreview: commandPreview)
    {
        SelectedLocalModel = selectedModel,
    };

    private static VideoConversionPlanOptions Options() => new(
        OutputContainer: OutputContainer.MP4,
        QualityPreset: AiQualityPreset.Balanced,
        Intensity: ThreeDIntensity.Medium,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom);

    private static void AssertIssue(
        ConversionExecutionRequestValidationResult result,
        ConversionExecutionRequestValidationIssueKind expectedKind)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Kind == expectedKind);
        Assert.False(result.CanStartLocalProcess);
    }
}
