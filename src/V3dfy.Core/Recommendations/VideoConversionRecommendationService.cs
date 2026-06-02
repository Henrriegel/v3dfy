using System.IO;
using V3dfy.Core.Models;

namespace V3dfy.Core.Recommendations;

public sealed class VideoConversionRecommendationService
{
    public VideoConversionSetupRecommendation Recommend(
        VideoAnalysisResult analysis,
        TargetDevicePreset targetPreset)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(targetPreset);

        var preset = targetPreset.Recommendation;
        var isAboveTargetResolution = analysis.Video is
            { Width: { } sourceWidth, Height: { } sourceHeight } &&
            (sourceWidth > preset.Width || sourceHeight > preset.Height);
        var isHdrSource = analysis.Video?.IsHdr == true;
        var issues = new List<VideoCompatibilityIssue>();

        if (isAboveTargetResolution)
        {
            issues.Add(new(
                VideoCompatibilitySeverity.Warning,
                targetPreset.UsesLegacyLgCompatibilityGuidance
                    ? "Source resolution is higher than the LG Full HD target. The recommended output should be downscaled to 1920x1080."
                    : $"Source resolution is higher than the selected preset target. The recommended output should be downscaled to {preset.Width}x{preset.Height}.",
                targetPreset.UsesLegacyLgCompatibilityGuidance
                    ? "La resolución de origen supera el objetivo LG Full HD. La salida recomendada debe reducirse a 1920x1080."
                    : $"La resolución de origen supera el objetivo del perfil seleccionado. La salida recomendada debe reducirse a {preset.Width}x{preset.Height}."));
        }

        if (isHdrSource)
        {
            issues.Add(new(
                VideoCompatibilitySeverity.Warning,
                targetPreset.UsesLegacyLgCompatibilityGuidance
                    ? "HDR was detected. SDR tone mapping may be required for older 3D TVs."
                    : "HDR was detected. SDR tone mapping may be required for some playback devices.",
                targetPreset.UsesLegacyLgCompatibilityGuidance
                    ? "Se detectó HDR. Puede requerirse conversión a SDR para televisores 3D antiguos."
                    : "Se detectó HDR. Puede requerirse conversión a SDR para algunos dispositivos de reproducción."));
        }

        if (IsMkvSource(analysis))
        {
            issues.Add(new(
                VideoCompatibilitySeverity.Information,
                targetPreset.UsesLegacyLgCompatibilityGuidance
                    ? "MKV is a good master/archive source, but MP4 is recommended for direct playback on older LG 3D TVs."
                    : "MKV is a good master/archive source, but MP4 is recommended for broad playback compatibility.",
                targetPreset.UsesLegacyLgCompatibilityGuidance
                    ? "MKV es una buena fuente maestra/de archivo, pero se recomienda MP4 para reproducción directa en televisores LG 3D antiguos."
                    : "MKV es una buena fuente maestra/de archivo, pero se recomienda MP4 para una amplia compatibilidad de reproducción."));
        }

        if (analysis.AudioStreams.Count == 0)
        {
            issues.Add(new(
                VideoCompatibilitySeverity.Warning,
                "No audio streams were detected.",
                "No se detectaron pistas de audio."));
        }

        if (analysis.SubtitleStreams.Count > 0)
        {
            issues.Add(new(
                VideoCompatibilitySeverity.Information,
                "Subtitle handling will be configured in a later step.",
                "El manejo de subtítulos se configurará en un paso posterior."));
        }

        return new(
            OutputContainer: preset.OutputContainer,
            VideoCodec: preset.VideoCodec,
            AudioCodec: preset.AudioCodec,
            Width: preset.Width,
            Height: preset.Height,
            ThreeDOutputFormat: preset.ThreeDOutputFormat,
            QualityPreset: AiQualityPreset.Balanced,
            Intensity: ThreeDIntensity.Medium,
            IsSourceAboveTargetResolution: isAboveTargetResolution,
            IsHdrSource: isHdrSource,
            UseTvCompatibleMp4: preset.OutputContainer == OutputContainer.MP4,
            SuggestMkvMasterOutput: targetPreset.AdvancedOutputContainers.Contains(OutputContainer.MKV),
            CompatibilityIssues: issues);
    }

    private static bool IsMkvSource(VideoAnalysisResult analysis) =>
        analysis.File.FormatName?.Contains("matroska", StringComparison.OrdinalIgnoreCase) == true ||
        string.Equals(Path.GetExtension(analysis.InputPath), ".mkv", StringComparison.OrdinalIgnoreCase);
}
