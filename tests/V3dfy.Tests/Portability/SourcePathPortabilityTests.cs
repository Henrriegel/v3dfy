namespace V3dfy.Tests.Portability;

public sealed class SourcePathPortabilityTests
{
    private static readonly string Backslash = new([Path.DirectorySeparatorChar]);

    [Fact]
    public void ProductionSource_DoesNotContainForbiddenHardcodedPaths()
    {
        var forbiddenFragments = new[]
        {
            DrivePath("dev", "v3dfy"),
            DrivePath("v3dfy"),
            DrivePathWithTrailing("Users"),
            string.Concat("lega", "rcia"),
            BackslashPath("Videos", "v3dfy-tests"),
            BackslashPath("artifacts", "publish"),
            string.Concat("candidate", "-", "iw3"),
            string.Concat("v3dfy", "-", "iw3", "-", "intake"),
        };
        var productionDirectories = new[]
        {
            Path.Combine("src", "V3dfy.App"),
            Path.Combine("src", "V3dfy.Core"),
            Path.Combine("src", "V3dfy.Engine.Iw3"),
            Path.Combine("src", "V3dfy.Infrastructure"),
        };

        AssertNoForbiddenFragments(
            EnumerateTextFiles(productionDirectories),
            forbiddenFragments);
    }

    [Fact]
    public void TestSource_DoesNotContainRealUserOrDeveloperMachinePaths()
    {
        var forbiddenFragments = new[]
        {
            DrivePath("dev", "v3dfy"),
            DrivePath("v3dfy"),
            DrivePathWithTrailing("Users"),
            string.Concat("lega", "rcia"),
            BackslashPath("Videos", "v3dfy-tests"),
        };

        AssertNoForbiddenFragments(
            EnumerateTextFiles([Path.Combine("tests", "V3dfy.Tests")]),
            forbiddenFragments);
    }

    private static void AssertNoForbiddenFragments(
        IEnumerable<string> files,
        IReadOnlyList<string> forbiddenFragments)
    {
        var violations = files
            .SelectMany(file =>
            {
                var text = File.ReadAllText(file);
                return forbiddenFragments
                    .Where(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    .Select(fragment => $"{Path.GetRelativePath(RepositoryRoot(), file)}: {fragment}");
            })
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> EnumerateTextFiles(IEnumerable<string> relativeDirectories)
    {
        var root = RepositoryRoot();
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".xaml",
            ".csproj",
        };

        foreach (var relativeDirectory in relativeDirectories)
        {
            var directory = Path.Combine(root, relativeDirectory);
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(root, file);
                if (relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase)) ||
                    !extensions.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "v3dfy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string DrivePath(params string[] segments) =>
        "C:" + Backslash + BackslashPath(segments);

    private static string DrivePathWithTrailing(params string[] segments) =>
        DrivePath(segments) + Backslash;

    private static string BackslashPath(params string[] segments) =>
        string.Join(Backslash, segments);
}
