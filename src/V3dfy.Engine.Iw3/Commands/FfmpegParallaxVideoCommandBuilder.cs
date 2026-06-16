using System.Globalization;
using V3dfy.Core.Image;

namespace V3dfy.Engine.Iw3.Commands;

public sealed class FfmpegParallaxVideoCommandBuilder
{
    public Iw3Command Build(
        ImageParallaxExportRequest request,
        string framesDirectory,
        string outputPath)
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
            "-c:v",
            "libx264",
            "-pix_fmt",
            "yuv420p",
            "-movflags",
            "+faststart",
            outputPath,
        };

        return new(
            ExecutablePath: request.ExpectedToolPaths.FfmpegExecutable,
            Arguments: arguments,
            FullCommandPreview: BuildPreview(request.ExpectedToolPaths.FfmpegExecutable, arguments),
            DryRun: false,
            UnconfirmedPlanningOptions: []);
    }

    private static string BuildPreview(string executablePath, IEnumerable<string> arguments) =>
        string.Join(" ", [Quote(executablePath), .. arguments.Select(Quote)]);

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
