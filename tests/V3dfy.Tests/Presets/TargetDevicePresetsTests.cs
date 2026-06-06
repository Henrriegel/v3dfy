using V3dfy.Core.Models;
using V3dfy.Core.Presets;

namespace V3dfy.Tests.Presets;

public sealed class TargetDevicePresetsTests
{
    [Fact]
    public void All_DefaultsToGeneral3dVideo()
    {
        Assert.Same(TargetDevicePresets.General3dVideo, TargetDevicePresets.All[0]);
        Assert.Equal("General 3D video", TargetDevicePresets.General3dVideo.Name);
        Assert.Equal("Video 3D general", TargetDevicePresets.General3dVideo.SpanishName);
    }

    [Fact]
    public void General3dVideo_RecommendsNeutralMp4HalfTopBottomDefaults()
    {
        Assert.Equal(
            OutputContainer.MP4,
            TargetDevicePresets.General3dVideo.Recommendation.OutputContainer);
        Assert.Equal(
            ThreeDOutputFormat.HalfTopBottom,
            TargetDevicePresets.General3dVideo.Recommendation.ThreeDOutputFormat);
        Assert.False(TargetDevicePresets.General3dVideo.UsesLegacyLgCompatibilityGuidance);
        Assert.Contains("Neutral output profile", TargetDevicePresets.General3dVideo.Description);
        Assert.Contains("broad compatibility", TargetDevicePresets.General3dVideo.CompatibilityNote);
    }

    [Fact]
    public void Lg3dFullHd2012_DescribesDeviceSpecificPlaybackGuidance()
    {
        Assert.Contains("Device-specific", TargetDevicePresets.Lg3dFullHd2012.Description);
        Assert.Contains("optional Full HD MP4 copy", TargetDevicePresets.Lg3dFullHd2012.CompatibilityNote);
        Assert.Contains("Side-by-Side", TargetDevicePresets.Lg3dFullHd2012.PlaybackInstructions);
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
