namespace V3dfy.Core.Execution;

public static class PreviewProgressResolver
{
    public static int Resolve(int progressPercent, string? outputText)
    {
        var parsedProgress = ConversionProgressTimingEstimator
            .TryParseOutputLine(outputText)
            ?.ProgressPercent;

        return Math.Clamp(parsedProgress ?? progressPercent, 0, 100);
    }
}
