using V3dfy.Core.Models;
using V3dfy.Infrastructure.Analysis;

namespace V3dfy.Tests.Infrastructure;

public sealed class FfprobeJsonParserTests
{
    private readonly FfprobeJsonParser _parser = new();

    [Fact]
    public void Parse_LiveAction1080p_MapsPrimaryVideoAndFormat()
    {
        var result = ParseFixture("ffprobe_live_action_1080p.json");
        var video = Assert.IsType<VideoStreamInfo>(result.Video);

        Assert.Equal(@"C:\videos\input.mp4", result.InputPath);
        Assert.Equal(1920, video.Width);
        Assert.Equal(1080, video.Height);
        Assert.Equal(23.976, video.FrameRate!.Value, precision: 3);
        Assert.Equal("h264", video.CodecName);
        Assert.Equal("mov,mp4,m4a,3gp,3g2,mj2", result.File.FormatName);
        Assert.Equal(TimeSpan.FromSeconds(600.125), result.File.Duration);
        Assert.Equal(5800000L, result.File.OverallBitRate);
    }

    [Fact]
    public void Parse_4kHdr_MapsStreamsLanguagesAndHdrWarning()
    {
        var result = ParseFixture("ffprobe_4k_hdr_multi_audio_subtitles.json");
        var video = Assert.IsType<VideoStreamInfo>(result.Video);

        Assert.True(video.Width.GetValueOrDefault() > 1920);
        Assert.True(video.Height.GetValueOrDefault() > 1080);
        Assert.True(video.IsHdr);
        Assert.Contains(VideoCompatibilityWarning.HdrVideo, result.Warnings);
        Assert.Equal(2, result.AudioStreams.Count);
        Assert.Equal(2, result.SubtitleStreams.Count);
        Assert.Equal(new[] { "eng", "spa" }, result.AudioStreams.Select(stream => stream.Language));
        Assert.Equal(new[] { "eng", "spa" }, result.SubtitleStreams.Select(stream => stream.Language));
        Assert.Equal(29.970, video.FrameRate!.Value, precision: 3);
    }

    [Fact]
    public void Parse_MissingOptionalFields_DoesNotThrowAndLeavesValuesNull()
    {
        var result = ParseFixture("ffprobe_missing_optional_fields.json");

        Assert.Null(result.File.Duration);
        Assert.Null(result.File.FormatName);
        Assert.Null(result.File.OverallBitRate);
        Assert.Null(result.Video?.FrameRate);
        Assert.Null(result.Video?.CodecName);
        Assert.Single(result.AudioStreams);
        Assert.Single(result.SubtitleStreams);
    }

    [Fact]
    public void Parse_ZeroAverageFrameRate_FallsBackToRealFrameRate()
    {
        var result = ParseFixture("ffprobe_4k_hdr_multi_audio_subtitles.json");
        var video = Assert.IsType<VideoStreamInfo>(result.Video);

        Assert.Equal(29.970, video.FrameRate!.Value, precision: 3);
    }

    private VideoAnalysisResult ParseFixture(string fileName)
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Ffprobe",
            fileName);

        return _parser.Parse(@"C:\videos\input.mp4", File.ReadAllText(fixturePath));
    }
}
