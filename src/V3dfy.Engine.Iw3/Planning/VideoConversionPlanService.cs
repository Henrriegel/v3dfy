using System.IO;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Recommendations;
using V3dfy.Engine.Iw3.Commands;

namespace V3dfy.Engine.Iw3.Planning;

public sealed class VideoConversionPlanService
{
    private readonly Iw3CommandBuilder _commandBuilder = new();

    public VideoConversionPlan Create(
        VideoAnalysisResult analysis,
        VideoConversionSetupRecommendation recommendation,
        TargetDevicePreset targetPreset,
        VideoConversionPlanOptions options,
        InternalToolPaths paths,
        EngineHealthStatus healthStatus)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(targetPreset);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(healthStatus);

        var outputPath = string.IsNullOrWhiteSpace(options.CustomOutputPath)
            ? CreateSuggestedOutputPath(
                analysis.InputPath,
                options.OutputContainer,
                options.ThreeDOutputFormat)
            : options.CustomOutputPath;
        var request = new ConversionRequest(
            InputPath: analysis.InputPath,
            OutputPath: outputPath,
            OutputContainer: options.OutputContainer,
            ThreeDOutputFormat: options.ThreeDOutputFormat,
            AiQualityPreset: options.QualityPreset,
            ThreeDIntensity: options.Intensity);
        var command = _commandBuilder.Build(request, paths, healthStatus);

        return new(
            SourcePath: analysis.InputPath,
            SuggestedOutputPath: outputPath,
            OutputContainer: options.OutputContainer,
            VideoCodec: recommendation.VideoCodec,
            AudioCodec: recommendation.AudioCodec,
            Width: recommendation.Width,
            Height: recommendation.Height,
            ThreeDOutputFormat: options.ThreeDOutputFormat,
            QualityPreset: options.QualityPreset,
            Intensity: options.Intensity,
            Status: command.DryRun ? VideoConversionPlanStatus.DryRun : VideoConversionPlanStatus.Ready,
            DryRunReason: GetDryRunReason(healthStatus),
            Steps:
            [
                new(
                    "Read the analyzed source video.",
                    "Leer el video de origen analizado."),
                new(
                    $"Generate {GetLayoutName(options.ThreeDOutputFormat)} 3D frames with the bundled local iw3 engine.",
                    $"Generar cuadros 3D {GetLayoutName(options.ThreeDOutputFormat)} con el motor local iw3 incluido."),
                new(
                    $"Prepare the {recommendation.Width}x{recommendation.Height} {recommendation.VideoCodec} output for {targetPreset.Name}.",
                    $"Preparar la salida {recommendation.Width}x{recommendation.Height} {recommendation.VideoCodec} para {targetPreset.SpanishName}."),
                new(
                    $"Write the converted video to {outputPath}.",
                    $"Guardar el video convertido en {outputPath}."),
            ],
            CommandPreview: command.FullCommandPreview);
    }

    private static string GetLayoutName(ThreeDOutputFormat format) => format switch
    {
        ThreeDOutputFormat.HalfTopBottom => "Half Top-Bottom",
        ThreeDOutputFormat.HalfSideBySide => "Half Side-by-Side",
        ThreeDOutputFormat.FullSideBySide => "Full Side-by-Side",
        ThreeDOutputFormat.Anaglyph => "Anaglyph",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static string CreateSuggestedOutputPath(
        string inputPath,
        OutputContainer outputContainer,
        ThreeDOutputFormat threeDOutputFormat)
    {
        var directory = Path.GetDirectoryName(inputPath);
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var extension = outputContainer.ToString().ToLowerInvariant();
        var layoutSuffix = GetLayoutSuffix(threeDOutputFormat);
        var outputName = $"{fileName}.v3dfy.3d.{layoutSuffix}.{extension}";

        return string.IsNullOrWhiteSpace(directory)
            ? outputName
            : Path.Combine(directory, outputName);
    }

    private static string GetLayoutSuffix(ThreeDOutputFormat format) => format switch
    {
        ThreeDOutputFormat.HalfTopBottom => "htab",
        ThreeDOutputFormat.HalfSideBySide => "hsbs",
        ThreeDOutputFormat.FullSideBySide => "sbs",
        ThreeDOutputFormat.Anaglyph => "anaglyph",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private static ConversionDryRunReason GetDryRunReason(EngineHealthStatus healthStatus)
    {
        if (healthStatus.Python == ToolHealthStatus.Missing ||
            healthStatus.Iw3EngineDirectory == ToolHealthStatus.Missing ||
            healthStatus.ModelsDirectory == ToolHealthStatus.Missing)
        {
            return ConversionDryRunReason.MissingLocalAiBundle;
        }

        return healthStatus.IsComplete
            ? ConversionDryRunReason.None
            : ConversionDryRunReason.MissingRequiredTools;
    }
}
