using V3dfy.Core.Planning;

namespace V3dfy.Tests.Planning;

public sealed class ConversionOutputPathStateTests
{
    [Fact]
    public void CommitOutputPathText_TrimmedPath_SetsCustomOutputPath()
    {
        var state = new ConversionOutputPathState();

        var changed = state.CommitOutputPathText(
            "  C:\\Videos\\Converted\\Movie.3d.mp4  ",
            out var normalizedPath);

        Assert.True(changed);
        Assert.Equal("C:\\Videos\\Converted\\Movie.3d.mp4", normalizedPath);
        Assert.Equal("C:\\Videos\\Converted\\Movie.3d.mp4", state.CustomOutputPath);
        Assert.True(state.HasCustomOutputPath);
    }

    [Fact]
    public void CommitOutputPathText_Whitespace_ClearsCustomOutputPath()
    {
        var state = new ConversionOutputPathState();
        state.SetCustomOutputPath("C:\\Videos\\Converted\\Movie.3d.mp4");

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
        state.SetCustomOutputPath("C:\\Videos\\Converted\\Movie.3d.mp4");

        var changed = state.CommitOutputPathText(
            "C:\\Videos\\Converted\\Movie.3d.mp4",
            out var normalizedPath);

        Assert.False(changed);
        Assert.Equal("C:\\Videos\\Converted\\Movie.3d.mp4", normalizedPath);
        Assert.Equal("C:\\Videos\\Converted\\Movie.3d.mp4", state.CustomOutputPath);
    }

    [Fact]
    public void SetCustomOutputPath_PreservesExactPath()
    {
        var state = new ConversionOutputPathState();

        var changed = state.SetCustomOutputPath("C:\\Videos\\Custom Name.output");

        Assert.True(changed);
        Assert.Equal("C:\\Videos\\Custom Name.output", state.CustomOutputPath);
    }

    [Fact]
    public void ResetCustomOutputPath_WithCustomPath_ClearsPath()
    {
        var state = new ConversionOutputPathState();
        state.SetCustomOutputPath("C:\\Videos\\Converted\\Movie.3d.mp4");

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
        state.SetCustomOutputPath("D:\\Manual\\Movie.3d.mkv");

        var directory = state.GetInitialOutputDirectory("C:\\Videos\\Movie.v3dfy.3d.htab.mp4");

        Assert.Equal("D:\\Manual", directory);
    }

    [Fact]
    public void GetInitialOutputDirectory_WithoutCustomPath_UsesAutomaticDirectory()
    {
        var state = new ConversionOutputPathState();

        var directory = state.GetInitialOutputDirectory("C:\\Videos\\Movie.v3dfy.3d.htab.mp4");

        Assert.Equal("C:\\Videos", directory);
    }

    [Fact]
    public void ClearCustomOutputPath_ClearsPathWithoutNeedingExistingValue()
    {
        var state = new ConversionOutputPathState();
        state.SetCustomOutputPath("C:\\Videos\\Converted\\Movie.3d.mp4");

        state.ClearCustomOutputPath();

        Assert.Null(state.CustomOutputPath);
        Assert.False(state.HasCustomOutputPath);
    }
}
