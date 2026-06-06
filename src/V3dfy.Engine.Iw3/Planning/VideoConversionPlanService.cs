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
        EngineHealthStatus healthStatus,
        LocalModelSelectionCandidate? selectedLocalModel = null)
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
        var selectedLocalModelPlan = selectedLocalModel is null
            ? null
            : LocalModelPlanSelection.FromCandidate(selectedLocalModel);
        var audioCodec = GetAudioCodecForContainer(
            options.OutputContainer,
            recommendation.AudioCodec);
        var command = _commandBuilder.Build(
            request,
            paths,
            healthStatus,
            selectedLocalModelPlan);
        var steps = CreateSteps(
            recommendation,
            targetPreset,
            options,
            outputPath,
            selectedLocalModelPlan,
            audioCodec);

        return new(
            SourcePath: analysis.InputPath,
            SuggestedOutputPath: outputPath,
            OutputContainer: options.OutputContainer,
            VideoCodec: recommendation.VideoCodec,
            AudioCodec: audioCodec,
            Width: recommendation.Width,
            Height: recommendation.Height,
            ThreeDOutputFormat: options.ThreeDOutputFormat,
            QualityPreset: options.QualityPreset,
            Intensity: options.Intensity,
            Status: command.DryRun ? VideoConversionPlanStatus.DryRun : VideoConversionPlanStatus.Ready,
            DryRunReason: GetDryRunReason(healthStatus),
            Steps: steps,
            CommandPreview: command.FullCommandPreview)
        {
            SelectedLocalModel = selectedLocalModelPlan,
        };
    }

    private static IReadOnlyList<VideoConversionPlanStep> CreateSteps(
        VideoConversionSetupRecommendation recommendation,
        TargetDevicePreset targetPreset,
        VideoConversionPlanOptions options,
        string outputPath,
        LocalModelPlanSelection? selectedLocalModel,
        string audioCodec)
    {
        var steps = new List<VideoConversionPlanStep>
        {
            new(
                "Read the analyzed source video.",
                "Leer el video de origen analizado."),
        };

        if (selectedLocalModel is not null)
        {
            steps.Add(new(
                $"Plan selected local model for future execution: {selectedLocalModel.DisplayName} ({selectedLocalModel.RelativePath}).",
                $"Preparar el modelo local seleccionado para ejecuci\u00f3n futura: {selectedLocalModel.DisplayName} ({selectedLocalModel.RelativePath})."));
        }

        steps.Add(new(
            $"Generate {GetLayoutName(options.ThreeDOutputFormat)} 3D frames with the bundled local iw3 engine.",
            $"Generar cuadros 3D {GetLayoutName(options.ThreeDOutputFormat)} con el motor local iw3 incluido."));
        steps.Add(new(
            $"Prepare the {recommendation.Width}x{recommendation.Height} {recommendation.VideoCodec} output for {targetPreset.Name}.",
            $"Preparar la salida {recommendation.Width}x{recommendation.Height} {recommendation.VideoCodec} para {targetPreset.SpanishName}."));
        if (options.CreateLgCompatibilityCopy)
        {
            steps.Add(CreateLgCompatibilityStep(options));
        }

        steps.Add(new(
            $"Write the converted video to {outputPath}.",
            $"Guardar el video convertido en {outputPath}."));

        return steps;
    }

    private static string GetLayoutName(ThreeDOutputFormat format) => format switch
    {
        ThreeDOutputFormat.HalfTopBottom => "Half Top-Bottom",
        ThreeDOutputFormat.HalfSideBySide => "Half Side-by-Side",
        ThreeDOutputFormat.Anaglyph => "Anaglyph",
        ThreeDOutputFormat.FullSideBySide => throw new NotSupportedException(
            "Full Side-by-Side is not available because the bundled iw3 contract has no verified direct SBS flag."),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private static string GetAudioCodecForContainer(
        OutputContainer outputContainer,
        string recommendedAudioCodec) => recommendedAudioCodec;

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
        ThreeDOutputFormat.Anaglyph => "anaglyph",
        ThreeDOutputFormat.FullSideBySide => throw new NotSupportedException(
            "Full Side-by-Side is not available because the bundled iw3 contract has no verified direct SBS flag."),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private static VideoConversionPlanStep CreateLgCompatibilityStep(
        VideoConversionPlanOptions options)
    {
        if (options.ThreeDOutputFormat == ThreeDOutputFormat.HalfSideBySide)
        {
            return new(
                "After the primary iw3 output succeeds, create an optional LG 3D TV 2012 MP4 copy with H.264, copied audio from the primary output, yuv420p, faststart, and a 1920x1080 Half Side-by-Side target.",
                "Despues de completar la salida principal de iw3, crear una copia MP4 opcional para LG 3D TV 2012 con H.264, audio copiado desde la salida principal, yuv420p, faststart y objetivo 1920x1080 Medio lado a lado.");
        }

        return new(
            $"LG 3D TV 2012 MP4 copy is selected, but post-processing is currently enabled only for Half Side-by-Side. Selected layout: {GetLayoutName(options.ThreeDOutputFormat)}.",
            $"La copia MP4 para LG 3D TV 2012 esta seleccionada, pero el postprocesamiento actualmente solo esta habilitado para Medio lado a lado. Diseno seleccionado: {GetLayoutName(options.ThreeDOutputFormat)}.");
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
