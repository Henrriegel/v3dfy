using V3dfy.Core.Models;
using V3dfy.Core.Readiness;

namespace V3dfy.Tests.Readiness;

public sealed class ConversionReadinessServiceTests
{
    private readonly ConversionReadinessService _service = new();

    [Fact]
    public void Evaluate_WhenPythonIsMissing_BlocksConversion()
    {
        var readiness = _service.Evaluate(CompleteHealth() with
        {
            Python = ToolHealthStatus.Missing,
        });

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage == "Embedded Python runtime is missing.");
    }

    [Fact]
    public void Evaluate_WhenIw3EngineIsMissing_BlocksConversion()
    {
        var readiness = _service.Evaluate(CompleteHealth() with
        {
            Iw3EngineDirectory = ToolHealthStatus.Missing,
        });

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage == "Local iw3 engine is missing.");
    }

    [Fact]
    public void Evaluate_WhenModelsAreMissing_BlocksConversion()
    {
        var readiness = _service.Evaluate(CompleteHealth() with
        {
            ModelsDirectory = ToolHealthStatus.Missing,
        });

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage == "Local 3D models are missing.");
    }

    [Fact]
    public void Evaluate_WhenFfmpegOrFfprobeAreMissing_BlocksConversion()
    {
        var readiness = _service.Evaluate(CompleteHealth() with
        {
            Ffmpeg = ToolHealthStatus.Missing,
            Ffprobe = ToolHealthStatus.Missing,
        });

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage == "FFmpeg is missing.");
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage == "FFprobe is missing.");
    }

    [Fact]
    public void Evaluate_WhenAllRequiredComponentsAreFound_AllowsConversion()
    {
        var readiness = _service.Evaluate(CompleteHealth());

        Assert.True(readiness.CanConvert);
        Assert.Empty(readiness.Issues);
    }

    private static EngineHealthStatus CompleteHealth() => new(
        Ffmpeg: ToolHealthStatus.Found,
        Ffprobe: ToolHealthStatus.Found,
        Python: ToolHealthStatus.Found,
        Iw3EngineDirectory: ToolHealthStatus.Found,
        ModelsDirectory: ToolHealthStatus.Found);
}
