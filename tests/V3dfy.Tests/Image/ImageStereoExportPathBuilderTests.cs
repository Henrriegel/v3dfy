using V3dfy.Core.Image;

namespace V3dfy.Tests.Image;

public sealed class ImageStereoExportPathBuilderTests
{
    [Fact]
    public void CreateOutputPaths_IncludesSourceWorkflowFormatAndPngExtension()
    {
        var output = ImageStereoExportPathBuilder.CreateOutputPaths(
            @"C:\input\my photo.png",
            @"C:\output",
            ImageStereoExportFormat.SideBySide,
            "depth-anything-metric-indoor",
            pathExists: _ => false);

        var fileName = Path.GetFileName(output.PrimaryOutputPath);

        Assert.Equal("my-photo-depth-anything-metric-indoor-sbs.png", fileName);
        Assert.EndsWith(".png", fileName, StringComparison.OrdinalIgnoreCase);
        Assert.Single(output.GeneratedFiles);
        Assert.Equal(output.PrimaryOutputPath, output.GeneratedFiles[0]);
    }

    [Fact]
    public void CreateOutputPaths_AddsCollisionSafeSuffixWithoutOverwriting()
    {
        var baseName = ImageStereoExportPathBuilder.CreateBaseFileName(
            @"C:\input\source.png",
            ImageStereoExportFormat.HalfTopBottom,
            "depth-anything-metric-indoor");
        var firstCandidate = Path.Combine(@"C:\output", $"{baseName}.png");

        var output = ImageStereoExportPathBuilder.CreateOutputPaths(
            @"C:\input\source.png",
            @"C:\output",
            ImageStereoExportFormat.HalfTopBottom,
            "depth-anything-metric-indoor",
            pathExists: path => string.Equals(path, firstCandidate, StringComparison.OrdinalIgnoreCase));

        Assert.EndsWith("-2.png", output.PrimaryOutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(firstCandidate, output.PrimaryOutputPath);
    }

    [Theory]
    [InlineData(ImageStereoExportFormat.HalfTopBottom, "tab")]
    [InlineData(ImageStereoExportFormat.Anaglyph, "anaglyph")]
    public void CreateOutputPaths_UsesSelectedFormatSuffix(
        ImageStereoExportFormat format,
        string expectedSuffix)
    {
        var output = ImageStereoExportPathBuilder.CreateOutputPaths(
            @"C:\input\source.png",
            @"C:\output",
            format,
            "depth-anything-metric-indoor",
            pathExists: _ => false);

        Assert.EndsWith($"-{expectedSuffix}.png", output.PrimaryOutputPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateOutputPaths_SanitizesUnsafeFilenameParts()
    {
        var sanitized = ImageStereoExportPathBuilder.SanitizeFileNamePart(" bad:name*with spaces ");

        Assert.Equal("bad-name-with-spaces", sanitized);
        Assert.DoesNotContain(':', sanitized);
        Assert.DoesNotContain('*', sanitized);
        Assert.DoesNotContain(' ', sanitized);
    }

    [Fact]
    public void CreateOutputPaths_CreatesLeftAndRightPairPaths()
    {
        var output = ImageStereoExportPathBuilder.CreateOutputPaths(
            @"C:\input\source.png",
            @"C:\output",
            ImageStereoExportFormat.LeftRightPair,
            "depth-anything-metric-indoor",
            pathExists: _ => false);

        Assert.Equal(2, output.GeneratedFiles.Count);
        Assert.Contains("-lr-pair_", Path.GetFileName(output.PrimaryOutputPath));
        Assert.Contains("_left.png", Path.GetFileName(output.GeneratedFiles[0]));
        Assert.Contains("_right.png", Path.GetFileName(output.GeneratedFiles[1]));
    }
}
