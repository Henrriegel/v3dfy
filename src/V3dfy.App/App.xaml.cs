using System.Windows;
using System.Windows.Threading;
using V3dfy.App.Services;

namespace V3dfy.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        AppErrorLogService.LogUnhandledException(
            AppErrorLogService.CurrentOperation ?? "Dispatcher unhandled exception",
            e.Exception);
    }

    private static void OnUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        AppErrorLogService.LogUnhandledException(
            AppErrorLogService.CurrentOperation ?? "AppDomain unhandled exception",
            e.ExceptionObject);
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        AppErrorLogService.LogUnhandledException(
            AppErrorLogService.CurrentOperation ?? "Unobserved task exception",
            e.Exception);
        e.SetObserved();
    }
}
