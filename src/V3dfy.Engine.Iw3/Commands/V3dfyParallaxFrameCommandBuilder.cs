using System.Globalization;
using V3dfy.Core.Image;

namespace V3dfy.Engine.Iw3.Commands;

public sealed class V3dfyParallaxFrameCommandBuilder
{
    public Iw3Command Build(
        ImageParallaxExportRequest request,
        string depthMapPath,
        string framesDirectory,
        int frameCount)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(depthMapPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(framesDirectory);

        var arguments = new List<string>
        {
            request.ExpectedToolPaths.V3dfyParallaxHelperScript,
            "--source",
            request.SourcePath,
            "--depth",
            depthMapPath,
            "--frames-dir",
            framesDirectory,
            "--frame-count",
            frameCount.ToString(CultureInfo.InvariantCulture),
            "--fps",
            request.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
            "--intensity",
            request.DepthIntensity,
            "--direction",
            request.MotionDirection,
            "--zoom",
            request.ZoomAmplitude,
            "--smoothing",
            request.Smoothing,
            "--layer-behavior",
            request.LayerBehavior,
        };

        return new(
            ExecutablePath: request.ExpectedToolPaths.PythonExecutable,
            Arguments: arguments,
            FullCommandPreview: BuildPreview(request.ExpectedToolPaths.PythonExecutable, arguments),
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
