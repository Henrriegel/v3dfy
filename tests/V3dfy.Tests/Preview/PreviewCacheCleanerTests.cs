using V3dfy.Core.Preview;

namespace V3dfy.Tests.Preview;

public sealed class PreviewCacheCleanerTests
{
    [Fact]
    public void DeleteStaleFiles_DeletesOnlyFilesInsidePreviewCacheOlderThanLimit()
    {
        var cacheRoot = TestPaths.PreviewCacheRoot();
        var stalePreviewPath = TestPaths.PreviewCacheRoot("old.preview.mp4");
        var freshPreviewPath = TestPaths.PreviewCacheRoot("new.preview.mp4");
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");
        var files = new FakePreviewCacheFileService(
            [
                new(stalePreviewPath, new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero)),
                new(freshPreviewPath, new DateTimeOffset(2026, 6, 6, 11, 0, 0, 0, TimeSpan.Zero)),
                new(sourcePath, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            ]);
        var cleaner = new PreviewCacheCleaner(files);

        var deleted = cleaner.DeleteStaleFiles(
            cacheRoot,
            TimeSpan.FromHours(24),
            new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(1, deleted);
        Assert.Contains(stalePreviewPath, files.DeletedPaths);
        Assert.DoesNotContain(sourcePath, files.DeletedPaths);
    }

    [Fact]
    public void DeletePreviewFiles_DoesNotDeleteSourceVideosOrFinalOutputsOutsideCache()
    {
        var files = new FakePreviewCacheFileService([]);
        var cleaner = new PreviewCacheCleaner(files);
        var cacheRoot = TestPaths.PreviewCacheRoot();
        var previewPath = TestPaths.PreviewCacheRoot("Movie.preview.mp4");
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");

        cleaner.DeletePreviewFiles(
            cacheRoot,
            [
                previewPath,
                sourcePath,
                finalOutputPath,
            ]);

        Assert.Single(files.DeletedPaths);
        Assert.Equal(previewPath, files.DeletedPaths.Single());
    }

    [Fact]
    public void DeletePartialFiles_DeletesOnlyV3dfyPartialFilesInsidePreviewCache()
    {
        var cacheRoot = TestPaths.PreviewCacheRoot();
        var sourcePartialMp4 = TestPaths.PreviewCacheRoot("Movie.source.v3dfy-partial.mp4");
        var sourcePartialMkv = TestPaths.PreviewCacheRoot("Movie.source.v3dfy-partial.mkv");
        var tempPreviewPartial = TestPaths.PreviewCacheRoot("tmp123.preview.v3dfy-partial.mp4");
        var unrelatedPartial = TestPaths.PreviewCacheRoot("Movie.v3dfy-partial.tmp");
        var acceptedPreview = TestPaths.PreviewCacheRoot("Movie.preview.mp4");
        var sourceClip = TestPaths.PreviewCacheRoot("Movie.source.mp4");
        var outputFolderPartial = TestPaths.OutputRoot("Movie.v3dfy-partial.mp4");
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");
        var files = new FakePreviewCacheFileService(
            [
                new(sourcePartialMp4, DateTimeOffset.UtcNow),
                new(sourcePartialMkv, DateTimeOffset.UtcNow),
                new(tempPreviewPartial, DateTimeOffset.UtcNow),
                new(unrelatedPartial, DateTimeOffset.UtcNow),
                new(acceptedPreview, DateTimeOffset.UtcNow),
                new(sourceClip, DateTimeOffset.UtcNow),
                new(outputFolderPartial, DateTimeOffset.UtcNow),
                new(sourcePath, DateTimeOffset.UtcNow),
            ]);
        var cleaner = new PreviewCacheCleaner(files);

        var deleted = cleaner.DeletePartialFiles(cacheRoot);

        Assert.Equal(3, deleted);
        Assert.Contains(sourcePartialMp4, files.DeletedPaths);
        Assert.Contains(sourcePartialMkv, files.DeletedPaths);
        Assert.Contains(tempPreviewPartial, files.DeletedPaths);
        Assert.DoesNotContain(unrelatedPartial, files.DeletedPaths);
        Assert.DoesNotContain(acceptedPreview, files.DeletedPaths);
        Assert.DoesNotContain(sourceClip, files.DeletedPaths);
        Assert.DoesNotContain(outputFolderPartial, files.DeletedPaths);
        Assert.DoesNotContain(sourcePath, files.DeletedPaths);
    }

    [Fact]
    public void IsPreviewPartialFilePath_AcceptsSupportedPartialNamingPatterns()
    {
        var cacheRoot = TestPaths.PreviewCacheRoot();
        var paths = new[]
        {
            TestPaths.PreviewCacheRoot("Movie.source.v3dfy-partial.mp4"),
            TestPaths.PreviewCacheRoot("Movie.source.v3dfy-partial.mkv"),
            TestPaths.PreviewCacheRoot("tmp123.preview.v3dfy-partial.mp4"),
            TestPaths.PreviewCacheRoot("_tmp_123.source.v3dfy-partial.mkv"),
        };

        Assert.All(
            paths,
            path => Assert.True(PreviewCacheCleaner.IsPreviewPartialFilePath(
                cacheRoot,
                path)));
    }

    [Fact]
    public void IsPreviewPartialFilePath_RejectsAcceptedPreviewAndGenericPartialNames()
    {
        var cacheRoot = TestPaths.PreviewCacheRoot();

        Assert.False(PreviewCacheCleaner.IsPreviewPartialFilePath(
            cacheRoot,
            TestPaths.PreviewCacheRoot("Movie.preview.mp4")));
        Assert.False(PreviewCacheCleaner.IsPreviewPartialFilePath(
            cacheRoot,
            TestPaths.PreviewCacheRoot("Movie.v3dfy-partial.tmp")));
        Assert.False(PreviewCacheCleaner.IsPreviewPartialFilePath(
            cacheRoot,
            TestPaths.OutputRoot("Movie.preview.v3dfy-partial.mp4")));
    }

    [Fact]
    public void DeletePartialFiles_LockedStalePreviewPartialWarnsAndContinues()
    {
        var cacheRoot = TestPaths.PreviewCacheRoot();
        var lockedPartial = TestPaths.PreviewCacheRoot("Movie.preview.v3dfy-partial.mp4");
        var sourcePartial = TestPaths.PreviewCacheRoot("Movie.source.v3dfy-partial.mkv");
        var files = new FakePreviewCacheFileService(
            [
                new(lockedPartial, DateTimeOffset.UtcNow),
                new(sourcePartial, DateTimeOffset.UtcNow),
            ]);
        files.FailDeletes(lockedPartial, new IOException("locked"));
        var cleaner = new PreviewCacheCleaner(files);
        var warnings = new List<string>();

        var deleted = cleaner.DeletePartialFiles(
            cacheRoot,
            (path, _) => warnings.Add(path));

        Assert.Equal(1, deleted);
        Assert.Contains(sourcePartial, files.DeletedPaths);
        Assert.DoesNotContain(lockedPartial, files.DeletedPaths);
        Assert.Contains(lockedPartial, warnings);
    }

    private sealed class FakePreviewCacheFileService(
        IReadOnlyList<PreviewCacheFile> files) : IPreviewCacheFileService
    {
        private readonly Dictionary<string, Exception> _deleteFailures = new(StringComparer.OrdinalIgnoreCase);

        public List<string> DeletedPaths { get; } = [];

        public void EnsureDirectory(string directory)
        {
        }

        public bool Exists(string path) => true;

        public void DeleteIfExists(string path)
        {
            if (_deleteFailures.TryGetValue(path, out var exception))
            {
                throw exception;
            }

            DeletedPaths.Add(path);
        }

        public void Move(string sourcePath, string destinationPath, bool overwrite)
        {
        }

        public IReadOnlyList<PreviewCacheFile> EnumerateFiles(string directory) => files;

        public void FailDeletes(string path, Exception exception) =>
            _deleteFailures[path] = exception;
    }
}
