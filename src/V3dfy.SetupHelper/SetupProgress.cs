namespace V3dfy.SetupHelper;

public enum SetupProgressPhase
{
    Preparing,
    FindingPart,
    DownloadingPart,
    VerifyingPart,
    RebuildingZip,
    VerifyingZip,
    ExtractingPayload,
    InstallingPayload,
    CleaningUp,
    Completed,
}

public sealed record SetupProgressEvent(
    SetupProgressPhase Phase,
    string Message,
    string? CurrentFile = null,
    long? CurrentBytes = null,
    long? TotalBytes = null)
{
    public double? Percent =>
        CurrentBytes is { } current &&
        TotalBytes is { } total &&
        total > 0
            ? Math.Clamp(current * 100.0 / total, 0, 100)
            : null;
}

public interface ISetupProgress
{
    void Report(SetupProgressEvent progress);
}

public sealed class NullSetupProgress : ISetupProgress
{
    public static NullSetupProgress Instance { get; } = new();

    private NullSetupProgress()
    {
    }

    public void Report(SetupProgressEvent progress)
    {
    }
}
