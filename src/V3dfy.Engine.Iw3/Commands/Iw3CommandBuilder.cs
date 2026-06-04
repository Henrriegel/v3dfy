using V3dfy.Core.Models;

namespace V3dfy.Engine.Iw3.Commands;

public sealed class Iw3CommandBuilder
{
    public Iw3Command Build(
        ConversionRequest request,
        InternalToolPaths paths,
        EngineHealthStatus healthStatus)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(healthStatus);

        var arguments = Iw3CliContract.CreateConfirmedBaseArguments(
            request.InputPath,
            request.OutputPath);

        return new Iw3Command(
            ExecutablePath: paths.PythonExecutable,
            Arguments: arguments,
            FullCommandPreview: BuildPreview(paths.PythonExecutable, arguments),
            DryRun: !healthStatus.IsComplete,
            UnconfirmedPlanningOptions: Iw3CliContract.UnconfirmedPlanningOptions);
    }

    private static string BuildPreview(string executablePath, IEnumerable<string> arguments) =>
        string.Join(" ", [Quote(executablePath), .. arguments.Select(Quote)]);

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
