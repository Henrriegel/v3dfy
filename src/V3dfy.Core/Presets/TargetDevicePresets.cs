using V3dfy.Core.Models;

namespace V3dfy.Core.Presets;

public static class TargetDevicePresets
{
    public static TargetDevicePreset Recommended3dTv { get; } = new(
        Id: "recommended-3d-tv",
        Name: "Recommended 3D TV",
        SpanishName: "TV 3D recomendada",
        Recommendation: new ConversionRecommendation(
            OutputContainer: OutputContainer.MP4,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
            UserInstruction: "use a compatible player or display for the selected 3D layout"),
        AdvancedOutputContainers: [OutputContainer.MKV],
        PlaybackTitle: "Playback note",
        SpanishPlaybackTitle: "Nota de reproduccion",
        PlaybackInstructions:
            "Use Top & Bottom / Half Top-Bottom mode on the TV or player when available.",
        SpanishPlaybackInstructions:
            "Usa el modo Arriba y abajo / Half Top-Bottom en la TV o reproductor cuando este disponible.",
        Description:
            "Default general-purpose profile for most 3D TVs and media players.",
        SpanishDescription:
            "Perfil predeterminado general para la mayoria de TVs 3D y reproductores.",
        BestFor:
            "Best first choice for modern 3D TV playback and general testing.",
        SpanishBestFor:
            "Mejor primera opcion para reproduccion en TV 3D moderna y pruebas generales.",
        CompatibilityNote:
            "Uses broad MP4/H.264 1080p defaults. Older devices may need the Maximum Compatibility or Legacy LG profile.",
        SpanishCompatibilityNote:
            "Usa valores generales MP4/H.264 1080p. Los equipos antiguos pueden necesitar Maxima compatibilidad o Legacy LG.",
        Category: TargetDevicePresetCategory.Recommended,
        EstimatedVideoBitrateLowMbps: 10,
        EstimatedVideoBitrateHighMbps: 16);

    public static TargetDevicePreset General3dVideo => Recommended3dTv;

    public static TargetDevicePreset MaximumCompatibility { get; } = new(
        Id: "maximum-compatibility",
        Name: "Maximum Compatibility",
        SpanishName: "Maxima compatibilidad",
        Recommendation: new ConversionRecommendation(
            OutputContainer: OutputContainer.MP4,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
            UserInstruction: "use conservative MP4 playback settings"),
        AdvancedOutputContainers: [OutputContainer.MKV],
        PlaybackTitle: "Compatibility note",
        SpanishPlaybackTitle: "Nota de compatibilidad",
        PlaybackInstructions:
            "Use this profile for older TVs, USB playback, or media players that are picky about files.",
        SpanishPlaybackInstructions:
            "Usa este perfil para TVs antiguas, reproduccion por USB o reproductores exigentes con los archivos.",
        Description:
            "Conservative MP4/H.264 output for older TVs and media players.",
        SpanishDescription:
            "Salida MP4/H.264 conservadora para TVs antiguas y reproductores.",
        BestFor:
            "Best when playback compatibility matters more than file size or maximum quality.",
        SpanishBestFor:
            "Ideal cuando la compatibilidad importa mas que el tamano o la maxima calidad.",
        CompatibilityNote:
            "Keeps the same safe 1080p target with a more conservative bitrate expectation.",
        SpanishCompatibilityNote:
            "Mantiene el objetivo seguro 1080p con una expectativa de bitrate mas conservadora.",
        Category: TargetDevicePresetCategory.Compatibility,
        EstimatedVideoBitrateLowMbps: 8,
        EstimatedVideoBitrateHighMbps: 12);

