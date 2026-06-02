using V3dfy.Core.Models;
using V3dfy.Core.Presets;
using V3dfy.Core.Recommendations;

namespace V3dfy.Tests.Recommendations;

public sealed class VideoConversionRecommendationServiceTests
{
    private readonly VideoConversionRecommendationService _service = new();

    [Fact]
    public void Recommend_1080pSdrSource_UsesLgTvCompatibleDefaults()
    {
        var recommendation = Recommend(CreateAnalysis());

        Assert.Equal(OutputContainer.MP4, recommendation.OutputContainer);
        Assert.Equal("H.264", recommendation.VideoCodec);
        Assert.Equal("AAC or AC3", recommendation.AudioCodec);
        Assert.Equal(1920, recommendation.Width);
        Assert.Equal(1080, recommendation.Height);
        Assert.Equal(ThreeDOutputFormat.HalfTopBottom, recommendation.ThreeDOutputFormat);
        Assert.Equal(AiQualityPreset.Balanced, recommendation.QualityPreset);
        Assert.Equal(ThreeDIntensity.Medium, recommendation.Intensity);
        Assert.True(recommendation.UseTvCompatibleMp4);
        Assert.True(recommendation.SuggestMkvMasterOutput);
    }

    [Fact]
    public void Recommend_4kSource_AddsDownscaleWarning()
    {
        var recommendation = Recommend(CreateAnalysis(width: 3840, height: 2160));

        Assert.True(recommendation.IsSourceAboveTargetResolution);
        Assert.Contains(
            recommendation.CompatibilityIssues,
            issue => issue.EnglishMessage.Contains("downscaled", StringComparison.Ordinal));
    }

    [Fact]
    public void Recommend_HdrSource_AddsToneMappingWarning()
    {
        var recommendation = Recommend(CreateAnalysis(isHdr: true));

        Assert.True(recommendation.IsHdrSource);
        Assert.Contains(
            recommendation.CompatibilityIssues,
            issue => issue.EnglishMessage.Contains("tone mapping", StringComparison.Ordinal));
    }

    [Fact]
    public void Recommend_MkvSource_AddsMp4CompatibilityNote()
    {
        var recommendation = Recommend(CreateAnalysis(
            inputPath: @"C:\videos\input.mkv",
            formatName: "matroska,webm"));

        Assert.Contains(
            recommendation.CompatibilityIssues,
            issue => issue.EnglishMessage.Contains("MP4 is recommended", StringComparison.Ordinal));
    }

    [Fact]
    public void Recommend_SubtitleSource_AddsSubtitleNote()
    {
        var recommendation = Recommend(CreateAnalysis(subtitleCount: 1));

        Assert.Contains(
            recommendation.CompatibilityIssues,
            issue => issue.EnglishMessage.Contains("Subtitle handling", StringComparison.Ordinal));
    }

    [Fact]
    public void Recommend_SourceWithoutAudio_AddsAudioWarning()
    {
        var recommendation = Recommend(CreateAnalysis(audioCount: 0));

        Assert.Contains(
            recommendation.CompatibilityIssues,
            issue => issue.EnglishMessage.Contains("No audio streams", StringComparison.Ordinal));
    }

    [Fact]
    public void Recommend_GeneralPreset_UsesNeutralCompatibilityGuidance()
    {
        var recommendation = _service.Recommend(
            CreateAnalysis(width: 3840, height: 2160),
            TargetDevicePresets.General3dVideo);

        Assert.Contains(
            recommendation.CompatibilityIssues,
            issue => issue.EnglishMessage.Contains("selected preset target", StringComparison.Ordinal));
    }

    private VideoConversionSetupRecommendation Recommend(VideoAnalysisResult analysis) =>
        _service.Recommend(analysis, TargetDevicePresets.Lg3dFullHd2012);

    private static VideoAnalysisResult CreateAnalysis(
        string inputPath = @"C:\videos\input.mp4",
        string formatName = "mov,mp4,m4a,3gp,3g2,mj2",
        int width = 1920,
        int height = 1080,
        bool isHdr = false,
        int audioCount = 1,
        int subtitleCount = 0) => new(
        InputPath: inputPath,
        File: new VideoFileMetadata(
            Duration: TimeSpan.FromMinutes(10),
            FormatName: formatName,
            OverallBitRate: 5_800_000),
        Video: new VideoStreamInfo(
            Index: 0,
            Width: width,
            Height: height,
            FrameRate: 23.976,
            CodecName: "h264",
            PixelFormat: "yuv420p",
            ColorTransfer: null,
            ColorSpace: null,
            ColorPrimaries: null,
            IsHdr: isHdr),
        AudioStreams: Enumerable.Range(0, audioCount)
            .Select(index => new AudioStreamInfo(index, "aac", 2, "stereo", 48000, "eng"))
            .ToArray(),
        SubtitleStreams: Enumerable.Range(0, subtitleCount)
            .Select(index => new SubtitleStreamInfo(index, "subrip", "eng"))
            .ToArray(),
        Warnings: []);
}
