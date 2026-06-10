using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Recommendations;

namespace V3dfy.Tests.Planning;

public sealed class ConversionPlanOptionStateTests
{
    [Fact]
    public void Defaults_MatchGeneralConversionDefaults()
    {
        var state = new ConversionPlanOptionState();

        Assert.Equal(OutputContainer.MP4, state.OutputContainer);
        Assert.Equal(AiQualityPreset.Balanced, state.QualityPreset);
        Assert.Equal(ThreeDIntensity.Medium, state.Intensity);
        Assert.Equal(ThreeDOutputFormat.HalfTopBottom, state.ThreeDOutputFormat);
        Assert.False(state.CreateLgCompatibilityCopy);
        Assert.False(state.PreferLgCompatibilityCopyWhenOpening);
        Assert.False(state.HasCustomizedOptions);
    }

    [Fact]
    public void SetOutputContainer_ChangedValue_MarksOptionsCustomized()
    {
        var state = new ConversionPlanOptionState();

        var changed = state.SetOutputContainer(OutputContainer.MKV);

        Assert.True(changed);
        Assert.Equal(OutputContainer.MKV, state.OutputContainer);
        Assert.True(state.HasCustomizedOptions);
    }

    [Fact]
    public void SetQualityPreset_SameValue_DoesNotMarkCustomized()
    {
        var state = new ConversionPlanOptionState();

        var changed = state.SetQualityPreset(AiQualityPreset.Balanced);

        Assert.False(changed);
        Assert.False(state.HasCustomizedOptions);
    }

    [Fact]
    public void ApplyRecommendationDefaultsIfNeeded_NotCustomized_AppliesRecommendation()
    {
        var state = new ConversionPlanOptionState();
        var recommendation = CreateRecommendation(
            OutputContainer.MKV,
            AiQualityPreset.HighQuality,
            ThreeDIntensity.High,
            ThreeDOutputFormat.Anaglyph);

        var changed = state.ApplyRecommendationDefaultsIfNeeded(recommendation);

        Assert.True(changed);
        Assert.Equal(OutputContainer.MKV, state.OutputContainer);
        Assert.Equal(AiQualityPreset.HighQuality, state.QualityPreset);
        Assert.Equal(ThreeDIntensity.High, state.Intensity);
        Assert.Equal(ThreeDOutputFormat.Anaglyph, state.ThreeDOutputFormat);
        Assert.False(state.CreateLgCompatibilityCopy);
        Assert.False(state.PreferLgCompatibilityCopyWhenOpening);
        Assert.False(state.HasCustomizedOptions);
    }

    [Fact]
    public void ApplyRecommendationDefaultsIfNeeded_Customized_PreservesExistingOptions()
    {
        var state = new ConversionPlanOptionState();
        state.SetThreeDOutputFormat(ThreeDOutputFormat.HalfSideBySide);
        var recommendation = CreateRecommendation(
            OutputContainer.MKV,
            AiQualityPreset.HighQuality,
            ThreeDIntensity.High,
            ThreeDOutputFormat.Anaglyph);

        var changed = state.ApplyRecommendationDefaultsIfNeeded(recommendation);

        Assert.False(changed);
        Assert.Equal(OutputContainer.MP4, state.OutputContainer);
        Assert.Equal(AiQualityPreset.Balanced, state.QualityPreset);
        Assert.Equal(ThreeDIntensity.Medium, state.Intensity);
        Assert.Equal(ThreeDOutputFormat.HalfSideBySide, state.ThreeDOutputFormat);
        Assert.True(state.HasCustomizedOptions);
    }

    [Fact]
    public void ApplyPresetDefaults_ResetsCustomizationAndUsesPresetDefaults()
    {
        var state = new ConversionPlanOptionState();
        state.SetQualityPreset(AiQualityPreset.HighQuality);
        var preset = CreatePreset(
            OutputContainer.MKV,
            ThreeDOutputFormat.HalfSideBySide,
            usesLegacyLgCompatibilityGuidance: true);

        var changed = state.ApplyPresetDefaults(preset);

        Assert.True(changed);
        Assert.Equal(OutputContainer.MKV, state.OutputContainer);
        Assert.Equal(AiQualityPreset.Balanced, state.QualityPreset);
        Assert.Equal(ThreeDIntensity.Medium, state.Intensity);
        Assert.Equal(ThreeDOutputFormat.HalfSideBySide, state.ThreeDOutputFormat);
        Assert.True(state.CreateLgCompatibilityCopy);
        Assert.True(state.PreferLgCompatibilityCopyWhenOpening);
        Assert.False(state.HasCustomizedOptions);
    }

