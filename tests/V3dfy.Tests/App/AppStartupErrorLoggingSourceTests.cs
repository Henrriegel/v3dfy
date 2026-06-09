namespace V3dfy.Tests.App;

public sealed class AppStartupErrorLoggingSourceTests
{
    [Fact]
    public void AppStartup_RegistersGlobalUnhandledExceptionHooks()
    {
        var source = ReadAppSource();

        Assert.Contains("DispatcherUnhandledException += OnDispatcherUnhandledException", source);
        Assert.Contains("AppDomain.CurrentDomain.UnhandledException += OnUnhandledException", source);
        Assert.Contains("TaskScheduler.UnobservedTaskException += OnUnobservedTaskException", source);
        Assert.Contains("AppErrorLogService.LogUnhandledException", source);
        Assert.Contains("e.SetObserved();", source);
    }

    [Fact]
    public void ErrorLogService_WritesUnderLocalAppDataV3dfyLogs()
    {
        var source = ReadErrorLogServiceSource();

        Assert.Contains("Environment.SpecialFolder.LocalApplicationData", source);
        Assert.Contains("\"v3dfy\"", source);
        Assert.Contains("\"logs\"", source);
        Assert.Contains("\"app-error.log\"", source);
        Assert.Contains("\"crash.log\"", source);
        Assert.Contains("Assembly.GetEntryAssembly()?.GetName().Version", source);
        Assert.Contains("CurrentOperation", source);
        Assert.DoesNotContain("AppContext.BaseDirectory", source);
        Assert.DoesNotContain("Environment.CurrentDirectory", source);
        Assert.DoesNotContain("artifacts", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadAppSource() =>
        ReadSource("src", "V3dfy.App", "App.xaml.cs");

    private static string ReadErrorLogServiceSource() =>
        ReadSource("src", "V3dfy.App", "Services", "AppErrorLogService.cs");

    private static string ReadSource(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var combinedParts = new string[pathParts.Length + 1];
            combinedParts[0] = directory.FullName;
            Array.Copy(pathParts, 0, combinedParts, 1, pathParts.Length);
            var candidate = Path.Combine(combinedParts);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(pathParts)}.");
    }
}
