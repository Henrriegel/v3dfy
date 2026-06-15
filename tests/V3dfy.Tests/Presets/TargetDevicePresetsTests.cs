using V3dfy.Core.Models;
using V3dfy.Core.Presets;

namespace V3dfy.Tests.Presets;

public sealed class TargetDevicePresetsTests
{
    [Fact]
    public void All_DefaultsToRecommended3dTv()
    {
        Assert.Same(TargetDevicePresets.Recommended3dTv, TargetDevicePresets.All[0]);
        Assert.Equal("recommended-3d-tv", TargetDevicePresets.Recommended3dTv.Id);
        Assert.Equal("Recommended 3D TV", TargetDevicePresets.Recommended3dTv.Name);
        Assert.Equal("TV 3D recomendada", TargetDevicePresets.Recommended3dTv.SpanishName);
        Assert.Same(TargetDevicePresets.Recommended3dTv, TargetDevicePresets.General3dVideo);
    }

    [Fact]
    public void Recommended3dTv_RecommendsMp4HalfTopBottomDefaults()
    {
        Assert.Equal(
            OutputContainer.MP4,
            TargetDevicePresets.Recommended3dTv.Recommendation.OutputContainer);
        Assert.Equal(
            ThreeDOutputFormat.HalfTopBottom,
            TargetDevicePresets.Recommended3dTv.Recommendation.ThreeDOutputFormat);
        Assert.False(TargetDevicePresets.Recommended3dTv.UsesLegacyLgCompatibilityGuidance);
        Assert.Equal(TargetDevicePresetCategory.Recommended, TargetDevicePresets.Recommended3dTv.Category);
        Assert.Contains("Default general-purpose", TargetDevicePresets.Recommended3dTv.Description);
    }

    [Fact]
    public void Lg3dFullHd2012_IsLegacyAndNotDefault()
    {
        Assert.NotSame(TargetDevicePresets.Lg3dFullHd2012, TargetDevicePresets.All[0]);
        Assert.Equal(TargetDevicePresetCategory.Legacy, TargetDevicePresets.Lg3dFullHd2012.Category);
        Assert.Contains("Legacy", TargetDevicePresets.Lg3dFullHd2012.Name);
        Assert.Contains("optional Full HD MP4 copy", TargetDevicePresets.Lg3dFullHd2012.CompatibilityNote);
        Assert.Contains("Side-by-Side", TargetDevicePresets.Lg3dFullHd2012.PlaybackInstructions);
    }

    [Fact]
    public void MaximumCompatibilityAndHighQualityMasterUseDifferentSettings()
    {
        Assert.Equal(OutputContainer.MP4, TargetDevicePresets.MaximumCompatibility.Recommendation.OutputContainer);
        Assert.Equal(OutputContainer.MKV, TargetDevicePresets.HighQualityMaster.Recommendation.OutputContainer);
        Assert.True(
            TargetDevicePresets.HighQualityMaster.EstimatedVideoBitrateHighMbps >
            TargetDevicePresets.MaximumCompatibility.EstimatedVideoBitrateHighMbps);
    }

    [Fact]
    public void Resolve_MapsOldLgIdToLegacyPresetAndUnknownToRecommended()
    {
        Assert.Same(
            TargetDevicePresets.Lg3dFullHd2012,
            TargetDevicePresets.Resolve("lg-3d-full-hd-2012"));
        Assert.Same(
            TargetDevicePresets.Lg3dFullHd2012,
            TargetDevicePresets.Resolve("LG 3D Full HD 2012"));
        Assert.Same(
            TargetDevicePresets.Recommended3dTv,
            TargetDevicePresets.Resolve("missing"));
    }

    [Fact]
    public void UnsupportedVrPreset_IsNotExposed()
    {
        Assert.DoesNotContain(
            TargetDevicePresets.All,
            preset => preset.Name.Contains("VR", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Lg3dFullHd2012_RecommendsMp4()
    {
        Assert.Equal(
            OutputContainer.MP4,
            TargetDevicePresets.Lg3dFullHd2012.Recommendation.OutputContainer);
    }

    [Fact]
    public void Lg3dFullHd2012_RecommendsHalfSideBySide()
    {
        Assert.Equal(
            ThreeDOutputFormat.HalfSideBySide,
            TargetDevicePresets.Lg3dFullHd2012.Recommendation.ThreeDOutputFormat);
    }

    [Fact]
    public void Lg3dFullHd2012_KeepsMkvAsAdvancedOption()
    {
        Assert.Contains(
            OutputContainer.MKV,
            TargetDevicePresets.Lg3dFullHd2012.AdvancedOutputContainers);
    }

    [Fact]
    public void OutputContainerOptions_IncludeOnlyPrimaryMp4AndMkv()
    {
        Assert.Equal(
            [OutputContainer.MP4, OutputContainer.MKV],
            OutputContainerOptions.All);
    }

    [Fact]
    public void Lg3dFullHd2012_DoesNotExposeAviAsAdvancedPrimaryOption()
    {
        Assert.DoesNotContain(
            TargetDevicePresets.Lg3dFullHd2012.AdvancedOutputContainers,
            container => container.ToString().Equals("AVI", StringComparison.Ordinal));
        Assert.Contains(
            OutputContainer.MKV,
            TargetDevicePresets.Lg3dFullHd2012.AdvancedOutputContainers);
    }
}
