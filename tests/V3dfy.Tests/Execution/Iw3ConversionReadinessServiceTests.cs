using V3dfy.Core.Planning;
using V3dfy.Core.Readiness;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Engine.Iw3.Execution;

namespace V3dfy.Tests.Execution;

public sealed class Iw3ConversionReadinessServiceTests
{
    private readonly Iw3ConversionReadinessService _service = new();

    [Fact]
    public void ApplyIw3ExecutionRequirements_MappedModelKeepsReadinessReady()
    {
        var readiness = _service.ApplyIw3ExecutionRequirements(
            ReadyReadiness(),
            new(
                "depth_anything_metric_depth_indoor.pt",
                Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
                LocalModelPlanSource.UnmanagedLocalFile));

        Assert.True(readiness.CanConvert);
        Assert.Empty(readiness.Issues);
    }

    [Fact]
    public void ApplyIw3ExecutionRequirements_UnmappedModelBlocksReadiness()
    {
        var readiness = _service.ApplyIw3ExecutionRequirements(
            ReadyReadiness(),
            new(
                "Default depth model",
                "depth/default-depth.onnx",
                LocalModelPlanSource.CatalogMetadata));

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage ==
                "Selected local model is not mapped to a verified iw3 depth model yet.");
    }

    [Fact]
    public void ApplyIw3ExecutionRequirements_IncompleteReadinessKeepsExistingIssues()
    {
        var readiness = _service.ApplyIw3ExecutionRequirements(
            BlockedReadiness(),
            null);

        Assert.False(readiness.CanConvert);
        Assert.Single(readiness.Issues);
        Assert.Equal("missing", readiness.Issues[0].EnglishMessage);
    }

    private static ConversionReadiness ReadyReadiness() => new(
        CanConvert: true,
        EnglishStatus: "ready",
        SpanishStatus: "listo",
        Issues: [],
        EnglishRequiredComponentsSummary: "required",
        SpanishRequiredComponentsSummary: "requeridos");

    private static ConversionReadiness BlockedReadiness() => new(
        CanConvert: false,
        EnglishStatus: "blocked",
        SpanishStatus: "bloqueado",
        Issues: [new("missing", "faltante")],
        EnglishRequiredComponentsSummary: "required",
        SpanishRequiredComponentsSummary: "requeridos");
}
