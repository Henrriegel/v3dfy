using V3dfy.Core.Image;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using System.Globalization;

namespace V3dfy.Engine.Iw3.Commands;

public sealed class Iw3ImageStereoCommandBuilder
{
    public Iw3Command Build(
        ImageStereoExportRequest request,
        string outputPath,
        LocalModelPlanSelection selectedModel)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(selectedModel);

        var arguments = Iw3CliContract.CreateConfirmedBaseArguments(
            request.SourcePath,
            outputPath).ToList();
        arguments.Add(GetLayoutSwitch(request.OutputFormat));
        arguments.Add(Iw3CliContract.DivergenceSwitch);
        arguments.Add(FormatNumber(MapEyeSeparationToDivergence(request.EyeSeparationPercent)));
        arguments.Add(Iw3CliContract.ConvergenceSwitch);
        arguments.Add(FormatNumber(MapConvergence(request.Convergence)));

        if (!Iw3DepthModelMapper.TryMap(selectedModel, out var mapping) ||
            mapping is null)
        {
            throw new ArgumentException(
                "Selected local model is not mapped to a verified iw3 depth model.",
                nameof(selectedModel));
        }

        arguments.Add(Iw3CliContract.DepthModelSwitch);
        arguments.Add(mapping.DepthModelName);
        if (request.SwapEyes)
        {
            arguments.Add(Iw3CliContract.CrossEyedSwitch);
        }

        return new(
            ExecutablePath: request.ExpectedToolPaths.PythonExecutable,
            Arguments: arguments,
            FullCommandPreview: BuildPreview(request.ExpectedToolPaths.PythonExecutable, arguments),
            DryRun: false,
            UnconfirmedPlanningOptions: []);
    }

    public static string GetLayoutCapabilityToken(ImageStereoExportFormat format) => format switch
    {
        ImageStereoExportFormat.SideBySide => "image:half-sbs",
        ImageStereoExportFormat.HalfTopBottom => "image:half-tb",
        ImageStereoExportFormat.Anaglyph => "image:anaglyph",
        ImageStereoExportFormat.LeftRightPair => "image:left-right-pair",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private static string GetLayoutSwitch(ImageStereoExportFormat outputFormat) =>
        outputFormat switch
        {
            ImageStereoExportFormat.SideBySide => Iw3CliContract.HalfSideBySideSwitch,
            ImageStereoExportFormat.HalfTopBottom => Iw3CliContract.HalfTopBottomSwitch,
            ImageStereoExportFormat.Anaglyph => Iw3CliContract.AnaglyphSwitch,
            ImageStereoExportFormat.LeftRightPair => throw new NotSupportedException(
                "Left/right pair image export requires a verified iw3 image CLI capability before it can be enabled."),
            _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, null),
        };

    private static double MapEyeSeparationToDivergence(double eyeSeparationPercent) =>
        Math.Clamp(eyeSeparationPercent / 4d, 0.25d, 2d);

    private static double MapConvergence(string convergence) =>
        convergence.Trim().ToLowerInvariant() switch
        {
            "near" => 0.35d,
            "far" => 0.65d,
            _ => 0.5d,
        };

    private static string FormatNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string BuildPreview(string executablePath, IEnumerable<string> arguments) =>
        string.Join(" ", [Quote(executablePath), .. arguments.Select(Quote)]);

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
