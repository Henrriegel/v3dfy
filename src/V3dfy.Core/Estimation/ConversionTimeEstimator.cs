using V3dfy.Core.Models;

namespace V3dfy.Core.Estimation;

public sealed class ConversionTimeEstimator
{
    private const double DefaultSmallModelMegapixelFramesPerSecond = 38d;
    private const double DefaultBaseModelMegapixelFramesPerSecond = 24d;
    private const double DefaultLargeModelMegapixelFramesPerSecond = 11d;
    private const double DefaultMetricModelMegapixelFramesPerSecond = 18d;
    private const double DefaultExperimentalModelMegapixelFramesPerSecond = 8d;

    public ConversionTimeEstimate Estimate(
        ConversionEstimateInput? input,
        IReadOnlyList<ConversionPerformanceRecord>? history)
    {
        if (input is null ||
            !ConversionPerformanceHistory.TryGetVideoWork(
                input,
                operationDuration: null,
                out _,
                out var frameCount,
                out var width,
                out var height,
                out var megapixelFrames))
        {
            return ConversionTimeEstimate.Unavailable(
                "Not enough video information yet.",
                "Aun no hay suficiente informacion del video.");
        }

        var modelProfile = CreateModelProfile(input);
        var usableHistory = ConversionPerformanceHistory.Sanitize(history);
        var match = SelectHistoryMatch(input, modelProfile, usableHistory);
        var throughput = match.ThroughputMegapixelFramesPerSecond ??
            modelProfile.DefaultMegapixelFramesPerSecond;
        var adjustedSeconds = megapixelFrames / Math.Max(1d, throughput);
        adjustedSeconds *= GetQualityFactor(input.QualityPreset);
        adjustedSeconds *= GetLayoutFactor(input.ThreeDOutputFormat);
        adjustedSeconds = Math.Max(10d, adjustedSeconds);

        var (lowFactor, highFactor) = match.Confidence switch
        {
            ConversionEstimateConfidence.High => (0.90d, 1.15d),
            ConversionEstimateConfidence.Medium => (0.82d, 1.35d),
            _ => (0.70d, 1.75d),
        };

        var displayName = string.IsNullOrWhiteSpace(input.ModelDisplayName)
            ? modelProfile.EnglishName
            : input.ModelDisplayName!;
        var resolution = $"{width}p";
        var frameText = frameCount.ToString("N0");
        var englishBasis = new List<string>
        {
            $"{width}x{height}, {frameText} frames",
            displayName,
            input.OutputPresetName ?? input.OutputPresetId ?? "selected output profile",
            match.UsedLocalHistory
                ? "local performance history"
                : $"{modelProfile.EnglishSpeedTier} built-in speed profile",
            string.IsNullOrWhiteSpace(input.DeviceBucket)
                ? "hardware not benchmarked yet"
                : input.DeviceBucket,
        };
        var spanishBasis = new List<string>
        {
            $"{width}x{height}, {frameText} frames",
            displayName,
            input.OutputPresetName ?? input.OutputPresetId ?? "perfil de salida seleccionado",
            match.UsedLocalHistory
                ? "historial local de rendimiento"
                : $"perfil integrado de velocidad {modelProfile.SpanishSpeedTier}",
            string.IsNullOrWhiteSpace(input.DeviceBucket)
                ? "hardware aun no medido"
                : input.DeviceBucket,
        };

        if (height > 0)
        {
            englishBasis[0] = $"{resolution}, {frameText} frames";
            spanishBasis[0] = $"{resolution}, {frameText} frames";
        }

        return new(
            IsAvailable: true,
            Low: TimeSpan.FromSeconds(adjustedSeconds * lowFactor),
            High: TimeSpan.FromSeconds(adjustedSeconds * highFactor),
            Confidence: match.Confidence,
            EnglishBasisItems: englishBasis,
            SpanishBasisItems: spanishBasis,
            UsedLocalHistory: match.UsedLocalHistory);
    }

    public static ModelPerformanceProfile CreateModelProfile(ConversionEstimateInput input)
    {
        var key = Normalize(input.ModelKey);
        var name = Normalize(input.ModelDisplayName);
        var source = $"{key} {name}";

        if (source.Contains("3-mono") ||
            source.Contains("depth-anything-3") ||
            source.Contains("experimental"))
        {
            return new(
                EnglishName: "experimental large model",
                EnglishSpeedTier: "slow experimental",
                SpanishSpeedTier: "lento experimental",
                DefaultMegapixelFramesPerSecond: DefaultExperimentalModelMegapixelFramesPerSecond);
        }

        if (source.Contains("large") || source.Contains("_l") || source.Contains("vitl"))
        {
            return new(
                EnglishName: "large model",
                EnglishSpeedTier: "slow",
                SpanishSpeedTier: "lento",
                DefaultMegapixelFramesPerSecond: DefaultLargeModelMegapixelFramesPerSecond);
        }

        if (source.Contains("metric") ||
            source.Contains("indoor") ||
            source.Contains("outdoor") ||
            source.Contains("_n") ||
            source.Contains("_k"))
        {
            return new(
                EnglishName: "metric model",
                EnglishSpeedTier: "balanced metric",
                SpanishSpeedTier: "metrico balanceado",
                DefaultMegapixelFramesPerSecond: DefaultMetricModelMegapixelFramesPerSecond);
        }

        if (source.Contains("base") || source.Contains("_b") || source.Contains("vitb"))
        {
            return new(
                EnglishName: "base model",
                EnglishSpeedTier: "balanced",
                SpanishSpeedTier: "balanceado",
                DefaultMegapixelFramesPerSecond: DefaultBaseModelMegapixelFramesPerSecond);
        }

        return new(
            EnglishName: "small model",
            EnglishSpeedTier: "fast",
            SpanishSpeedTier: "rapido",
            DefaultMegapixelFramesPerSecond: DefaultSmallModelMegapixelFramesPerSecond);
    }

