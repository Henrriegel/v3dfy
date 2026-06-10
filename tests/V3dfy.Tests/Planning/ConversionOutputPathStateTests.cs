using V3dfy.Core.Planning;

namespace V3dfy.Tests.Planning;

public sealed class ConversionOutputPathStateTests
{
    [Fact]
    public void CommitOutputPathText_TrimmedPath_SetsCustomOutputPath()
    {
        var state = new ConversionOutputPathState();
        var outputPath = TestPaths.OutputRoot("Converted", "Movie.3d.mp4");

        var changed = state.CommitOutputPathText(
            $"  {outputPath}  ",
            out var normalizedPath);

        Assert.True(changed);
        Assert.Equal(outputPath, normalizedPath);
        Assert.Equal(outputPath, state.CustomOutputPath);
        Assert.True(state.HasCustomOutputPath);
    }

    [Fact]
    public void CommitOutputPathText_Whitespace_ClearsCustomOutputPath()
    {
        var state = new ConversionOutputPathState();
        state.SetCustomOutputPath(TestPaths.OutputRoot("Converted", "Movie.3d.mp4"));

        var changed = state.CommitOutputPathText("   ", out var normalizedPath);

        Assert.True(changed);
        Assert.Null(normalizedPath);
        Assert.Null(state.CustomOutputPath);
        Assert.False(state.HasCustomOutputPath);
    }

    [Fact]
    public void CommitOutputPathText_SameNormalizedPath_ReturnsFalse()
    {
        var state = new ConversionOutputPathState();
        var outputPath = TestPaths.OutputRoot("Converted", "Movie.3d.mp4");
        state.SetCustomOutputPath(outputPath);

        var changed = state.CommitOutputPathText(
            outputPath,
            out var normalizedPath);

        Assert.False(changed);
        Assert.Equal(outputPath, normalizedPath);
        Assert.Equal(outputPath, state.CustomOutputPath);
    }

    [Fact]
    public void SetCustomOutputPath_PreservesExactPath()
    {
        var state = new ConversionOutputPathState();
        var outputPath = TestPaths.OutputRoot("Custom Name.output");

        var changed = state.SetCustomOutputPath(outputPath);

        Assert.True(changed);
        Assert.Equal(outputPath, state.CustomOutputPath);
    }

    [Fact]
    public void ResetCustomOutputPath_WithCustomPath_ClearsPath()
    {
        var state = new ConversionOutputPathState();
        state.SetCustomOutputPath(TestPaths.OutputRoot("Converted", "Movie.3d.mp4"));

        var changed = state.ResetCustomOutputPath();

        Assert.True(changed);
        Assert.Null(state.CustomOutputPath);
        Assert.False(state.HasCustomOutputPath);
    }

    [Fact]
    public void ResetCustomOutputPath_WithoutCustomPath_ReturnsFalse()
    {
        var state = new ConversionOutputPathState();

        var changed = state.ResetCustomOutputPath();

        Assert.False(changed);
    }

    [Fact]
    public void GetInitialOutputDirectory_WithCustomPath_UsesCustomDirectory()
    {
        var state = new ConversionOutputPathState();
        var customOutputPath = TestPaths.OutputRoot("Manual", "Movie.3d.mkv");
        state.SetCustomOutputPath(customOutputPath);

        var directory = state.GetInitialOutputDirectory(TestPaths.SourceRoot("Movie.v3dfy.3d.htab.mp4"));

        Assert.Equal(Path.GetDirectoryName(customOutputPath), directory);
    }

    [Fact]
    public void GetInitialOutputDirectory_WithoutCustomPath_UsesAutomaticDirectory()
    {
        var state = new ConversionOutputPathState();
        var automaticOutputPath = TestPaths.SourceRoot("Movie.v3dfy.3d.htab.mp4");

        var directory = state.GetInitialOutputDirectory(automaticOutputPath);

        Assert.Equal(Path.GetDirectoryName(automaticOutputPath), directory);
    }

    [Fact]
    public void ClearCustomOutputPath_ClearsPathWithoutNeedingExistingValue()
    {
        var state = new ConversionOutputPathState();
        state.SetCustomOutputPath(TestPaths.OutputRoot("Converted", "Movie.3d.mp4"));

        state.ClearCustomOutputPath();

        Assert.Null(state.CustomOutputPath);
        Assert.False(state.HasCustomOutputPath);
    }
}
