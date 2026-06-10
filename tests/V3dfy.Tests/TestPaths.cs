using V3dfy.Core.Models;
using V3dfy.Infrastructure.Paths;

internal static class TestPaths
{
    public static string TempRoot(params string[] segments) =>
        Combine(Path.GetTempPath(), ["synthetic-v3dfy", .. segments]);

    public static string RuntimeRoot(params string[] segments) =>
        TempRoot(["app", .. segments]);

    public static string AlternateRuntimeRoot(params string[] segments) =>
        TempRoot(["alternate-app", .. segments]);

    public static string AppDataRoot(params string[] segments) =>
        TempRoot(["local-app-data", .. segments]);

    public static string PreviewCacheRoot(params string[] segments) =>
        AppDataRoot(["v3dfy", "previews", .. segments]);

    public static string LogsRoot(params string[] segments) =>
        AppDataRoot(["v3dfy", "logs", .. segments]);

    public static string SourceRoot(params string[] segments) =>
        TempRoot(["sources", .. segments]);

    public static string OutputRoot(params string[] segments) =>
        TempRoot(["outputs", .. segments]);

    public static string OtherRoot(params string[] segments) =>
        TempRoot(["other", .. segments]);

    public static InternalToolPaths InternalToolPaths(params string[] runtimeSegments) =>
        new InternalToolPathResolver(RuntimeRoot(runtimeSegments)).Resolve();

    public static InternalToolPaths AlternateInternalToolPaths(params string[] runtimeSegments) =>
        new InternalToolPathResolver(AlternateRuntimeRoot(runtimeSegments)).Resolve();

    public static string ModelPath(params string[] segments) =>
        Combine(InternalToolPaths().ModelsDirectory, segments);

    private static string Combine(string root, params string[] segments) =>
        Path.Combine([root, .. segments]);
}
