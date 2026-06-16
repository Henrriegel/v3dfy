using V3dfy.Core.Image;
using V3dfy.Core.Planning;

namespace V3dfy.Engine.Iw3.Commands;

public sealed class Iw3ImageDepthExportCommandBuilder
{
    public Iw3Command Build(
        ImageParallaxExportRequest request,
        string depthExportDirectory,
        LocalModelPlanSelection selectedModel)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(depthExportDirectory);
        ArgumentNullException.ThrowIfNull(selectedModel);

        if (!Iw3DepthModelMapper.TryMap(selectedModel, out var mapping) ||
            mapping is null)
        {
            throw new ArgumentException(
                "Selected local model is not mapped to a verified iw3 depth model.",
                nameof(selectedModel));
        }

        var arguments = Iw3CliContract.CreateConfirmedBaseArguments(
            request.SourcePath,
            depthExportDirectory).ToList();
        arguments.Add(Iw3CliContract.ExportSwitch);
        arguments.Add(Iw3CliContract.ExportDepthOnlySwitch);
        arguments.Add(Iw3CliContract.ExportDepthFitSwitch);
        arguments.Add(Iw3CliContract.DepthModelSwitch);
        arguments.Add(mapping.DepthModelName);

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
