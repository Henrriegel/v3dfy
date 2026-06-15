using V3dfy.Core.Estimation;
using V3dfy.Infrastructure.Estimation;

namespace V3dfy.Tests.Infrastructure;

public sealed class ConversionPerformanceHistoryStoreTests : IDisposable
{
    private readonly string root = TestPaths.TempRoot(
        "conversion-performance-history",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_WritesSuccessfulRecord()
    {
        var path = Path.Combine(root, "history.json");
        var store = new FileSystemConversionPerformanceHistoryStore(path);
        var record = CreateRecord();

        var save = store.Save([record]);
        var load = store.Load();

        Assert.True(save.Success);
        Assert.Null(save.Warning);
        Assert.Single(load.Records);
        Assert.Equal(record.ModelKey, load.Records[0].ModelKey);
    }

    [Fact]
    public void Load_CorruptJsonIgnoresHistorySafely()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "history.json");
        File.WriteAllText(path, "{not json");
        var store = new FileSystemConversionPerformanceHistoryStore(path);

        var load = store.Load();

        Assert.Empty(load.Records);
        Assert.NotNull(load.Warning);
    }

    [Fact]
    public void Save_BoundsHistoryLength()
    {
        var path = Path.Combine(root, "history.json");
        var store = new FileSystemConversionPerformanceHistoryStore(path);
        var records = Enumerable.Range(0, 120)
            .Select(index => CreateRecord(index))
            .ToArray();

        store.Save(records);
        var load = store.Load();

        Assert.Equal(ConversionPerformanceHistory.DefaultMaximumRecords, load.Records.Count);
    }

    [Fact]
    public void DefaultPath_UsesLocalAppDataV3dfy()
    {
        var path = FileSystemConversionPerformanceHistoryStore.GetDefaultHistoryPath();

        Assert.Contains("v3dfy", path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("conversion-performance-history.json", path);
    }

    private static ConversionPerformanceRecord CreateRecord(int index = 0) => new(
        SchemaVersion: ConversionPerformanceHistory.CurrentSchemaVersion,
        OperationType: ConversionPerformanceOperationType.FullConversion,
        ModelKey: "depth-anything-v2-small",
        ModelDisplayName: "Depth Anything V2 Small",
        OutputPresetId: "recommended-3d-tv",
        Width: 1920,
        Height: 1080,
        FrameCount: 2400,
        DurationSeconds: 100,
        ElapsedSeconds: 80,
        EffectiveFramesPerSecond: 30,
        EffectiveMegapixelFramesPerSecond: 62.2,
        DeviceBucket: "local engine ready",
        TimestampUtc: DateTimeOffset.UtcNow.AddMinutes(index),
        AppVersion: "test");
}