    private static HistoryMatch SelectHistoryMatch(
        ConversionEstimateInput input,
        ModelPerformanceProfile modelProfile,
        IReadOnlyList<ConversionPerformanceRecord> history)
    {
        var normalizedModel = Normalize(input.ModelKey);
        var normalizedDevice = Normalize(input.DeviceBucket);
        var width = input.SourceWidth ?? 0;
        var height = input.SourceHeight ?? 0;
        var close = history
            .Where(record =>
                string.Equals(record.ModelKey, normalizedModel, StringComparison.Ordinal) &&
                DeviceMatches(record.DeviceBucket, normalizedDevice) &&
                IsResolutionRatioWithin(record, width, height, 0.80d, 1.25d))
            .ToArray();
        if (close.Any(record => record.OperationType == ConversionPerformanceOperationType.FullConversion))
        {
            return new(
                ConversionEstimateConfidence.High,
                WeightedThroughput(close),
                UsedLocalHistory: true);
        }

        var similar = history
            .Where(record =>
                (string.Equals(record.ModelKey, normalizedModel, StringComparison.Ordinal) ||
                 ModelSpeedBucket(record.ModelKey) == ModelSpeedBucket(normalizedModel)) &&
                IsResolutionRatioWithin(record, width, height, 0.50d, 2.00d))
            .ToArray();
        if (similar.Length > 0)
        {
            return new(
                ConversionEstimateConfidence.Medium,
                WeightedThroughput(similar),
                UsedLocalHistory: true);
        }

        return new(
            ConversionEstimateConfidence.Low,
            modelProfile.DefaultMegapixelFramesPerSecond,
            UsedLocalHistory: false);
    }

    private static double WeightedThroughput(IReadOnlyList<ConversionPerformanceRecord> records)
    {
        var weightedTotal = 0d;
        var weightTotal = 0d;
        foreach (var record in records)
        {
            var weight = record.OperationType == ConversionPerformanceOperationType.FullConversion
                ? 1d
                : 0.35d;
            weightedTotal += record.EffectiveMegapixelFramesPerSecond * weight;
            weightTotal += weight;
        }

        return weightTotal <= 0 ? 0 : weightedTotal / weightTotal;
    }

    private static bool DeviceMatches(string recordDevice, string currentDevice) =>
        string.IsNullOrWhiteSpace(recordDevice) ||
        string.IsNullOrWhiteSpace(currentDevice) ||
        string.Equals(recordDevice, currentDevice, StringComparison.Ordinal);

    private static bool IsResolutionRatioWithin(
        ConversionPerformanceRecord record,
        int width,
        int height,
        double low,
        double high)
    {
        if (record.Width <= 0 || record.Height <= 0 || width <= 0 || height <= 0)
        {
            return false;
        }

        var currentPixels = width * height;
        var recordPixels = record.Width * record.Height;
        var ratio = recordPixels / (double)currentPixels;
        return ratio >= low && ratio <= high;
    }

    private static double GetQualityFactor(AiQualityPreset qualityPreset) => qualityPreset switch
    {
        AiQualityPreset.Fast => 0.82d,
        AiQualityPreset.HighQuality => 1.28d,
        _ => 1d,
    };

    private static double GetLayoutFactor(ThreeDOutputFormat outputFormat) => outputFormat switch
    {
        ThreeDOutputFormat.Anaglyph => 0.90d,
        ThreeDOutputFormat.HalfSideBySide => 1.05d,
        _ => 1d,
    };

    private static string ModelSpeedBucket(string? modelKey)
    {
        var key = Normalize(modelKey);
        if (key.Contains("3-mono") || key.Contains("large"))
        {
            return "large";
        }

        if (key.Contains("base"))
        {
            return "base";
        }

        if (key.Contains("metric") || key.Contains("indoor") || key.Contains("outdoor"))
        {
            return "metric";
        }

        return "small";
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    private sealed record HistoryMatch(
        ConversionEstimateConfidence Confidence,
        double? ThroughputMegapixelFramesPerSecond,
        bool UsedLocalHistory);
}

public sealed record ModelPerformanceProfile(
    string EnglishName,
    string EnglishSpeedTier,
    string SpanishSpeedTier,
    double DefaultMegapixelFramesPerSecond);
