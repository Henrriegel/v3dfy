using V3dfy.Core.Execution;

namespace V3dfy.Tests.Execution;

public sealed class PreviewProgressResolverTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(59)]
    public void Resolve_UsesNormalizedProgressPercentDirectly(int percent)
    {
        var resolved = PreviewProgressResolver.Resolve(percent, outputText: null);

        Assert.Equal(percent, resolved);
    }

    [Theory]
    [InlineData("10/100 [00:01<00:09, 10.00it/s]", 10)]
    [InlineData("59/100 [00:59<00:41, 1.00it/s]", 59)]
    public void Resolve_UsesParsedIw3ProgressWhenOutputLineContainsProgress(
        string outputText,
        int expected)
    {
        var resolved = PreviewProgressResolver.Resolve(55, outputText);

        Assert.Equal(expected, resolved);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(150, 100)]
    public void Resolve_ClampsUnparsedProgressPercent(
        int progressPercent,
        int expected)
    {
        var resolved = PreviewProgressResolver.Resolve(progressPercent, outputText: "not progress");

        Assert.Equal(expected, resolved);
    }
}
