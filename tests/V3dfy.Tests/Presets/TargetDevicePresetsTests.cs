using V3dfy.Core.Models;
using V3dfy.Core.Presets;

namespace V3dfy.Tests.Presets;

public sealed class TargetDevicePresetsTests
{
    [Fact]
    public void Lg3dFullHd2012_RecommendsMp4()
    {
        Assert.Equal(
            OutputContainer.MP4,
            TargetDevicePresets.Lg3dFullHd2012.Recommendation.OutputContainer);
    }

    [Fact]
    public void Lg3dFullHd2012_RecommendsHalfTopBottom()
    {
        Assert.Equal(
            ThreeDOutputFormat.HalfTopBottom,
            TargetDevicePresets.Lg3dFullHd2012.Recommendation.ThreeDOutputFormat);
    }

    [Fact]
    public void Lg3dFullHd2012_KeepsMkvAsAdvancedOption()
    {
        Assert.Contains(
            OutputContainer.MKV,
            TargetDevicePresets.Lg3dFullHd2012.AdvancedOutputContainers);
    }
}
