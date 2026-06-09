using V3dfy.Core.Models;
using V3dfy.Core.Preview;

namespace V3dfy.Tests.Preview;

public sealed class PreviewCachePathServiceTests
{
    [Fact]
    public void CreatePaths_UsesLocalAppDataStylePreviewCachePath()
    {
        var provider = new StubPreviewCachePathProvider(@"C:\Users\tester\AppData\Local\v3dfy\previews");
        var service = new PreviewCachePathService(provider);

        var paths = service.CreatePaths(CreateConfiguration(), new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(@"C:\Users\tester\AppData\Local\v3dfy\previews", paths.CacheDirectory);
        Assert.StartsWith(paths.CacheDirectory, paths.PreviewOutputPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePaths_PutsEveryPreviewPathUnderPreviewCacheRoot()
    {
        var cacheRoot = @"C:\Users\tester\AppData\Local\v3dfy\previews";
        var service = new PreviewCachePathService(
            new StubPreviewCachePathProvider(cacheRoot));

        var paths = service.CreatePaths(CreateConfiguration(), DateTimeOffset.UtcNow);

        Assert.True(paths.AreAllPathsInsideCache);
        Assert.Empty(paths.PathsOutsideCache);
        Assert.All(paths.AllPaths, path =>
            Assert.True(
                PreviewCachePathSafety.IsPathInsideRoot(cacheRoot, path),
                $"Expected preview path to be inside cache root: {path}"));
    }

    [Fact]
    public void CreatePaths_DoesNotUseSourceFolderFinalOutputFolderOrCurrentDirectory()
    {
        var cacheRoot = @"C:\Users\tester\AppData\Local\v3dfy\previews";
        var sourceDirectory = @"D:\Videos";
        var finalOutputDirectory = @"E:\Converted";
        var currentDirectory = Directory.GetCurrentDirectory();
        var service = new PreviewCachePathService(
            new StubPreviewCachePathProvider(cacheRoot));

        var paths = service.CreatePaths(CreateConfiguration(), DateTimeOffset.UtcNow);

        Assert.All(paths.AllPaths, path =>
        {
            Assert.False(
                PreviewCachePathSafety.IsPathInsideRoot(sourceDirectory, path),
                $"Preview path must not be in the source folder: {path}");
            Assert.False(
                PreviewCachePathSafety.IsPathInsideRoot(finalOutputDirectory, path),
                $"Preview path must not be in the final output folder: {path}");
            Assert.False(
                PreviewCachePathSafety.IsPathInsideRoot(currentDirectory, path),
                $"Preview path must not be in the current working directory: {path}");
        });
    }

    [Fact]
    public void PreviewPathSafety_RejectsPathsOutsidePreviewCache()
    {
        var paths = new PreviewCachePaths(
            CacheDirectory: @"C:\Users\tester\AppData\Local\v3dfy\previews",
            ShortSourcePath: @"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.source.mkv",
            PartialShortSourcePath: @"D:\Videos\Movie.source.v3dfy-partial.mkv",
            PreviewOutputPath: @"C:\Users\tester\AppData\Local\v3dfy\previews\Movie.preview.mp4",
            PartialPreviewOutputPath: @"E:\Converted\Movie.preview.v3dfy-partial.mp4");

        Assert.False(paths.AreAllPathsInsideCache);
        Assert.Equal(2, paths.PathsOutsideCache.Count);
        Assert.Contains(@"D:\Videos\Movie.source.v3dfy-partial.mkv", paths.PathsOutsideCache);
        Assert.Contains(@"E:\Converted\Movie.preview.v3dfy-partial.mp4", paths.PathsOutsideCache);
    }

    [Fact]
    public void CreatePaths_FileNameIncludesSourceModelLayoutIntensityAndTimestamp()
    {
        var service = new PreviewCachePathService(
            new StubPreviewCachePathProvider(@"C:\Users\tester\AppData\Local\v3dfy\previews"));

        var paths = service.CreatePaths(CreateConfiguration(), new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero));

        var fileName = Path.GetFileName(paths.PreviewOutputPath);
        Assert.Contains("Movie", fileName, StringComparison.Ordinal);
        Assert.Contains("depth-anything-metric-indoor", fileName, StringComparison.Ordinal);
        Assert.Contains("htab", fileName, StringComparison.Ordinal);
        Assert.Contains("medium", fileName, StringComparison.Ordinal);
        Assert.Contains("20260606120000", fileName, StringComparison.Ordinal);
        Assert.EndsWith(".preview.mp4", fileName, StringComparison.Ordinal);
        Assert.EndsWith(".source.mkv", paths.ShortSourcePath, StringComparison.Ordinal);
        Assert.EndsWith(".source.v3dfy-partial.mkv", paths.PartialShortSourcePath, StringComparison.Ordinal);
        Assert.EndsWith(".v3dfy-partial.mp4", paths.PartialPreviewOutputPath, StringComparison.Ordinal);
    }

    private static PreviewConfigurationSnapshot CreateConfiguration() => new(
        SourcePath: @"D:\Videos\Movie.mp4",
        OutputProfileName: "LG 3D Full HD 2012",
        OutputContainer: OutputContainer.MP4,
        QualityPreset: AiQualityPreset.Balanced,
        Intensity: ThreeDIntensity.Medium,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
        ModelKey: "depth-anything-metric-indoor",
        ModelDisplayName: "Depth Anything Metric Indoor",
        ModelRelativePath: "hub/checkpoints/depth_anything_metric_depth_indoor.pt",
        Iw3DepthModelName: "ZoeD_Any_N",
        PreviewStartTime: TimeSpan.FromMinutes(10),
        PreviewDuration: TimeSpan.FromSeconds(15));

    private sealed class StubPreviewCachePathProvider(string path) : IPreviewCachePathProvider
    {
        public string GetPreviewCacheDirectory() => path;
    }
}
