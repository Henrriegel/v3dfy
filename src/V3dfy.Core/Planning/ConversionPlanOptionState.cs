using V3dfy.Core.Models;
using V3dfy.Core.Recommendations;

namespace V3dfy.Core.Planning;

public sealed class ConversionPlanOptionState
{
    private OutputContainer _outputContainer = OutputContainer.MP4;
    private AiQualityPreset _qualityPreset = AiQualityPreset.Balanced;
    private ThreeDIntensity _intensity = ThreeDIntensity.Medium;
    private ThreeDOutputFormat _threeDOutputFormat = ThreeDOutputFormat.HalfTopBottom;
    private bool _createLgCompatibilityCopy;
    private bool _preferLgCompatibilityCopyWhenOpening;

    public OutputContainer OutputContainer => _outputContainer;

    public AiQualityPreset QualityPreset => _qualityPreset;

    public ThreeDIntensity Intensity => _intensity;

    public ThreeDOutputFormat ThreeDOutputFormat => _threeDOutputFormat;

    public bool CreateLgCompatibilityCopy => _createLgCompatibilityCopy;

    public bool PreferLgCompatibilityCopyWhenOpening =>
        _preferLgCompatibilityCopyWhenOpening;

    public bool HasCustomizedOptions { get; private set; }

    public bool SetOutputContainer(OutputContainer value) =>
        SetCustomizedOption(ref _outputContainer, value);

    public bool SetQualityPreset(AiQualityPreset value) =>
        SetCustomizedOption(ref _qualityPreset, value);

    public bool SetIntensity(ThreeDIntensity value) =>
        SetCustomizedOption(ref _intensity, value);

    public bool SetThreeDOutputFormat(ThreeDOutputFormat value) =>
        SetCustomizedOption(ref _threeDOutputFormat, value);

    public bool SetCreateLgCompatibilityCopy(bool value)
    {
        var changed = SetCustomizedOption(ref _createLgCompatibilityCopy, value);
        if (!value)
        {
            changed |= SetCustomizedOption(
                ref _preferLgCompatibilityCopyWhenOpening,
                false);
        }

        return changed;
    }

    public bool SetPreferLgCompatibilityCopyWhenOpening(bool value) =>
        SetCustomizedOption(ref _preferLgCompatibilityCopyWhenOpening, value);

    public bool ApplyRecommendationDefaultsIfNeeded(
        VideoConversionSetupRecommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        if (HasCustomizedOptions)
        {
            return false;
        }

        return SetRecommendedOptions(
            recommendation.OutputContainer,
            recommendation.QualityPreset,
            recommendation.Intensity,
            recommendation.ThreeDOutputFormat);
    }

    public bool ApplyPresetDefaults(TargetDevicePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        HasCustomizedOptions = false;
        return SetRecommendedOptions(
            preset.Recommendation.OutputContainer,
            AiQualityPreset.Balanced,
            ThreeDIntensity.Medium,
            preset.Recommendation.ThreeDOutputFormat,
            preset.UsesLegacyLgCompatibilityGuidance,
            preset.UsesLegacyLgCompatibilityGuidance);
    }

    public VideoConversionPlanOptions CreatePlanOptions(string? customOutputPath) => new(
        OutputContainer,
        QualityPreset,
        Intensity,
        ThreeDOutputFormat,
        customOutputPath,
        CreateLgCompatibilityCopy,
        PreferLgCompatibilityCopyWhenOpening);

    private bool SetRecommendedOptions(
        OutputContainer outputContainer,
        AiQualityPreset qualityPreset,
        ThreeDIntensity intensity,
        ThreeDOutputFormat threeDOutputFormat,
        bool createLgCompatibilityCopy = false,
        bool preferLgCompatibilityCopyWhenOpening = false)
    {
        var changed = false;
        changed |= SetOption(ref _outputContainer, outputContainer);
        changed |= SetOption(ref _qualityPreset, qualityPreset);
        changed |= SetOption(ref _intensity, intensity);
        changed |= SetOption(ref _threeDOutputFormat, threeDOutputFormat);
        changed |= SetOption(ref _createLgCompatibilityCopy, createLgCompatibilityCopy);
        changed |= SetOption(
            ref _preferLgCompatibilityCopyWhenOpening,
            preferLgCompatibilityCopyWhenOpening);
        return changed;
    }

    private bool SetCustomizedOption<T>(ref T field, T value)
    {
        if (!SetOption(ref field, value))
        {
            return false;
        }

        HasCustomizedOptions = true;
        return true;
    }

    private static bool SetOption<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
