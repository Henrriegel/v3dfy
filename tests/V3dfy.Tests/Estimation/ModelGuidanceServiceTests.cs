using V3dfy.Core.Estimation;

namespace V3dfy.Tests.Estimation;

public sealed class ModelGuidanceServiceTests
{
    private readonly ModelGuidanceService service = new();

    [Fact]
    public void Create_AnyV2SmallIsRecommendedFirstOptionalModel()
    {
        var guidance = service.Create(
            "depth-anything-v2-small",
            "Any_V2_S",
            "Depth Anything V2 Small",
            isEmbeddedBase: false);

        Assert.True(guidance.IsRecommendedFirstOptionalModel);
        Assert.Contains("Recommended first optional", guidance.EnglishHeadline);
        Assert.Equal("Fast", guidance.EnglishSpeed);
    }

    [Fact]
    public void Create_DepthAnything3MonoLargeIsSlowLargeExperimental()
    {
        var guidance = service.Create(
            "depth-anything-3-mono-large",
            "Any_V3_Mono",
            "Depth Anything 3 Mono Large",
            isEmbeddedBase: false);

        Assert.True(guidance.IsExperimental);
        Assert.Equal("Slow", guidance.EnglishSpeed);
        Assert.Equal("Large", guidance.EnglishSize);
    }

    [Fact]
    public void Create_IndoorAndOutdoorModelsGetSceneGuidance()
    {
        var indoor = service.Create(
            "depth-anything-metric-indoor",
            "ZoeD_Any_N",
            "Depth Anything Metric Indoor",
            isEmbeddedBase: false);
        var outdoor = service.Create(
            "depth-anything-metric-outdoor",
            "ZoeD_Any_K",
            "Depth Anything Metric Outdoor",
            isEmbeddedBase: false);

        Assert.Contains("Indoor", indoor.EnglishHeadline);
        Assert.Contains("Outdoor", outdoor.EnglishHeadline);
    }

    [Fact]
    public void Create_BaseModelRemainsUsable()
    {
        var guidance = service.Create(
            "depth-anything-metric-indoor",
            "ZoeD_Any_N",
            "Depth Anything Metric Indoor",
            isEmbeddedBase: true);

        Assert.True(guidance.IsBaseModel);
        Assert.Contains("Included base", guidance.EnglishHeadline);
        Assert.Equal("Usable", guidance.EnglishQuality);
    }

    [Fact]
    public void Create_MissingOptionalMetadataFallsBackSafely()
    {
        var guidance = service.Create(null, null, null, isEmbeddedBase: false);

        Assert.False(guidance.IsRecommendedFirstOptionalModel);
        Assert.False(string.IsNullOrWhiteSpace(guidance.EnglishHeadline));
    }
}
