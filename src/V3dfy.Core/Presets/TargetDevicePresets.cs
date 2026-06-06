using V3dfy.Core.Models;

namespace V3dfy.Core.Presets;

public static class TargetDevicePresets
{
    public static TargetDevicePreset General3dVideo { get; } = new(
        Name: "General 3D video",
        SpanishName: "Video 3D general",
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
            "Use a player, display, TV, projector, headset or media server compatible with the selected 3D layout.",
        SpanishPlaybackInstructions:
            "Usa un reproductor, pantalla, TV, proyector, visor o servidor multimedia compatible con el diseno 3D seleccionado.",
        Description:
            "Neutral output profile for general 2D to 3D conversion.",
        SpanishDescription:
            "Perfil de salida neutral para conversion general de 2D a 3D.",
        BestFor:
            "Best for players, displays, projectors, headsets or media servers that support the selected 3D layout.",
        SpanishBestFor:
            "Ideal para reproductores, pantallas, proyectores, visores o servidores multimedia compatibles con el diseno 3D seleccionado.",
        CompatibilityNote:
            "Uses broad compatibility defaults. Device-specific profiles may provide more precise playback instructions.",
        SpanishCompatibilityNote:
            "Usa valores predeterminados de amplia compatibilidad. Los perfiles especificos de dispositivo pueden ofrecer instrucciones de reproduccion mas precisas.");

    public static TargetDevicePreset Lg3dFullHd2012 { get; } = new(
        Name: "LG 3D Full HD 2012",
        SpanishName: "LG 3D Full HD 2012",
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
            "Device-specific starting profile for older LG Full HD 3D TVs.",
        SpanishDescription:
            "Perfil inicial especifico para televisores LG 3D Full HD antiguos.",
        BestFor:
            "Best for LG passive 3D TVs that support Side-by-Side playback.",
        SpanishBestFor:
            "Ideal para televisores LG 3D pasivos compatibles con reproduccion Lado a lado / Side-by-Side.",
        CompatibilityNote:
            "LG 3D TV 2012 compatibility preparation creates an optional Full HD MP4 copy from the completed primary output. It uses Side-by-Side playback guidance and does not claim perfect TV compatibility.",
        SpanishCompatibilityNote:
            "La preparacion de compatibilidad LG 3D TV 2012 crea una copia MP4 Full HD opcional desde la salida principal completada. Usa instrucciones de reproduccion Lado a lado y no promete compatibilidad perfecta con la TV.",
        UsesLegacyLgCompatibilityGuidance: true);

    public static IReadOnlyList<TargetDevicePreset> All { get; } =
    [
        General3dVideo,
        Lg3dFullHd2012,
    ];
}
