using System.Text.Json;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.Health;

public sealed class InternalToolsHealthChecker
{
    public EngineHealthStatus Check(InternalToolPaths paths) => CheckDetailed(paths).Summary;

    public EngineDependencyHealth CheckDetailed(InternalToolPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return new EngineDependencyHealth(
            Ffmpeg: GetBundledFileHealth(paths.FfmpegExecutable),
            Ffprobe: GetBundledFileHealth(paths.FfprobeExecutable),
            Python: GetBundledFileHealth(paths.PythonExecutable),
            Iw3EngineDirectory: GetIw3EngineHealth(paths.Iw3EngineDirectory),
            ModelsDirectory: GetModelsHealth(paths.ModelsDirectory));
    }

    private static ToolDependencyHealth GetBundledFileHealth(string path) =>
        File.Exists(path)
            ? new(ToolHealthStatus.Found, ToolHealthDetailKind.BundledFileFound, path)
            : new(ToolHealthStatus.Missing, ToolHealthDetailKind.BundledFileMissing, path);

    private static ToolDependencyHealth GetIw3EngineHealth(string path)
    {
        if (!Directory.Exists(path))
        {
            return new(
                ToolHealthStatus.Missing,
                ToolHealthDetailKind.EngineDirectoryMissing,
                path);
        }

        var manifestPath = Path.Combine(path, "ENGINE_MANIFEST.json");
        var hasNonPlaceholderManifest = HasNonPlaceholderManifest(manifestPath);
        var hasRequiredEntryFile = Iw3EngineBundleContract.EngineEntryRelativePaths
            .Any(relativePath => File.Exists(Path.Combine(
                [path, .. SplitRelativePath(relativePath)])));

        if (hasNonPlaceholderManifest && hasRequiredEntryFile)
        {
            return new(ToolHealthStatus.Found, ToolHealthDetailKind.EngineBundleFound, path);
        }

        if (hasNonPlaceholderManifest)
        {
            return new(ToolHealthStatus.Missing, ToolHealthDetailKind.EngineEntryFilesMissing, path);
        }

        if (hasRequiredEntryFile)
        {
            return new(ToolHealthStatus.Missing, ToolHealthDetailKind.EngineManifestMissing, path);
        }

        var hasNonPlaceholderContent = Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Any(file => IsNonPlaceholderEngineContent(path, file));

        if (!hasNonPlaceholderContent)
        {
            return new(ToolHealthStatus.Missing, ToolHealthDetailKind.EnginePlaceholderOnly, path);
        }

        return new(ToolHealthStatus.Missing, ToolHealthDetailKind.EngineManifestMissing, path);
    }

    private static ToolDependencyHealth GetModelsHealth(string path)
    {
        if (!Directory.Exists(path))
        {
            return new(
                ToolHealthStatus.Missing,
                ToolHealthDetailKind.ModelsDirectoryMissing,
                path);
        }

        var hasModelFile = Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Any(file => Iw3EngineBundleContract.SupportedModelExtensions.Contains(
                Path.GetExtension(file),
                StringComparer.OrdinalIgnoreCase));

        return hasModelFile
            ? new(ToolHealthStatus.Found, ToolHealthDetailKind.ModelFilesFound, path)
            : new(ToolHealthStatus.Missing, ToolHealthDetailKind.ModelFilesMissing, path);
    }

    private static bool IsNonPlaceholderEngineContent(string engineDirectory, string file)
    {
        var relativePath = Path.GetRelativePath(engineDirectory, file);
        var firstSegment = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries)[0];

        if (string.Equals(firstSegment, "models", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(firstSegment, "python", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(file);
        if (Iw3EngineBundleContract.PlaceholderOrContractFileNames.Contains(
            fileName,
            StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string[] SplitRelativePath(string relativePath) =>
        relativePath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);

    private static bool HasNonPlaceholderManifest(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(path));
            return manifest.RootElement.TryGetProperty("version", out var version) &&
                !string.IsNullOrWhiteSpace(version.GetString()) &&
                !string.Equals(
                    version.GetString(),
                    "placeholder",
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

}
