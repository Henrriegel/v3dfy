using V3dfy.Core.Execution;

namespace V3dfy.Tests.Execution;

public sealed class ConversionOutputOpenServiceTests
{
    [Fact]
    public void OpenAfterSuccessfulConversion_WhenUnchecked_DoesNotOpen()
    {
        var opener = new FakeOutputFileOpenService();
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService([@"C:\Videos\out.mp4"]),
            opener);

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(success: true),
            @"C:\Videos\out.mp4",
            openOutputWhenFinished: false);

        Assert.False(result.Attempted);
        Assert.Equal(0, opener.OpenCallCount);
    }

    [Fact]
    public void OpenAfterSuccessfulConversion_WhenSuccessfulAndChecked_OpensFinalOutput()
    {
        var opener = new FakeOutputFileOpenService();
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService([@"C:\Videos\out.mp4"]),
            opener);

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(success: true),
            @"C:\Videos\out.mp4",
            openOutputWhenFinished: true);

        Assert.True(result.Attempted);
        Assert.True(result.Opened);
        Assert.Equal(@"C:\Videos\out.mp4", opener.OpenedPaths.Single());
    }

    [Fact]
    public void OpenAfterSuccessfulConversion_WhenPreferredOutputExists_OpensPreferredOutput()
    {
        var opener = new FakeOutputFileOpenService();
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService(
                [@"C:\Videos\out.mp4", @"C:\Videos\out.lg3d.hsbs.mp4"]),
            opener);

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(
                success: true,
                preferredOpenOutputPath: @"C:\Videos\out.lg3d.hsbs.mp4"),
            @"C:\Videos\out.mp4",
            openOutputWhenFinished: true);

        Assert.True(result.Attempted);
        Assert.True(result.Opened);
        Assert.Equal(@"C:\Videos\out.lg3d.hsbs.mp4", opener.OpenedPaths.Single());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void OpenAfterSuccessfulConversion_WhenFailedOrCanceled_DoesNotOpen(
        bool success,
        bool wasCanceled)
    {
        var opener = new FakeOutputFileOpenService();
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService([@"C:\Videos\out.mp4"]),
            opener);

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(success, wasCanceled),
            @"C:\Videos\out.mp4",
            openOutputWhenFinished: true);

        Assert.False(result.Attempted);
        Assert.Equal(0, opener.OpenCallCount);
    }

    [Fact]
    public void OpenAfterSuccessfulConversion_WhenFinalOutputIsMissing_LogsWarningShape()
    {
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService([]),
            new FakeOutputFileOpenService());

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(success: true),
            @"C:\Videos\out.mp4",
            openOutputWhenFinished: true);

        Assert.False(result.Attempted);
        Assert.False(result.Opened);
        Assert.Contains("not found", result.EnglishWarning);
    }

    [Fact]
    public void OpenAfterSuccessfulConversion_WhenOpeningFails_ReturnsWarningButNoFailure()
    {
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService([@"C:\Videos\out.mp4"]),
            new FakeOutputFileOpenService(throwOnOpen: true));

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(success: true),
            @"C:\Videos\out.mp4",
            openOutputWhenFinished: true);

        Assert.True(result.Attempted);
        Assert.False(result.Opened);
        Assert.Contains("opening the video failed", result.EnglishWarning);
    }

    private static ConversionExecutionResult CompletedResult(
        bool success,
        bool wasCanceled = false,
        string? preferredOpenOutputPath = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            Success: success,
            WasCanceled: wasCanceled,
            ExitCode: success ? 0 : 7,
            EnglishSummary: success ? "ok" : "failed",
            SpanishSummary: success ? "ok" : "fallo",
            StartedAt: now,
            FinishedAt: now,
            Logs: [],
            PreferredOpenOutputPath: preferredOpenOutputPath);
    }

    private sealed class FakeConversionOutputFileService(IEnumerable<string> paths)
        : IConversionOutputFileService
    {
        private readonly HashSet<string> _paths = new(paths, StringComparer.OrdinalIgnoreCase);

        public bool Exists(string path) => _paths.Contains(path);

        public void DeleteIfExists(string path) => _paths.Remove(path);

        public void Move(string sourcePath, string destinationPath, bool overwrite)
        {
            _paths.Remove(sourcePath);
            _paths.Add(destinationPath);
        }
    }

    private sealed class FakeOutputFileOpenService(bool throwOnOpen = false)
        : IOutputFileOpenService
    {
        public List<string> OpenedPaths { get; } = [];

        public int OpenCallCount => OpenedPaths.Count;

        public void Open(string outputPath)
        {
            if (throwOnOpen)
            {
                throw new InvalidOperationException("default player failed");
            }

            OpenedPaths.Add(outputPath);
        }
    }
}
