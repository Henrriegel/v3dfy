using V3dfy.Core.Preview;

namespace V3dfy.Tests.Preview;

public sealed class PreviewCacheCleanerTests
{
    [Fact]
    public void DeleteStaleFiles_DeletesOnlyFilesInsidePreviewCacheOlderThanLimit()
    {
        var files = new FakePreviewCacheFileService(
            [
                new(@"C:\Users\tester\AppData\Local\v3dfy\previews\old.preview.mp4", new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero)),
                new(@"C:\Users\tester\AppData\Local\v3dfy\previews\new.preview.mp4", new DateTimeOffset(2026, 6, 6, 11, 0, 0, TimeSpan.Zero)),
                new(@"D:\Videos\Movie.mp4", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            ]);
        var cleaner = new PreviewCacheCleaner(files);

        var deleted = cleaner.DeleteStaleFiles(
            @"C:\Users\tester\AppData\Local\v3dfy\previews",
            TimeSpan.FromHours(24),
            new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(1, deleted);
        Assert.Contains(@"C:\Users\tester\AppData\Local\v3dfy\previews\old.preview.mp4", files.DeletedPaths);
        Assert.DoesNotContain(@"D:\Videos\Movie.mp4", files.DeletedPaths);
    }

    [Fact]
    public void DeletePreviewFiles_DoesNotDeleteSourceVideosOrFinalOutputsOutsideCache()
    {
        var files = new FakePreviewCacheFileService([]);
        var cleaner = new PreviewCacheCleaner(files);

        cleaner.DeletePreviewFiles(
            @"C:\Users\tester\AppData\Local\v3dfy\previews",
            [
                @"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.preview.mp4",
                @"D:\Videos\Movie.mp4",
                @"D:\Videos\Movie.v3dfy.3d.htab.mp4",
            ]);

        Assert.Single(files.DeletedPaths);
        Assert.Equal(
            @"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.preview.mp4",
            files.DeletedPaths.Single());
    }

    [Fact]
    public void DeletePartialFiles_DeletesOnlyV3dfyPartialFilesInsidePreviewCache()
    {
        var files = new FakePreviewCacheFileService(
            [
                new(@"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.source.v3dfy-partial.mp4", DateTimeOffset.UtcNow),
                new(@"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.source.v3dfy-partial.mkv", DateTimeOffset.UtcNow),
                new(@"C:\Users\tester\AppData\Local\v3dfy\previews\tmp123.preview.v3dfy-partial.mp4", DateTimeOffset.UtcNow),
                new(@"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.preview.mp4", DateTimeOffset.UtcNow),
                new(@"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.source.mp4", DateTimeOffset.UtcNow),
                new(@"D:\Videos\Movie.v3dfy-partial.mp4", DateTimeOffset.UtcNow),
                new(@"D:\Videos\Movie.mp4", DateTimeOffset.UtcNow),
            ]);
        var cleaner = new PreviewCacheCleaner(files);

        var deleted = cleaner.DeletePartialFiles(
            @"C:\Users\tester\AppData\Local\v3dfy\previews");

        Assert.Equal(3, deleted);
        Assert.Contains(
            @"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.source.v3dfy-partial.mp4",
            files.DeletedPaths);
        Assert.Contains(
            @"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.source.v3dfy-partial.mkv",
            files.DeletedPaths);
        Assert.Contains(
            @"C:\Users\tester\AppData\Local\v3dfy\previews\tmp123.preview.v3dfy-partial.mp4",
            files.DeletedPaths);
        Assert.DoesNotContain(
            @"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.preview.mp4",
            files.DeletedPaths);
        Assert.DoesNotContain(
            @"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.source.mp4",
            files.DeletedPaths);
        Assert.DoesNotContain(@"D:\Videos\Movie.v3dfy-partial.mp4", files.DeletedPaths);
        Assert.DoesNotContain(@"D:\Videos\Movie.mp4", files.DeletedPaths);
    }

    [Theory]
    [InlineData(@"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.source.v3dfy-partial.mp4")]
    [InlineData(@"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.source.v3dfy-partial.mkv")]
    [InlineData(@"C:\Users\tester\AppData\Local\v3dfy\previews\tmp123.preview.v3dfy-partial.mp4")]
    [InlineData(@"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.v3dfy-partial.tmp")]
    public void IsPreviewPartialFilePath_AcceptsSupportedPartialNamingPatterns(
        string path)
    {
        Assert.True(PreviewCacheCleaner.IsPreviewPartialFilePath(
            @"C:\Users\tester\AppData\Local\v3dfy\previews",
            path));
    }

    private sealed class FakePreviewCacheFileService(
        IReadOnlyList<PreviewCacheFile> files) : IPreviewCacheFileService
    {
        public List<string> DeletedPaths { get; } = [];

        public void EnsureDirectory(string directory)
        {
        }

        public bool Exists(string path) => true;

        public void DeleteIfExists(string path) => DeletedPaths.Add(path);

        public void Move(string sourcePath, string destinationPath, bool overwrite)
        {
        }

        public IReadOnlyList<PreviewCacheFile> EnumerateFiles(string directory) => files;
    }
}
