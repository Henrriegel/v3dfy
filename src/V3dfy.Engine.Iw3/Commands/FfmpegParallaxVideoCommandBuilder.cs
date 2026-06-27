using System.Globalization;
using V3dfy.Core.Image;

namespace V3dfy.Engine.Iw3.Commands;

public sealed class FfmpegParallaxVideoCommandBuilder
{
    public Iw3Command Build(
        ImageParallaxExportRequest request,
        string framesDirectory,
        string outputPath,
        int? frameWidth = null,
        int? frameHeight = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(framesDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var framePattern = Path.Combine(framesDirectory, "frame_%05d.png");
        var arguments = new List<string>
        {
            "-y",
            "-framerate",
            request.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
            "-i",
            framePattern,
        };
        var padFilter = CreateEvenDimensionPadFilter(frameWidth, frameHeight);
        if (padFilter is not null)
        {
            arguments.Add("-vf");
            arguments.Add(padFilter);
        }

        arguments.AddRange(
        [
            "-c:v",
            "libx264",
            "-pix_fmt",
            "yuv420p",
            "-movflags",
            "+faststart",
            outputPath,
        ]);

        return new(
            ExecutablePath: request.ExpectedToolPaths.FfmpegExecutable,
            Arguments: arguments,
            FullCommandPreview: BuildPreview(request.ExpectedToolPaths.FfmpegExecutable, arguments),
            DryRun: false,
            UnconfirmedPlanningOptions: []);
    }

    private static string? CreateEvenDimensionPadFilter(int? frameWidth, int? frameHeight)
    {
        if (frameWidth is not { } width ||
            frameHeight is not { } height ||
            width <= 0 ||
            height <= 0 ||
            (width % 2 == 0 && height % 2 == 0))
        {
            return null;
        }

        return $"pad={ToEven(width)}:{ToEven(height)}";
    }

    private static int ToEven(int value) =>
        value % 2 == 0 ? value : value + 1;

    private static string BuildPreview(string executablePath, IEnumerable<string> arguments) =>
        string.Join(" ", [Quote(executablePath), .. arguments.Select(Quote)]);

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
