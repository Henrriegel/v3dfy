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
        if (HasNonPlaceholderManifest(manifestPath))
        {
            return new(ToolHealthStatus.Found, ToolHealthDetailKind.EngineBundleFound, path);
        }

        var hasEngineFile = Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Any(file => IsPlausibleEngineFile(path, file));

        return hasEngineFile
            ? new(ToolHealthStatus.Found, ToolHealthDetailKind.EngineBundleFound, path)
            : new(ToolHealthStatus.Missing, ToolHealthDetailKind.EnginePlaceholderOnly, path);
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
            .Any(file => ModelExtensions.Contains(
                Path.GetExtension(file),
                StringComparer.OrdinalIgnoreCase));

        return hasModelFile
            ? new(ToolHealthStatus.Found, ToolHealthDetailKind.ModelFilesFound, path)
            : new(ToolHealthStatus.Missing, ToolHealthDetailKind.ModelFilesMissing, path);
    }

    private static bool IsPlausibleEngineFile(string engineDirectory, string file)
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
        if (string.Equals(fileName, "README.md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "ENGINE_MANIFEST.json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(Path.GetExtension(file), ".py", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(file), ".md", StringComparison.OrdinalIgnoreCase);
    }

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

    private static readonly string[] ModelExtensions =
    [
        ".pth",
        ".pt",
        ".onnx",
        ".safetensors",
        ".ckpt",
        ".bin",
    ];
}
