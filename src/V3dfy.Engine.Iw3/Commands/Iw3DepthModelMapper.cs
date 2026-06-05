using V3dfy.Core.Planning;

namespace V3dfy.Engine.Iw3.Commands;

public sealed record Iw3DepthModelMapping(
    string ModelRelativePath,
    string DepthModelName);

public static class Iw3DepthModelMapper
{
    public const string DepthAnythingMetricDepthIndoorRelativePath =
        "hub/checkpoints/depth_anything_metric_depth_indoor.pt";
    public const string ZoeDAnyNDepthModelName = "ZoeD_Any_N";

    public static bool TryMap(
        LocalModelPlanSelection? selectedModel,
        out Iw3DepthModelMapping? mapping)
    {
        mapping = null;
        var normalizedPath = NormalizeRelativePath(selectedModel?.RelativePath);
        if (!string.Equals(
            normalizedPath,
            DepthAnythingMetricDepthIndoorRelativePath,
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        mapping = new(
            ModelRelativePath: DepthAnythingMetricDepthIndoorRelativePath,
            DepthModelName: ZoeDAnyNDepthModelName);
        return true;
    }

    private static string NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathFullyQualified(relativePath) ||
            Path.IsPathRooted(relativePath))
        {
            return string.Empty;
        }

        var segments = relativePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            return string.Empty;
        }

        return string.Join('/', segments);
    }
}
