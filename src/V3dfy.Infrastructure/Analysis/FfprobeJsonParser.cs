using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.Analysis;

public sealed class FfprobeJsonParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public VideoAnalysisResult Parse(string inputPath, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentNullException.ThrowIfNull(json);

        var ffprobe = JsonSerializer.Deserialize<FfprobeOutput>(json, SerializerOptions)
            ?? new FfprobeOutput();
        var streams = ffprobe.Streams ?? [];
        var video = streams
            .Where(stream => string.Equals(stream.CodecType, "video", StringComparison.OrdinalIgnoreCase))
            .Select(MapVideo)
            .FirstOrDefault();
        var audioStreams = streams
            .Where(stream => string.Equals(stream.CodecType, "audio", StringComparison.OrdinalIgnoreCase))
            .Select(MapAudio)
            .ToArray();
        var subtitleStreams = streams
            .Where(stream => string.Equals(stream.CodecType, "subtitle", StringComparison.OrdinalIgnoreCase))
            .Select(MapSubtitle)
            .ToArray();
        var warnings = video?.IsHdr == true
            ? [VideoCompatibilityWarning.HdrVideo]
            : Array.Empty<VideoCompatibilityWarning>();

        return new VideoAnalysisResult(
            InputPath: inputPath,
            File: new VideoFileMetadata(
                Duration: ParseDuration(ffprobe.Format?.Duration),
                FormatName: ffprobe.Format?.FormatName,
                OverallBitRate: ParseLong(ffprobe.Format?.BitRate)),
            Video: video,
            AudioStreams: audioStreams,
            SubtitleStreams: subtitleStreams,
            Warnings: warnings);
    }

    private static VideoStreamInfo MapVideo(FfprobeStream stream)
    {
        var isHdr = IsHdr(stream);

        return new VideoStreamInfo(
            Index: stream.Index,
            Width: stream.Width,
            Height: stream.Height,
            FrameRate: ParseFrameRate(stream.AverageFrameRate) ?? ParseFrameRate(stream.RealFrameRate),
            CodecName: stream.CodecName,
            PixelFormat: stream.PixelFormat,
            ColorTransfer: stream.ColorTransfer,
            ColorSpace: stream.ColorSpace,
            ColorPrimaries: stream.ColorPrimaries,
            IsHdr: isHdr);
    }

    private static AudioStreamInfo MapAudio(FfprobeStream stream) => new(
        Index: stream.Index,
        CodecName: stream.CodecName,
        Channels: stream.Channels,
        ChannelLayout: stream.ChannelLayout,
        SampleRate: ParseInt(stream.SampleRate),
        Language: stream.Tags?.Language);

    private static SubtitleStreamInfo MapSubtitle(FfprobeStream stream) => new(
        Index: stream.Index,
        CodecName: stream.CodecName,
        Language: stream.Tags?.Language);

    private static bool IsHdr(FfprobeStream stream)
    {
        var transfer = stream.ColorTransfer;
        if (Contains(transfer, "smpte2084") || Contains(transfer, "arib-std-b67"))
        {
            return true;
        }

        var hasHdrMetadata =
            !string.IsNullOrWhiteSpace(stream.ColorTransfer) ||
            !string.IsNullOrWhiteSpace(stream.ColorSpace) ||
            !string.IsNullOrWhiteSpace(stream.ColorPrimaries);

        return IsTenBitPixelFormat(stream.PixelFormat) && hasHdrMetadata;
    }

    private static bool IsTenBitPixelFormat(string? pixelFormat) =>
        Contains(pixelFormat, "10le") ||
        Contains(pixelFormat, "10be") ||
        Contains(pixelFormat, "p010");

    private static bool Contains(string? value, string expected) =>
        value?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;

    private static TimeSpan? ParseDuration(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
        seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : null;

    private static double? ParseFrameRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator))
        {
            return denominator != 0 && numerator > 0 ? numerator / denominator : null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var frameRate) &&
            frameRate > 0
                ? frameRate
                : null;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private sealed class FfprobeOutput
    {
        public FfprobeFormat? Format { get; init; }

        public FfprobeStream[]? Streams { get; init; }
    }

    private sealed class FfprobeFormat
    {
        public string? Duration { get; init; }

        [JsonPropertyName("format_name")]
        public string? FormatName { get; init; }

        [JsonPropertyName("bit_rate")]
        public string? BitRate { get; init; }
    }

    private sealed class FfprobeStream
    {
        public int Index { get; init; }

        [JsonPropertyName("codec_type")]
        public string? CodecType { get; init; }

        [JsonPropertyName("codec_name")]
        public string? CodecName { get; init; }

        public int? Width { get; init; }

        public int? Height { get; init; }

        [JsonPropertyName("pix_fmt")]
        public string? PixelFormat { get; init; }

        [JsonPropertyName("avg_frame_rate")]
        public string? AverageFrameRate { get; init; }

        [JsonPropertyName("r_frame_rate")]
        public string? RealFrameRate { get; init; }

        [JsonPropertyName("color_transfer")]
        public string? ColorTransfer { get; init; }

        [JsonPropertyName("color_space")]
        public string? ColorSpace { get; init; }

        [JsonPropertyName("color_primaries")]
        public string? ColorPrimaries { get; init; }

        public int? Channels { get; init; }

        [JsonPropertyName("channel_layout")]
        public string? ChannelLayout { get; init; }

        [JsonPropertyName("sample_rate")]
        public string? SampleRate { get; init; }

        public FfprobeTags? Tags { get; init; }
    }

    private sealed class FfprobeTags
    {
        public string? Language { get; init; }
    }
}
