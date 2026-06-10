namespace V3dfy.SetupHelper;

public static partial class SetupHelperUiRunner
{
    private static partial int RunPlatformUi(
        PayloadInstallOptions options,
        string? logPath,
        CancellationToken cancellationToken)
    {
        Console.Error.WriteLine("Setup progress UI is only available in the Windows installer helper build.");
        return 2;
    }
}
