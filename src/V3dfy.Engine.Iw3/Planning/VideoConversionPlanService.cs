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
        InternalToolPaths paths,
        EngineHealthStatus healthStatus)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(targetPreset);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(healthStatus);

        var outputPath = SuggestOutputPath(analysis.InputPath, recommendation.OutputContainer);
        var request = new ConversionRequest(
            InputPath: analysis.InputPath,
            OutputPath: outputPath,
            OutputContainer: recommendation.OutputContainer,
            ThreeDOutputFormat: recommendation.ThreeDOutputFormat,
            AiQualityPreset: recommendation.QualityPreset,
            ThreeDIntensity: recommendation.Intensity);
        var command = _commandBuilder.Build(request, paths, healthStatus);

        return new(
            SourcePath: analysis.InputPath,
            SuggestedOutputPath: outputPath,
            OutputContainer: recommendation.OutputContainer,
            VideoCodec: recommendation.VideoCodec,
            AudioCodec: recommendation.AudioCodec,
            Width: recommendation.Width,
            Height: recommendation.Height,
            ThreeDOutputFormat: recommendation.ThreeDOutputFormat,
            QualityPreset: recommendation.QualityPreset,
            Intensity: recommendation.Intensity,
            Status: command.DryRun ? VideoConversionPlanStatus.DryRun : VideoConversionPlanStatus.Ready,
            DryRunReason: GetDryRunReason(healthStatus),
            Steps:
            [
                new(
                    "Read the analyzed source video.",
                    "Leer el video de origen analizado."),
                new(
                    "Generate Half Top-Bottom 3D frames with the bundled local iw3 engine.",
                    "Generar cuadros 3D Half Top-Bottom con el motor local iw3 incluido."),
                new(
                    $"Prepare the {recommendation.Width}x{recommendation.Height} {recommendation.VideoCodec} TV-compatible output.",
                    $"Preparar la salida compatible con TV en {recommendation.Width}x{recommendation.Height} {recommendation.VideoCodec}."),
                new(
                    $"Write the converted video to {outputPath}.",
                    $"Guardar el video convertido en {outputPath}."),
            ],
            CommandPreview: command.FullCommandPreview);
    }

    private static string SuggestOutputPath(string inputPath, OutputContainer outputContainer)
    {
        var directory = Path.GetDirectoryName(inputPath);
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var extension = outputContainer.ToString().ToLowerInvariant();
        var outputName = $"{fileName}.v3dfy.3d.htab.{extension}";

        return string.IsNullOrWhiteSpace(directory)
            ? outputName
            : Path.Combine(directory, outputName);
    }

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
