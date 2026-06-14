using V3dfy.Core.Planning;
using V3dfy.Core.Readiness;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Engine.Iw3.Execution;

namespace V3dfy.Tests.Execution;

public sealed class Iw3ConversionReadinessServiceTests
{
    private readonly Iw3ConversionReadinessService _service = new();

    public static TheoryData<string, string, string> KnownOptionalDepthModelData
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var entry in Iw3DepthModelMapper.RegistryEntries.Where(static entry =>
                entry.Availability == Iw3DepthModelAvailability.OptionalImportable &&
                entry.IsReadySelectable))
            {
                data.Add(
                    entry.Key,
                    entry.ExpectedRelativePaths[0],
                    entry.DepthModelName);
            }

            return data;
        }
    }

    public static TheoryData<string, string, string> GatedSafeDepthModelData
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var entry in Iw3DepthModelMapper.RegistryEntries.Where(static entry =>
                entry.IsPublicPackEligible &&
                !entry.IsReadySelectable))
            {
                data.Add(
                    entry.Key,
                    entry.ExpectedRelativePaths[0],
                    entry.DepthModelName);
            }

            return data;
        }
    }

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

    [Theory]
    [MemberData(nameof(KnownOptionalDepthModelData))]
    public void ApplyIw3ExecutionRequirements_MappedOptionalModelKeepsReadinessReady(
        string key,
        string relativePath,
        string depthModelName)
    {
        var readiness = _service.ApplyIw3ExecutionRequirements(
            ReadyReadiness(),
            new(
                FileName(relativePath),
                relativePath,
                LocalModelPlanSource.UnmanagedLocalFile,
                Iw3DepthModelName: depthModelName,
                MappingKey: key));

        Assert.True(readiness.CanConvert);
        Assert.Empty(readiness.Issues);
    }

    [Theory]
    [MemberData(nameof(GatedSafeDepthModelData))]
    public void ApplyIw3ExecutionRequirements_GatedSafeProviderBlocksReadiness(
        string key,
        string relativePath,
        string depthModelName)
    {
        var readiness = _service.ApplyIw3ExecutionRequirements(
            ReadyReadiness(),
            new(
                FileName(relativePath),
                relativePath,
                LocalModelPlanSource.UnmanagedLocalFile,
                Iw3DepthModelName: depthModelName,
                MappingKey: key));

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage ==
                "Selected local model is not mapped to a verified iw3 depth model yet.");
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
    public void ApplyIw3ExecutionRequirements_MissingSelectedModelBlocksReadiness()
    {
        var readiness = _service.ApplyIw3ExecutionRequirements(
            ReadyReadiness(),
            null);

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

    private static string FileName(string relativePath) =>
        relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();
}
