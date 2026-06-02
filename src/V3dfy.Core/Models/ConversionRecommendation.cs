namespace V3dfy.Core.Models;

public sealed record ConversionRecommendation(
    OutputContainer OutputContainer,
    string VideoCodec,
    string AudioCodec,
    int Width,
    int Height,
    ThreeDOutputFormat ThreeDOutputFormat,
    string UserInstruction);
