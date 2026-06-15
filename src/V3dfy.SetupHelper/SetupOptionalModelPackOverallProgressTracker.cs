namespace V3dfy.SetupHelper;

public sealed record SetupOptionalModelPackProgressItem(
    string AssetFileName,
    long ZipSizeBytes);

public sealed class SetupOptionalModelPackOverallProgressTracker : ISetupProgress
{
    private const int OverallUnits = 10000;
    private const int PayloadCompletedUnitsWhenModelPacksSelected = 8500;
    private const int OptionalCompletedUnitsBeforeFinalFinish = 9900;

    private readonly ISetupProgress inner;
    private readonly Dictionary<string, ModelPackProgressInfo> modelPackProgress;
    private readonly long totalModelPackBytes;
    private int lastOverallCompletedUnits;

    public SetupOptionalModelPackOverallProgressTracker(
        ISetupProgress inner,
        IReadOnlyList<SetupOptionalModelPackProgressItem> selectedModelPacks)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(selectedModelPacks);

        this.inner = inner;
        modelPackProgress = BuildModelPackProgress(selectedModelPacks, out totalModelPackBytes);
    }

    public void Report(SetupProgressEvent progress)
    {
        if (modelPackProgress.Count == 0)
        {
            inner.Report(progress);
            return;
        }

        if (CalculateOverallProgress(progress) is not { } overall)
        {
            inner.Report(progress);
            return;
        }

        var completedUnits = Math.Clamp(
            Math.Max(overall.CompletedUnits, lastOverallCompletedUnits),
            0,
            OverallUnits);
        lastOverallCompletedUnits = completedUnits;

        inner.Report(progress with
        {
            OverallCompletedUnits = completedUnits,
            OverallTotalUnits = OverallUnits,
            OverallMessage = overall.Message,
        });
    }

    private OverallProgressSnapshot? CalculateOverallProgress(SetupProgressEvent progress)
    {
        if (progress.Phase == SetupProgressPhase.Completed)
        {
            return progress.OverallPercent is not null
                ? new OverallProgressSnapshot(
                    PayloadCompletedUnitsWhenModelPacksSelected,
                    "App payload installed")
                : new OverallProgressSnapshot(OverallUnits, "Installation complete");
        }

        if (TryCalculateModelPackProgress(progress, out var modelPackProgressSnapshot))
        {
            return modelPackProgressSnapshot;
        }

        if (progress.OverallPercent is { } payloadOverallPercent)
        {
            var payloadUnits = (int)Math.Round(
                PayloadCompletedUnitsWhenModelPacksSelected * payloadOverallPercent / 100.0);
            return new OverallProgressSnapshot(
                payloadUnits,
                FormatPayloadOverallMessage(progress.OverallMessage));
        }

        return null;
    }

    private bool TryCalculateModelPackProgress(
        SetupProgressEvent progress,
        out OverallProgressSnapshot snapshot)
    {
        snapshot = default;

        if (!TryGetOptionalPhaseRange(progress.Phase, out var range))
        {
            return false;
        }

        var message = progress.Phase switch
        {
            SetupProgressPhase.DownloadingModelPack => "Downloading optional model packs",
            SetupProgressPhase.VerifyingModelPack => "Verifying optional model packs",
            SetupProgressPhase.ValidatingModelPack => "Validating optional model packs",
            SetupProgressPhase.InstallingModelPack => "Installing optional model packs",
            _ => "Optional model packs",
        };

        if (!TryGetModelPackInfo(progress.CurrentFile, out var packInfo) || totalModelPackBytes <= 0)
        {
            snapshot = new OverallProgressSnapshot(range.StartUnits, message);
            return true;
        }

        var phaseRatio = progress.Phase switch
        {
            SetupProgressPhase.DownloadingModelPack or SetupProgressPhase.VerifyingModelPack =>
                CalculateByteRatio(progress, packInfo),
            SetupProgressPhase.ValidatingModelPack =>
                CalculatePackRatio(packInfo, 0.5),
            SetupProgressPhase.InstallingModelPack =>
                CalculatePercentRatio(progress, packInfo),
            _ => CalculatePackRatio(packInfo, 0),
        };

        snapshot = new OverallProgressSnapshot(range.Interpolate(phaseRatio), message);
        return true;
    }

    private double CalculateByteRatio(SetupProgressEvent progress, ModelPackProgressInfo packInfo)
    {
        var currentBytes = Math.Clamp(
            progress.CurrentBytes ?? 0,
            0,
            packInfo.SizeBytes);
        return (packInfo.BytesBefore + currentBytes) / (double)totalModelPackBytes;
    }

    private double CalculatePercentRatio(SetupProgressEvent progress, ModelPackProgressInfo packInfo)
    {
        if (progress.Percent is not { } percent)
        {
            return CalculatePackRatio(packInfo, 0);
        }

        return CalculatePackRatio(packInfo, percent / 100.0);
    }

    private double CalculatePackRatio(ModelPackProgressInfo packInfo, double packRatio)
    {
        var clampedPackRatio = Math.Clamp(packRatio, 0, 1);
        return (packInfo.BytesBefore + packInfo.SizeBytes * clampedPackRatio) /
            (double)totalModelPackBytes;
    }

    private bool TryGetModelPackInfo(string? fileName, out ModelPackProgressInfo progressInfo)
    {
        progressInfo = default;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return modelPackProgress.TryGetValue(Path.GetFileName(fileName), out progressInfo);
    }

    private static string FormatPayloadOverallMessage(string? message) =>
        message switch
        {
            "Installation complete" => "App payload installed",
            "Finalizing installation" => "Finalizing app payload",
            null or "" => "Installing app payload",
            _ => message,
        };

    private static bool TryGetOptionalPhaseRange(
        SetupProgressPhase phase,
        out OverallPhaseRange range)
    {
        range = phase switch
        {
            SetupProgressPhase.DownloadingModelPack => new OverallPhaseRange(8500, 9000),
            SetupProgressPhase.VerifyingModelPack => new OverallPhaseRange(9000, 9400),
            SetupProgressPhase.ValidatingModelPack => new OverallPhaseRange(9400, 9600),
            SetupProgressPhase.InstallingModelPack => new OverallPhaseRange(
                9600,
                OptionalCompletedUnitsBeforeFinalFinish),
            _ => default,
        };

        return phase is
            SetupProgressPhase.DownloadingModelPack or
            SetupProgressPhase.VerifyingModelPack or
            SetupProgressPhase.ValidatingModelPack or
            SetupProgressPhase.InstallingModelPack;
    }

    private static Dictionary<string, ModelPackProgressInfo> BuildModelPackProgress(
        IReadOnlyList<SetupOptionalModelPackProgressItem> selectedModelPacks,
        out long totalModelPackBytes)
    {
        var result = new Dictionary<string, ModelPackProgressInfo>(StringComparer.OrdinalIgnoreCase);
        long bytesBefore = 0;
        foreach (var pack in selectedModelPacks)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pack.AssetFileName);
            if (pack.ZipSizeBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(selectedModelPacks),
                    "Optional model-pack ZIP size must be positive.");
            }

            var fileName = Path.GetFileName(pack.AssetFileName);
            if (result.ContainsKey(fileName))
            {
                throw new ArgumentException(
                    $"Duplicate optional model-pack asset file name: {fileName}",
                    nameof(selectedModelPacks));
            }

            result[fileName] = new ModelPackProgressInfo(bytesBefore, pack.ZipSizeBytes);
            bytesBefore += pack.ZipSizeBytes;
        }

        totalModelPackBytes = bytesBefore;
        return result;
    }

    private readonly record struct OverallProgressSnapshot(
        int CompletedUnits,
        string Message);

    private readonly record struct OverallPhaseRange(int StartUnits, int EndUnits)
    {
        public int Interpolate(double ratio)
        {
            var clampedRatio = Math.Clamp(ratio, 0, 1);
            return StartUnits + (int)Math.Round((EndUnits - StartUnits) * clampedRatio);
        }
    }

    private readonly record struct ModelPackProgressInfo(
        long BytesBefore,
        long SizeBytes);
}
