using System.Diagnostics;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed record ElevatedModelPackInstallLaunchRequest(
    string HelperExecutablePath,
    string ModelPackZipPath,
    string TargetPretrainedModelsRoot,
    string StagingRoot,
    string CurrentIw3Version,
    string? CurrentV3dfyVersion = null,
    string? ResultPath = null,
    string? LogPath = null);

public sealed class ElevatedModelPackInstallLauncher
{
    public ProcessStartInfo BuildStartInfo(ElevatedModelPackInstallLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateExistingFile(request.HelperExecutablePath, nameof(request.HelperExecutablePath));
        ValidateExistingFile(request.ModelPackZipPath, nameof(request.ModelPackZipPath));
        ValidateDirectoryPath(request.TargetPretrainedModelsRoot, nameof(request.TargetPretrainedModelsRoot));
        ValidateDirectoryPath(request.StagingRoot, nameof(request.StagingRoot));
        RequireValue(request.CurrentIw3Version, nameof(request.CurrentIw3Version));
        ValidateOptionalOutputPath(request.ResultPath, nameof(request.ResultPath));
        ValidateOptionalOutputPath(request.LogPath, nameof(request.LogPath));

        var command = new ModelPackHelperInstallCommand(
            request.ModelPackZipPath,
            request.TargetPretrainedModelsRoot,
            request.StagingRoot,
            request.CurrentIw3Version,
            request.CurrentV3dfyVersion,
            request.ResultPath,
            request.LogPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.HelperExecutablePath,
            UseShellExecute = true,
            Verb = "runas",
        };

        foreach (var argument in ModelPackHelperContract.CreateInstallArguments(command))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void ValidateExistingFile(string path, string parameterName)
    {
        ValidatePath(path, parameterName);
        if (!File.Exists(path))
        {
            throw new ArgumentException($"Required file was not found: {path}", parameterName);
        }
    }

    private static void ValidateDirectoryPath(string path, string parameterName) =>
        ValidatePath(path, parameterName);

    private static void ValidateOptionalOutputPath(string? path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        ValidatePath(path, parameterName);
    }

    private static void ValidatePath(string path, string parameterName)
    {
        RequireValue(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Path must be fully qualified.", parameterName);
        }
    }

    private static void RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }
}
