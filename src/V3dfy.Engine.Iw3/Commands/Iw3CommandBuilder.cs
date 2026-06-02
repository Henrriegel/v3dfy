using System.Globalization;
using V3dfy.Core.Models;

namespace V3dfy.Engine.Iw3.Commands;

public sealed class Iw3CommandBuilder
{
    public Iw3Command Build(
        ConversionRequest request,
        InternalToolPaths paths,
        EngineHealthStatus healthStatus)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(healthStatus);

        var arguments = new List<string>
        {
            "-m",
            "iw3",
            "-i",
            request.InputPath,
            "-o",
            request.OutputPath,
            GetOutputFormatArgument(request.ThreeDOutputFormat),
            "--video-codec",
            "libx264",
            "--preset",
            GetQualityPresetArgument(request.AiQualityPreset),
            "--scene-detect",
            "--ema-normalize",
            "-d",
            GetDepthArgument(request),
            "-c",
            "0.5",
        };

        return new Iw3Command(
            ExecutablePath: paths.PythonExecutable,
            Arguments: arguments,
            FullCommandPreview: BuildPreview(paths.PythonExecutable, arguments),
            DryRun: !healthStatus.IsComplete);
    }

    private static string GetOutputFormatArgument(ThreeDOutputFormat format) => format switch
    {
        ThreeDOutputFormat.HalfTopBottom => "--half-tb",
        ThreeDOutputFormat.HalfSideBySide => "--half-sbs",
        // iw3 uses --sbs for full-width side-by-side output.
        ThreeDOutputFormat.FullSideBySide => "--sbs",
        ThreeDOutputFormat.Anaglyph => "--anaglyph",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private static string GetQualityPresetArgument(AiQualityPreset preset) => preset switch
    {
        AiQualityPreset.Fast => "fast",
        AiQualityPreset.Balanced => "medium",
        AiQualityPreset.HighQuality => "slow",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };

    private static string GetDepthArgument(ConversionRequest request)
    {
        var depth = request.ThreeDIntensity switch
        {
            ThreeDIntensity.Low => 1.0,
            ThreeDIntensity.Medium => 1.5,
            ThreeDIntensity.High => 2.0,
            ThreeDIntensity.Custom when request.CustomDepth.HasValue => request.CustomDepth.Value,
            ThreeDIntensity.Custom => throw new ArgumentException(
                "Custom depth is required when the 3D intensity is Custom.",
                nameof(request)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.ThreeDIntensity,
                "Unsupported 3D intensity."),
        };

        return depth.ToString("0.0###", CultureInfo.InvariantCulture);
    }

    private static string BuildPreview(string executablePath, IEnumerable<string> arguments) =>
        string.Join(" ", [Quote(executablePath), .. arguments.Select(Quote)]);

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
