using V3dfy.Core.Execution;
using V3dfy.Core.Preview;

namespace V3dfy.Tests.Preview;

public sealed class PreviewOutputOpenServiceTests
{
    [Fact]
    public void OpenCurrentPreview_OpensOnlyCurrentReadyPreview()
    {
        var previewPath = PreviewPath();
        var opener = new FakeOutputFileOpenService();
        var service = new PreviewOutputOpenService(
            new FakeOutputFileService(existingPaths: [previewPath]),
            opener);

        var result = service.OpenCurrentPreview(ReadyState());

        Assert.True(result.Opened);
        Assert.Equal(previewPath, opener.OpenedPaths.Single());
    }

    [Theory]
    [InlineData(PreviewGenerationStatus.Failed)]
    [InlineData(PreviewGenerationStatus.Canceled)]
    [InlineData(PreviewGenerationStatus.Outdated)]
    [InlineData(PreviewGenerationStatus.NotGenerated)]
    public void OpenCurrentPreview_DoesNotRunAfterFailureCancelOutdatedOrMissing(
        PreviewGenerationStatus status)
    {
        var opener = new FakeOutputFileOpenService();
        var service = new PreviewOutputOpenService(
            new FakeOutputFileService(existingPaths: [PreviewPath()]),
            opener);

        var result = service.OpenCurrentPreview(ReadyState() with { Status = status });

        Assert.False(result.Opened);
        Assert.Empty(opener.OpenedPaths);
    }

    [Fact]
    public void OpenCurrentPreview_WhenOpeningFails_ReturnsWarning()
    {
        var service = new PreviewOutputOpenService(
            new FakeOutputFileService(existingPaths: [PreviewPath()]),
            new FakeOutputFileOpenService(throwOnOpen: true));

        var result = service.OpenCurrentPreview(ReadyState());

        Assert.False(result.Opened);
        Assert.NotNull(result.EnglishWarning);
    }

    private static PreviewWorkflowState ReadyState() => new(
        Status: PreviewGenerationStatus.Ready,
        OutputPath: PreviewPath(),
        ConfigurationFingerprint: "fingerprint",
        StartedAt: DateTimeOffset.UtcNow,
        FinishedAt: DateTimeOffset.UtcNow,
        PreviewStartTime: TimeSpan.Zero,
        PreviewDuration: TimeSpan.FromSeconds(15),
        EnglishDetail: "Preview ready.",
        SpanishDetail: "Vista previa lista.");

    private static string PreviewPath() => TestPaths.PreviewCacheRoot("preview.mp4");

    private sealed class FakeOutputFileService(
        IReadOnlyList<string> existingPaths) : IConversionOutputFileService
    {
        public bool Exists(string path) => existingPaths.Contains(path, StringComparer.OrdinalIgnoreCase);

        public void DeleteIfExists(string path)
        {
        }

        public void Move(string sourcePath, string destinationPath, bool overwrite)
        {
        }

        public IReadOnlyList<string> EnumerateFiles(string directory) => [];
    }

    private sealed class FakeOutputFileOpenService(bool throwOnOpen = false) : IOutputFileOpenService
    {
        public List<string> OpenedPaths { get; } = [];

        public void Open(string outputPath)
        {
            if (throwOnOpen)
            {
                throw new InvalidOperationException("open failed");
            }

            OpenedPaths.Add(outputPath);
        }
    }
}
