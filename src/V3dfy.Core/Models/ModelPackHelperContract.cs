namespace V3dfy.Core.Models;

public sealed record ModelPackHelperInstallCommand(
    string ZipPath,
    string TargetPretrainedModelsRoot,
    string StagingRoot,
    string CurrentIw3Version,
    string? CurrentV3dfyVersion = null,
    string? ResultPath = null,
    string? LogPath = null);

public static class ModelPackHelperContract
{
    public const string AreaArgument = "model-pack";
    public const string InstallArgument = "install";
    public const string ZipSwitch = "--zip";
    public const string TargetRootSwitch = "--target-root";
    public const string StagingRootSwitch = "--staging-root";
    public const string CurrentIw3VersionSwitch = "--current-iw3-version";
    public const string CurrentV3dfyVersionSwitch = "--current-v3dfy-version";
    public const string ResultSwitch = "--result";
    public const string LogSwitch = "--log";

    public static IReadOnlyList<string> CreateInstallArguments(ModelPackHelperInstallCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var arguments = new List<string>
        {
            AreaArgument,
            InstallArgument,
            ZipSwitch,
            command.ZipPath,
            TargetRootSwitch,
            command.TargetPretrainedModelsRoot,
            StagingRootSwitch,
            command.StagingRoot,
            CurrentIw3VersionSwitch,
            command.CurrentIw3Version,
        };

        if (!string.IsNullOrWhiteSpace(command.CurrentV3dfyVersion))
        {
            arguments.Add(CurrentV3dfyVersionSwitch);
            arguments.Add(command.CurrentV3dfyVersion);
        }

        if (!string.IsNullOrWhiteSpace(command.ResultPath))
        {
            arguments.Add(ResultSwitch);
            arguments.Add(command.ResultPath);
        }

        if (!string.IsNullOrWhiteSpace(command.LogPath))
        {
            arguments.Add(LogSwitch);
            arguments.Add(command.LogPath);
        }

        return arguments;
    }

    public static bool IsModelPackInstallCommand(IReadOnlyList<string> args) =>
        args.Count >= 2 &&
        string.Equals(args[0], AreaArgument, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(args[1], InstallArgument, StringComparison.OrdinalIgnoreCase);
}