    [Fact]
    public void SetCreateLgCompatibilityCopy_ChangedValue_MarksOptionsCustomized()
    {
        var state = new ConversionPlanOptionState();

        var changed = state.SetCreateLgCompatibilityCopy(true);

        Assert.True(changed);
        Assert.True(state.CreateLgCompatibilityCopy);
        Assert.True(state.HasCustomizedOptions);
    }

    [Fact]
    public void SetCreateLgCompatibilityCopy_DisablingAlsoClearsPreferredOpenTarget()
    {
        var state = new ConversionPlanOptionState();
        state.ApplyPresetDefaults(TargetDevicePresets.Lg3dFullHd2012);

        var changed = state.SetCreateLgCompatibilityCopy(false);

        Assert.True(changed);
        Assert.False(state.CreateLgCompatibilityCopy);
        Assert.False(state.PreferLgCompatibilityCopyWhenOpening);
        Assert.True(state.HasCustomizedOptions);
    }

    [Fact]
    public void ApplyPresetDefaults_SwitchingFromLgToGeneral_DisablesLgCopyBehavior()
    {
        var state = new ConversionPlanOptionState();
        state.ApplyPresetDefaults(TargetDevicePresets.Lg3dFullHd2012);

        Assert.True(state.CreateLgCompatibilityCopy);
        Assert.True(state.PreferLgCompatibilityCopyWhenOpening);

        state.ApplyPresetDefaults(TargetDevicePresets.General3dVideo);

        Assert.False(state.CreateLgCompatibilityCopy);
        Assert.False(state.PreferLgCompatibilityCopyWhenOpening);
        Assert.False(state.HasCustomizedOptions);
    }

    [Fact]
    public void CreatePlanOptions_PreservesCustomOutputPathExactly()
    {
        var state = new ConversionPlanOptionState();
        state.SetOutputContainer(OutputContainer.MKV);
        state.SetIntensity(ThreeDIntensity.High);
        var customOutputPath = TestPaths.OutputRoot("Manual", "Movie custom.output");

        var options = state.CreatePlanOptions(customOutputPath);

        Assert.Equal(OutputContainer.MKV, options.OutputContainer);
        Assert.Equal(AiQualityPreset.Balanced, options.QualityPreset);
        Assert.Equal(ThreeDIntensity.High, options.Intensity);
        Assert.Equal(ThreeDOutputFormat.HalfTopBottom, options.ThreeDOutputFormat);
        Assert.Equal(customOutputPath, options.CustomOutputPath);
        Assert.False(options.CreateLgCompatibilityCopy);
        Assert.False(options.PreferLgCompatibilityCopyWhenOpening);
    }

    private static VideoConversionSetupRecommendation CreateRecommendation(
        OutputContainer outputContainer,
        AiQualityPreset qualityPreset,
        ThreeDIntensity intensity,
        ThreeDOutputFormat outputFormat) => new(
        OutputContainer: outputContainer,
        VideoCodec: "H.264",
        AudioCodec: "AAC or AC3",
        Width: 1920,
        Height: 1080,
        ThreeDOutputFormat: outputFormat,
        QualityPreset: qualityPreset,
        Intensity: intensity,
        IsSourceAboveTargetResolution: false,
        IsHdrSource: false,
        UseTvCompatibleMp4: outputContainer == OutputContainer.MP4,
        SuggestMkvMasterOutput: outputContainer == OutputContainer.MKV,
        CompatibilityIssues: []);

    private static TargetDevicePreset CreatePreset(
        OutputContainer outputContainer,
        ThreeDOutputFormat outputFormat,
        bool usesLegacyLgCompatibilityGuidance = false) => new(
        Name: "Test preset",
        SpanishName: "Perfil de prueba",
        Recommendation: new ConversionRecommendation(
            outputContainer,
            "H.264",
            "AAC or AC3",
            1920,
            1080,
            outputFormat,
            "use selected layout"),
        AdvancedOutputContainers: [],
        PlaybackTitle: "Playback",
        SpanishPlaybackTitle: "Reproduccion",
        PlaybackInstructions: "Use a compatible player.",
        SpanishPlaybackInstructions: "Usa un reproductor compatible.",
        Description: "Test preset.",
        SpanishDescription: "Perfil de prueba.",
        BestFor: "Tests.",
        SpanishBestFor: "Pruebas.",
        CompatibilityNote: "Test note.",
        SpanishCompatibilityNote: "Nota de prueba.",
        UsesLegacyLgCompatibilityGuidance: usesLegacyLgCompatibilityGuidance);
}
