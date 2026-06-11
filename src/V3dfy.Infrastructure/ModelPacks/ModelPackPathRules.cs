using System.IO.Compression;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

internal static class ModelPackPathRules
{
    public static bool TryNormalizeArchivePath(
        string? rawPath,
        out string relativePath,
        out string error) =>
        TryNormalizeRelativePath(
            rawPath,
            allowTrailingDirectorySeparator: false,
            out relativePath,
            out error);

    public static bool TryNormalizeArchiveDirectoryPath(
        string? rawPath,
        out string relativePath,
        out string error) =>
        TryNormalizeRelativePath(
            rawPath,
            allowTrailingDirectorySeparator: true,
            out relativePath,
            out error);

    public static bool IsZipDirectoryEntry(ZipArchiveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return string.IsNullOrEmpty(entry.Name) ||
            entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
            entry.FullName.EndsWith("\\", StringComparison.Ordinal);
    }

    public static bool TryNormalizeModelPackFilePath(
        string? rawPath,
        out string relativePath,
        out string error)
    {
        if (!TryNormalizeRelativePath(
                rawPath,
                allowTrailingDirectorySeparator: false,
                out relativePath,
                out error))
        {
            return false;
        }

        if (string.Equals(relativePath, ModelPackManifest.FileName, StringComparison.OrdinalIgnoreCase))
        {
            error = $"{ModelPackManifest.FileName} is reserved.";
            return false;
        }

        if (relativePath.StartsWith("engine/iw3/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relativePath, "engine/iw3", StringComparison.OrdinalIgnoreCase))
        {
            error = "Paths are relative to pretrained_models and must not include engine/iw3.";
            return false;
        }

        if (relativePath.StartsWith("pretrained_models/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relativePath, "pretrained_models", StringComparison.OrdinalIgnoreCase))
        {
            error = "Paths are relative to pretrained_models and must not include pretrained_models.";
            return false;
        }

        return true;
    }

    public static string ResolveDestinationPath(string targetRoot, string relativePath)
    {
        var destinationPath = Path.GetFullPath(Path.Combine(
            [targetRoot, .. relativePath.Split('/')]));
        if (!IsSameOrUnderDirectory(targetRoot, destinationPath))
        {
            throw new InvalidOperationException($"Resolved destination escapes target root: {relativePath}");
        }

        return destinationPath;
    }

    public static bool IsProtectedRuntimeDependency(string relativePath) =>
        string.Equals(
            relativePath,
            "hub/checkpoints/" + Iw3EngineBundleContract.Iw3DefaultStereoRuntimeDependencyFileName,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsUnderProgramFiles(string path)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(NormalizeFullPath)
            .Any(root => IsSameOrUnderDirectory(root, path));
    }

    public static bool IsSameOrUnderDirectory(string rootDirectory, string candidatePath)
    {
        var root = NormalizeFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = NormalizeFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeFullPath(string path) =>
        Path.GetFullPath(path);

    private static bool TryNormalizeRelativePath(
        string? rawPath,
        bool allowTrailingDirectorySeparator,
        out string relativePath,
        out string error)
    {
        relativePath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            error = "Path is required.";
            return false;
        }

        var candidate = rawPath.Trim().Replace('\\', '/');
        if (candidate.StartsWith("//", StringComparison.Ordinal) ||
            candidate.StartsWith("/", StringComparison.Ordinal) ||
            Path.IsPathRooted(candidate))
        {
            error = "Absolute, rooted, and UNC paths are not allowed.";
            return false;
        }

        if (allowTrailingDirectorySeparator)
        {
            candidate = candidate.TrimEnd('/');
        }
        else if (candidate.EndsWith("/", StringComparison.Ordinal))
        {
            error = "File path must not end with a directory separator.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            error = "Path is required.";
            return false;
        }

        var segments = candidate.Split('/');
        if (segments.Length == 0 ||
            segments.Any(string.IsNullOrWhiteSpace))
        {
            error = "Path segments must not be empty.";
            return false;
        }

        if (segments.Any(segment => segment is "." or ".."))
        {
            error = "Path traversal segments are not allowed.";
            return false;
        }

        if (segments.Any(segment => segment.Contains(':')))
        {
            error = "Rooted drive paths are not allowed.";
            return false;
        }

        relativePath = string.Join('/', segments);
        return true;
    }
}
