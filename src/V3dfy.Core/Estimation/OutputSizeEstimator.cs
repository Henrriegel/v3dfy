using V3dfy.Core.Models;

namespace V3dfy.Core.Estimation;

public sealed class OutputSizeEstimator
{
    private const double AudioBitrateLowMbps = 0.192d;
    private const double AudioBitrateHighMbps = 0.448d;
    private const long MinimumRecommendedWorkingBytes = 512L * 1024L * 1024L;

    public OutputSizeEstimate Estimate(OutputSizeEstimateInput? input)
    {
        if (input?.Duration is not { } duration || duration <= TimeSpan.Zero)
        {
            return OutputSizeEstimate.Unavailable(
                "Output size depends on final encoding settings.",
                "El tamano de salida depende de la configuracion final de codificacion.");
        }

        var (videoLow, videoHigh) = GetVideoBitrateRange(input);
        var lowBytes = BitrateToBytes(videoLow + AudioBitrateLowMbps, duration);
        var highBytes = BitrateToBytes(videoHigh + AudioBitrateHighMbps, duration);
        var tempMultiplier = input.IncludeTemporaryWorkingSpace ? 2.2d : 1.25d;
        var recommendedFreeBytes = Math.Max(
            MinimumRecommendedWorkingBytes,
            (long)Math.Ceiling(highBytes * tempMultiplier));

        return new(
            IsAvailable: true,
            LowBytes: lowBytes,
            HighBytes: highBytes,
            RecommendedFreeBytes: recommendedFreeBytes,
            EnglishBasisItems:
            [
                $"{input.OutputContainer} output",
                $"{videoLow:0.#}-{videoHigh:0.#} Mbps video estimate",
                input.IncludeTemporaryWorkingSpace
                    ? "includes temporary working-space overhead"
                    : "final output only",
            ],
            SpanishBasisItems:
            [
                $"salida {input.OutputContainer}",
                $"estimacion de video {videoLow:0.#}-{videoHigh:0.#} Mbps",
                input.IncludeTemporaryWorkingSpace
                    ? "incluye espacio temporal de trabajo"
                    : "solo salida final",
            ]);
    }

    private static (double Low, double High) GetVideoBitrateRange(
        OutputSizeEstimateInput input)
    {
        var presetId = Normalize(input.OutputPresetId);
        var range = presetId switch
        {
            "maximum-compatibility" => (8d, 12d),
            "high-quality-master" => (18d, 28d),
            "legacy-lg-3d-tv-2012" => (10d, 14d),
            _ => (10d, 16d),
        };

        range = input.QualityPreset switch
        {
            AiQualityPreset.Fast => (range.Item1 * 0.80d, range.Item2 * 0.88d),
            AiQualityPreset.HighQuality => (range.Item1 * 1.25d, range.Item2 * 1.35d),
            _ => range,
        };

        if (input.OutputContainer == OutputContainer.MKV)
        {
            range = (range.Item1 * 1.10d, range.Item2 * 1.18d);
        }

        var width = input.TargetWidth ?? 1920;
        var height = input.TargetHeight ?? 1080;
        var pixelScale = Math.Clamp((width * height) / (1920d * 1080d), 0.65d, 2.50d);
        return (range.Item1 * pixelScale, range.Item2 * pixelScale);
    }

    private static long BitrateToBytes(double megabitsPerSecond, TimeSpan duration) =>
        (long)Math.Ceiling(megabitsPerSecond * 1_000_000d / 8d * duration.TotalSeconds);

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
}