    public static TargetDevicePreset HighQualityMaster { get; } = new(
        Id: "high-quality-master",
        Name: "High Quality Master",
        SpanishName: "Master de alta calidad",
        Recommendation: new ConversionRecommendation(
            OutputContainer: OutputContainer.MKV,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
            UserInstruction: "keep a higher-quality master file for archive or later copies"),
        AdvancedOutputContainers: [OutputContainer.MP4],
        PlaybackTitle: "Master output note",
        SpanishPlaybackTitle: "Nota de salida maestra",
        PlaybackInstructions:
            "Use this when you want a larger master/archive file before creating smaller playback copies.",
        SpanishPlaybackInstructions:
            "Usa este perfil cuando quieras un archivo maestro/de archivo mas grande antes de crear copias de reproduccion.",
        Description:
            "Higher-quality output for archive/master use.",
        SpanishDescription:
            "Salida de mayor calidad para archivo o master.",
        BestFor:
            "Best when preserving quality matters more than file size.",
        SpanishBestFor:
            "Ideal cuando conservar calidad importa mas que el tamano.",
        CompatibilityNote:
            "MKV is useful for master files, but MP4 is still the safer direct-playback choice for many TVs.",
        SpanishCompatibilityNote:
            "MKV es util para archivos master, pero MP4 sigue siendo la opcion mas segura para reproduccion directa en muchas TVs.",
        Category: TargetDevicePresetCategory.Master,
        EstimatedVideoBitrateLowMbps: 18,
        EstimatedVideoBitrateHighMbps: 28);

    public static TargetDevicePreset Lg3dFullHd2012 { get; } = new(
        Id: "legacy-lg-3d-tv-2012",
        Name: "Legacy LG 3D TV (2012)",
        SpanishName: "Legacy LG 3D TV (2012)",
        Recommendation: new ConversionRecommendation(
            OutputContainer: OutputContainer.MP4,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: ThreeDOutputFormat.HalfSideBySide,
            UserInstruction: "choose Side-by-Side mode on the TV"),
        AdvancedOutputContainers: [OutputContainer.MKV],
        PlaybackTitle: "How to watch on the TV",
        SpanishPlaybackTitle: "Como verlo en la TV",
        PlaybackInstructions:
            """
            1. Copy the converted video to a USB drive or play it from your media player/server.
            2. Open the video on the LG 3D TV.
            3. Enable the TV 3D mode.
            4. Select Side-by-Side mode.
            5. Use your passive 3D glasses.
            """,
        SpanishPlaybackInstructions:
            """
            1. Copia el video convertido a una USB o reproducelo desde tu servidor/reproductor.
            2. Abre el video en la TV LG 3D.
            3. Activa el modo 3D de la TV.
            4. Selecciona Side-by-Side / Lado a lado.
            5. Usa tus lentes 3D pasivos.
            """,
        Description:
            "Legacy compatibility profile for older LG Full HD 3D TVs.",
        SpanishDescription:
            "Perfil legacy de compatibilidad para televisores LG 3D Full HD antiguos.",
        BestFor:
            "Best for LG passive 3D TVs that support Side-by-Side playback.",
        SpanishBestFor:
            "Ideal para televisores LG 3D pasivos compatibles con reproduccion Lado a lado / Side-by-Side.",
        CompatibilityNote:
            "LG 3D TV 2012 compatibility preparation creates an optional Full HD MP4 copy from the completed primary output. It uses Side-by-Side playback guidance and does not claim perfect TV compatibility.",
        SpanishCompatibilityNote:
            "La preparacion de compatibilidad LG 3D TV 2012 crea una copia MP4 Full HD opcional desde la salida principal completada. Usa instrucciones de reproduccion Lado a lado y no promete compatibilidad perfecta con la TV.",
        Category: TargetDevicePresetCategory.Legacy,
        EstimatedVideoBitrateLowMbps: 10,
        EstimatedVideoBitrateHighMbps: 14,
        UsesLegacyLgCompatibilityGuidance: true);

    public static IReadOnlyList<TargetDevicePreset> All { get; } =
    [
        Recommended3dTv,
        MaximumCompatibility,
        HighQualityMaster,
        Lg3dFullHd2012,
    ];

    public static TargetDevicePreset Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Recommended3dTv;
        }

        var normalizedId = id.Trim();
        if (string.Equals(normalizedId, "lg-3d-full-hd-2012", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedId, "LG 3D Full HD 2012", StringComparison.OrdinalIgnoreCase))
        {
            return Lg3dFullHd2012;
        }

        return All.FirstOrDefault(preset =>
            string.Equals(preset.Id, normalizedId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(preset.Name, normalizedId, StringComparison.OrdinalIgnoreCase))
            ?? Recommended3dTv;
    }
}
