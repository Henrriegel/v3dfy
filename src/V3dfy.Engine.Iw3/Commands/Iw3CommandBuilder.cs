using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Engine.Iw3.Commands;

public sealed class Iw3CommandBuilder
{
    public Iw3Command Build(
        ConversionRequest request,
        InternalToolPaths paths,
        EngineHealthStatus healthStatus,
        LocalModelPlanSelection? selectedLocalModel = null,
        bool requireVerifiedDepthModel = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(healthStatus);

        var arguments = Iw3CliContract.CreateConfirmedBaseArguments(
            request.InputPath,
            request.OutputPath).ToList();

        arguments.Add(GetLayoutSwitch(request.ThreeDOutputFormat));

        if (Iw3DepthModelMapper.TryMap(selectedLocalModel, out var depthModelMapping) &&
            depthModelMapping is not null)
        {
            arguments.Add(Iw3CliContract.DepthModelSwitch);
            arguments.Add(depthModelMapping.DepthModelName);
        }
        else if (requireVerifiedDepthModel)
        {
            throw new ArgumentException(
                "Selected local model is not mapped to a verified iw3 depth model yet.",
                nameof(selectedLocalModel));
        }

        return new Iw3Command(
            ExecutablePath: paths.PythonExecutable,
            Arguments: arguments,
            FullCommandPreview: BuildPreview(paths.PythonExecutable, arguments),
            DryRun: !healthStatus.IsComplete,
            UnconfirmedPlanningOptions: Iw3CliContract.UnconfirmedPlanningOptions);
    }

    private static string GetLayoutSwitch(ThreeDOutputFormat outputFormat) =>
        outputFormat switch
        {
            ThreeDOutputFormat.HalfSideBySide =>
                Iw3CliContract.HalfSideBySideSwitch,
            ThreeDOutputFormat.HalfTopBottom =>
                Iw3CliContract.HalfTopBottomSwitch,
            ThreeDOutputFormat.Anaglyph =>
                Iw3CliContract.AnaglyphSwitch,
            ThreeDOutputFormat.FullSideBySide => throw new NotSupportedException(
                "Full Side-by-Side is not exposed because the bundled iw3 contract has no verified direct SBS flag."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(outputFormat),
                outputFormat,
                null),
        };

    private static string BuildPreview(string executablePath, IEnumerable<string> arguments) =>
        string.Join(" ", [Quote(executablePath), .. arguments.Select(Quote)]);

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
