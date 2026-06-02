using V3dfy.Core.Models;

namespace V3dfy.Core.Presets;

public static class TargetDevicePresets
{
    public static TargetDevicePreset Lg3dFullHd2012 { get; } = new(
        Name: "LG 3D Full HD 2012",
        Recommendation: new ConversionRecommendation(
            OutputContainer: OutputContainer.MP4,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
            UserInstruction: "choose Top & Bottom mode on the TV"),
        AdvancedOutputContainers: [OutputContainer.MKV]);
}
