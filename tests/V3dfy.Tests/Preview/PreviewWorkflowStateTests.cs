using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Preview;

namespace V3dfy.Tests.Preview;

public sealed class PreviewWorkflowStateTests
{
    [Fact]
    public void MarkOutdatedIfConfigurationChanged_SourceModelLayoutIntensityProfileAndContainerAffectState()
    {
        var original = CreateConfiguration();
        var state = PreviewWorkflowState
            .NotGenerated(original.PreviewStartTime, original.PreviewDuration)
            .Generating(original, DateTimeOffset.UtcNow)
            .Complete(new(
                Success: true,
                WasCanceled: false,
                Status: PreviewGenerationStatus.Ready,
                PreviewOutputPath: @"C:\cache\preview.mp4",
                CachePaths: new(@"C:\cache", @"C:\cache\source.mp4", @"C:\cache\source.part.mp4", @"C:\cache\preview.mp4", @"C:\cache\preview.part.mp4"),
                StartedAt: DateTimeOffset.UtcNow,
                FinishedAt: DateTimeOffset.UtcNow,
                EnglishSummary: "Preview generated successfully.",
                SpanishSummary: "La vista previa se genero correctamente.",
                Logs: []),
                original);

        Assert.Equal(PreviewGenerationStatus.Outdated, state.MarkOutdatedIfConfigurationChanged(
            original with { SourcePath = @"D:\Videos\Other.mp4" }).Status);
        Assert.Equal(PreviewGenerationStatus.Outdated, state.MarkOutdatedIfConfigurationChanged(
            original with { ModelRelativePath = "other.pt" }).Status);
        Assert.Equal(PreviewGenerationStatus.Outdated, state.MarkOutdatedIfConfigurationChanged(
            original with { ThreeDOutputFormat = ThreeDOutputFormat.HalfSideBySide }).Status);
        Assert.Equal(PreviewGenerationStatus.Outdated, state.MarkOutdatedIfConfigurationChanged(
            original with { Intensity = ThreeDIntensity.High }).Status);
        Assert.Equal(PreviewGenerationStatus.Outdated, state.MarkOutdatedIfConfigurationChanged(
            original with { OutputProfileName = "General 3D video" }).Status);
        Assert.Equal(PreviewGenerationStatus.Outdated, state.MarkOutdatedIfConfigurationChanged(
            original with { OutputContainer = OutputContainer.MKV }).Status);
    }

    [Fact]
    public void MarkOutdatedIfConfigurationChanged_CurrentConfigurationRemainsReady()
    {
        var configuration = CreateConfiguration();
        var state = PreviewWorkflowState
            .NotGenerated(configuration.PreviewStartTime, configuration.PreviewDuration)
            .Generating(configuration, DateTimeOffset.UtcNow)
            .Complete(new(
                Success: true,
                WasCanceled: false,
                Status: PreviewGenerationStatus.Ready,
                PreviewOutputPath: @"C:\cache\preview.mp4",
                CachePaths: new(@"C:\cache", @"C:\cache\source.mp4", @"C:\cache\source.part.mp4", @"C:\cache\preview.mp4", @"C:\cache\preview.part.mp4"),
                StartedAt: DateTimeOffset.UtcNow,
                FinishedAt: DateTimeOffset.UtcNow,
                EnglishSummary: "Preview generated successfully.",
                SpanishSummary: "La vista previa se genero correctamente.",
                Logs: []),
                configuration);

        Assert.Equal(PreviewGenerationStatus.Ready, state.MarkOutdatedIfConfigurationChanged(configuration).Status);
    }

    [Fact]
    public void Accept_CurrentReadyPreviewMarksPreviewAccepted()
    {
        var configuration = CreateConfiguration();
        var state = ReadyState(configuration);

        var accepted = state.Accept(configuration);

        Assert.Equal(PreviewGenerationStatus.Accepted, accepted.Status);
        Assert.True(accepted.IsAcceptedCurrent(configuration));
    }

    [Fact]
    public void MarkOutdatedIfConfigurationChanged_AcceptedPreviewBecomesOutdated()
    {
        var configuration = CreateConfiguration();
        var accepted = ReadyState(configuration).Accept(configuration);

        var outdated = accepted.MarkOutdatedIfConfigurationChanged(
            configuration with { QualityPreset = AiQualityPreset.HighQuality });

        Assert.Equal(PreviewGenerationStatus.Outdated, outdated.Status);
        Assert.False(outdated.IsAccepted);
    }

    [Fact]
    public void UpdateForCurrentConfiguration_IntensityChangeOutdatesAndChangingBackRestoresAccepted()
    {
        AssertAcceptedPreviewOutdatesThenRestores(
            configuration => configuration with { Intensity = ThreeDIntensity.High });
    }

    [Fact]
    public void UpdateForCurrentConfiguration_ModelChangeOutdatesAndChangingBackRestoresAccepted()
    {
        AssertAcceptedPreviewOutdatesThenRestores(
            configuration => configuration with
            {
                ModelKey = "other-model",
                ModelDisplayName = "Other model",
                ModelRelativePath = "hub/checkpoints/other.pt",
                Iw3DepthModelName = "ZoeD_N",
            });
    }

