using V3dfy.Core.Execution;

namespace V3dfy.Tests.Execution;

public sealed class ConversionOutputOpenServiceTests
{
    [Fact]
    public void OpenAfterSuccessfulConversion_WhenUnchecked_DoesNotOpen()
    {
        var outputPath = TestPaths.OutputRoot("out.mp4");
        var opener = new FakeOutputFileOpenService();
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService([outputPath]),
            opener);

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(success: true),
            outputPath,
            openOutputWhenFinished: false);

        Assert.False(result.Attempted);
        Assert.Equal(0, opener.OpenCallCount);
    }

    [Fact]
    public void OpenAfterSuccessfulConversion_WhenSuccessfulAndChecked_OpensFinalOutput()
    {
        var outputPath = TestPaths.OutputRoot("out.mp4");
        var opener = new FakeOutputFileOpenService();
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService([outputPath]),
            opener);

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(success: true),
            outputPath,
            openOutputWhenFinished: true);

        Assert.True(result.Attempted);
        Assert.True(result.Opened);
        Assert.Equal(outputPath, opener.OpenedPaths.Single());
    }

    [Fact]
    public void OpenAfterSuccessfulConversion_WhenPreferredOutputExists_OpensPreferredOutput()
    {
        var outputPath = TestPaths.OutputRoot("out.mp4");
        var preferredOutputPath = TestPaths.OutputRoot("out.lg3d.hsbs.mp4");
        var opener = new FakeOutputFileOpenService();
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService(
                [outputPath, preferredOutputPath]),
            opener);

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(
                success: true,
                preferredOpenOutputPath: preferredOutputPath),
            outputPath,
            openOutputWhenFinished: true);

        Assert.True(result.Attempted);
        Assert.True(result.Opened);
        Assert.Equal(preferredOutputPath, opener.OpenedPaths.Single());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void OpenAfterSuccessfulConversion_WhenFailedOrCanceled_DoesNotOpen(
        bool success,
        bool wasCanceled)
    {
        var outputPath = TestPaths.OutputRoot("out.mp4");
        var opener = new FakeOutputFileOpenService();
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService([outputPath]),
            opener);

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(success, wasCanceled),
            outputPath,
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
            TestPaths.OutputRoot("out.mp4"),
            openOutputWhenFinished: true);

        Assert.False(result.Attempted);
        Assert.False(result.Opened);
        Assert.Contains("not found", result.EnglishWarning);
    }

    [Fact]
    public void OpenAfterSuccessfulConversion_WhenOpeningFails_ReturnsWarningButNoFailure()
    {
        var outputPath = TestPaths.OutputRoot("out.mp4");
        var service = new ConversionOutputOpenService(
            new FakeConversionOutputFileService([outputPath]),
            new FakeOutputFileOpenService(throwOnOpen: true));

        var result = service.OpenAfterSuccessfulConversion(
            CompletedResult(success: true),
            outputPath,
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

        public IReadOnlyList<string> EnumerateFiles(string directory) =>
            _paths
                .Where(path => string.Equals(
                    Path.GetDirectoryName(Path.GetFullPath(path)),
                    Path.GetFullPath(directory),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
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
