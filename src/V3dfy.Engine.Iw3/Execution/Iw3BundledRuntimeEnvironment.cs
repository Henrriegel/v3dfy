using V3dfy.Core.Models;

namespace V3dfy.Engine.Iw3.Execution;

internal static class Iw3BundledRuntimeEnvironment
{
    public static IReadOnlyDictionary<string, string?> Create(InternalToolPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NUNIF_HOME"] = paths.NunifRootDirectory,
            ["PYTHONNOUSERSITE"] = "1",
            ["TORCH_HOME"] = paths.ModelsDirectory,
        };
    }
}