    [Fact]
    public void UpdateForCurrentConfiguration_LayoutChangeOutdatesAndChangingBackRestoresAccepted()
    {
        AssertAcceptedPreviewOutdatesThenRestores(
            configuration => configuration with
            {
                ThreeDOutputFormat = ThreeDOutputFormat.HalfSideBySide,
            });
    }

    [Fact]
    public void UpdateForCurrentConfiguration_DoesNotRestoreAcceptedWhenPreviewFileIsMissing()
    {
        var configuration = CreateConfiguration();
        var accepted = ReadyState(configuration).Accept(configuration);
        var outdated = accepted.UpdateForCurrentConfiguration(
            configuration with { Intensity = ThreeDIntensity.High },
            previewFileExists: true);

        var stillOutdated = outdated.UpdateForCurrentConfiguration(
            configuration,
            previewFileExists: false);

        Assert.Equal(PreviewGenerationStatus.Outdated, stillOutdated.Status);
    }

    [Fact]
    public void Create_FingerprintExcludesSuggestedOutputPath()
    {
        var range = new PreviewTimeRange(
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(15));
        var automaticOutput = PreviewConfigurationSnapshot.Create(
            CreatePlan(@"D:\Videos\Movie.v3dfy.3d.htab.mp4"),
            TargetDevicePresets.Lg3dFullHd2012,
            range);
        var customOutput = PreviewConfigurationSnapshot.Create(
            CreatePlan(@"E:\Converted\ManualName.mp4"),
            TargetDevicePresets.Lg3dFullHd2012,
            range);

        Assert.Equal(automaticOutput.Fingerprint, customOutput.Fingerprint);
    }

    private static void AssertAcceptedPreviewOutdatesThenRestores(
        Func<PreviewConfigurationSnapshot, PreviewConfigurationSnapshot> mutate)
    {
        var configuration = CreateConfiguration();
        var accepted = ReadyState(configuration).Accept(configuration);

        var outdated = accepted.UpdateForCurrentConfiguration(
            mutate(configuration),
            previewFileExists: true);
        var restored = outdated.UpdateForCurrentConfiguration(
            configuration,
            previewFileExists: true);

        Assert.Equal(PreviewGenerationStatus.Outdated, outdated.Status);
        Assert.False(outdated.IsAccepted);
        Assert.Equal(PreviewGenerationStatus.Accepted, restored.Status);
        Assert.True(restored.IsAcceptedCurrent(configuration));
    }

    private static PreviewConfigurationSnapshot CreateConfiguration() => new(
        SourcePath: @"D:\Videos\Movie.mp4",
        OutputProfileName: "LG 3D Full HD 2012",
        OutputContainer: OutputContainer.MP4,
        QualityPreset: AiQualityPreset.Balanced,
        Intensity: ThreeDIntensity.Medium,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
        ModelKey: "depth-anything-metric-indoor",
        ModelDisplayName: "Depth Anything Metric Indoor",
        ModelRelativePath: "hub/checkpoints/depth_anything_metric_depth_indoor.pt",
        Iw3DepthModelName: "ZoeD_Any_N",
        PreviewStartTime: TimeSpan.FromMinutes(10),
        PreviewDuration: TimeSpan.FromSeconds(15));

    private static PreviewWorkflowState ReadyState(
        PreviewConfigurationSnapshot configuration) => PreviewWorkflowState
        .NotGenerated(configuration.PreviewStartTime, configuration.PreviewDuration)
        .Generating(configuration, DateTimeOffset.UtcNow)
        .Complete(new(
            Success: true,
            WasCanceled: false,
            Status: PreviewGenerationStatus.Ready,
            PreviewOutputPath: @"C:\cache\preview.mp4",
            CachePaths: new(@"C:\cache", @"C:\cache\source.mp4", @"C:\cache\source.part.mp4", @"C:\cache\preview.mp4", @"C:\cache\preview.part.mp4"),
            StartedAt: DateTimeOffset.UtcNow,
            FinishedAt: DateTimeOffset.UtcNow,
            EnglishSummary: "Preview generated successfully.",
            SpanishSummary: "La vista previa se genero correctamente.",
            Logs: []),
            configuration);

    private static VideoConversionPlan CreatePlan(string suggestedOutputPath) => new(
        SourcePath: @"D:\Videos\Movie.mp4",
        SuggestedOutputPath: suggestedOutputPath,
        OutputContainer: OutputContainer.MP4,
        VideoCodec: "H.264",
        AudioCodec: "AAC",
        Width: 1920,
        Height: 1080,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
        QualityPreset: AiQualityPreset.Balanced,
        Intensity: ThreeDIntensity.Medium,
        Status: VideoConversionPlanStatus.Ready,
        DryRunReason: ConversionDryRunReason.None,
        Steps: [],
        CommandPreview: "iw3 preview")
    {
        SelectedLocalModel = new(
            "Depth Anything Metric Indoor",
            "hub/checkpoints/depth_anything_metric_depth_indoor.pt",
            LocalModelPlanSource.UnmanagedLocalFile,
            Iw3DepthModelName: "ZoeD_Any_N",
            MappingKey: "depth-anything-metric-indoor"),
    };
}
