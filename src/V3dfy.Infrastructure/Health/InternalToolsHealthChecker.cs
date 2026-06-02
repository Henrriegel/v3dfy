using System.Text.Json;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.Health;

public sealed class InternalToolsHealthChecker
{
    public EngineHealthStatus Check(InternalToolPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return new EngineHealthStatus(
            Ffmpeg: GetFileStatus(paths.FfmpegExecutable),
            Ffprobe: GetFileStatus(paths.FfprobeExecutable),
            Python: GetFileStatus(paths.PythonExecutable),
            Iw3EngineDirectory: GetIw3EngineStatus(paths.Iw3EngineDirectory),
            ModelsDirectory: GetModelsStatus(paths.ModelsDirectory));
    }

    private static ToolHealthStatus GetFileStatus(string path) =>
        File.Exists(path) ? ToolHealthStatus.Found : ToolHealthStatus.Missing;

    private static ToolHealthStatus GetIw3EngineStatus(string path)
    {
        if (!Directory.Exists(path))
        {
            return ToolHealthStatus.Missing;
        }

        var manifestPath = Path.Combine(path, "ENGINE_MANIFEST.json");
        if (HasNonPlaceholderManifest(manifestPath))
        {
            return ToolHealthStatus.Found;
        }

        var hasEngineFile = Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Any(file => IsPlausibleEngineFile(path, file));

        return hasEngineFile ? ToolHealthStatus.Found : ToolHealthStatus.Missing;
    }

    private static ToolHealthStatus GetModelsStatus(string path)
    {
        if (!Directory.Exists(path))
        {
            return ToolHealthStatus.Missing;
        }

        var hasModelFile = Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Any(file => ModelExtensions.Contains(
                Path.GetExtension(file),
                StringComparer.OrdinalIgnoreCase));

        return hasModelFile ? ToolHealthStatus.Found : ToolHealthStatus.Missing;
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
