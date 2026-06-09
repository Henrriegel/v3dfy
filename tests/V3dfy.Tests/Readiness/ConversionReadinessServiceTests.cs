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
    public void Evaluate_WhenIw3RuntimeDependencyIsMissing_BlocksConversion()
    {
        var readiness = _service.Evaluate(CompleteHealth() with
        {
            Iw3RuntimeDependencies = ToolHealthStatus.Missing,
        });

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage == "Missing iw3 runtime dependency.");
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

    [Fact]
    public void Evaluate_DetailedHealth_IncludesExpectedLocalPaths()
    {
        var readiness = _service.Evaluate(new EngineDependencyHealth(
            Ffmpeg: FoundDependency(@"C:\v3dfy\tools\ffmpeg\win-x64\ffmpeg.exe"),
            Ffprobe: FoundDependency(@"C:\v3dfy\tools\ffmpeg\win-x64\ffprobe.exe"),
            Python: MissingDependency(
                ToolHealthDetailKind.BundledFileMissing,
                @"C:\v3dfy\engine\iw3\python\python.exe"),
            Iw3EngineDirectory: MissingDependency(
                ToolHealthDetailKind.EnginePlaceholderOnly,
                @"C:\v3dfy\engine\iw3"),
            ModelsDirectory: MissingDependency(
                ToolHealthDetailKind.ModelFilesMissing,
                @"C:\v3dfy\engine\iw3\nunif\iw3\pretrained_models"))
        {
            Iw3RuntimeDependencies = MissingDependency(
                ToolHealthDetailKind.Iw3RuntimeDependenciesMissing,
                @"C:\v3dfy\engine\iw3\nunif\iw3\pretrained_models\hub\checkpoints\iw3_row_flow_v3_20250627.pth"),
        });

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage.Contains(
                @"C:\v3dfy\engine\iw3\python\python.exe",
                StringComparison.Ordinal));
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage.Contains(
                "Engine directory exists but only placeholder or contract files were detected",
                StringComparison.Ordinal));
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage.Contains(
                @"C:\v3dfy\engine\iw3\nunif\iw3\pretrained_models",
                StringComparison.Ordinal));
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage.Contains(
                "Missing iw3 runtime dependency",
                StringComparison.Ordinal) &&
                issue.EnglishMessage.Contains(
                    "iw3_row_flow_v3_20250627.pth",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_DetailedHealth_ExplainsIncompleteEngineContract()
    {
        var readiness = _service.Evaluate(new EngineDependencyHealth(
            Ffmpeg: FoundDependency(@"C:\v3dfy\tools\ffmpeg\win-x64\ffmpeg.exe"),
            Ffprobe: FoundDependency(@"C:\v3dfy\tools\ffmpeg\win-x64\ffprobe.exe"),
            Python: FoundDependency(@"C:\v3dfy\engine\iw3\python\python.exe"),
            Iw3EngineDirectory: MissingDependency(
                ToolHealthDetailKind.EngineEntryFilesMissing,
                @"C:\v3dfy\engine\iw3"),
            ModelsDirectory: FoundDependency(@"C:\v3dfy\engine\iw3\nunif\iw3\pretrained_models")));

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage.Contains(
                @"C:\v3dfy\engine\iw3\nunif\iw3\__main__.py",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_DetailedHealth_ExplainsMissingEngineManifest()
    {
        var readiness = _service.Evaluate(new EngineDependencyHealth(
            Ffmpeg: FoundDependency(@"C:\v3dfy\tools\ffmpeg\win-x64\ffmpeg.exe"),
            Ffprobe: FoundDependency(@"C:\v3dfy\tools\ffmpeg\win-x64\ffprobe.exe"),
            Python: FoundDependency(@"C:\v3dfy\engine\iw3\python\python.exe"),
            Iw3EngineDirectory: MissingDependency(
                ToolHealthDetailKind.EngineManifestMissing,
                @"C:\v3dfy\engine\iw3"),
            ModelsDirectory: FoundDependency(@"C:\v3dfy\engine\iw3\nunif\iw3\pretrained_models")));

        Assert.False(readiness.CanConvert);
        Assert.Contains(
            readiness.Issues,
            issue => issue.EnglishMessage.Contains(
                @"C:\v3dfy\engine\iw3\ENGINE_MANIFEST.json",
                StringComparison.Ordinal));
    }

    private static EngineHealthStatus CompleteHealth() => new(
        Ffmpeg: ToolHealthStatus.Found,
        Ffprobe: ToolHealthStatus.Found,
        Python: ToolHealthStatus.Found,
        Iw3EngineDirectory: ToolHealthStatus.Found,
        ModelsDirectory: ToolHealthStatus.Found);

    private static ToolDependencyHealth FoundDependency(string path) => new(
        ToolHealthStatus.Found,
        ToolHealthDetailKind.BundledFileFound,
        path);

    private static ToolDependencyHealth MissingDependency(
        ToolHealthDetailKind detailKind,
        string path) => new(
        ToolHealthStatus.Missing,
        detailKind,
        path);
}
