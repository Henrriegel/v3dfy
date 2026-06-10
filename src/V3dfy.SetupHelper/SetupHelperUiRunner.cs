namespace V3dfy.SetupHelper;

public static partial class SetupHelperUiRunner
{
    public static int Run(
        PayloadInstallOptions options,
        string? logPath,
        CancellationToken cancellationToken) =>
        RunPlatformUi(options, logPath, cancellationToken);

    private static partial int RunPlatformUi(
        PayloadInstallOptions options,
        string? logPath,
        CancellationToken cancellationToken);
}
