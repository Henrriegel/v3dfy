using V3dfy.Core.Estimation;
using V3dfy.Core.Models;

namespace V3dfy.Tests.Estimation;

public sealed class ConversionPerformanceHistoryTests
{
    [Fact]
    public void CreateSuccessfulRecord_StoresTechnicalMetadataWithoutFilePaths()
    {
        var input = CreateInput();

        var record = ConversionPerformanceHistory.CreateSuccessfulRecord(
            ConversionPerformanceOperationType.FullConversion,
            input,
            TimeSpan.FromMinutes(10),
            "0.1.test");

        Assert.NotNull(record);
        Assert.Equal(ConversionPerformanceOperationType.FullConversion, record.OperationType);
        Assert.Equal("depth-anything-v2-small", record.ModelKey);
        Assert.DoesNotContain('\\', record.ModelDisplayName);
        Assert.DoesNotContain('/', record.ModelDisplayName);
        Assert.True(record.EffectiveFramesPerSecond > 0);
        Assert.True(record.EffectiveMegapixelFramesPerSecond > 0);
    }

    [Fact]
    public void CreateSuccessfulRecord_DoesNotWriteCanceledOrFailedRecordWhenCallerDoesNotCallIt()
    {
        var records = Array.Empty<ConversionPerformanceRecord>();

        Assert.Empty(records);
    }

    [Fact]
    public void AddBounded_KeepsMostRecentRecords()
    {
        var records = Enumerable.Range(0, 120)
            .Select(index => CreateRecord(index))
            .ToArray();
        var newRecord = CreateRecord(121);

        var bounded = ConversionPerformanceHistory.AddBounded(records, newRecord, maximumRecords: 50);

        Assert.Equal(50, bounded.Count);
        Assert.Contains(bounded, record => record.TimestampUtc == newRecord.TimestampUtc);
        Assert.DoesNotContain(bounded, record => record.TimestampUtc == records[0].TimestampUtc);
    }

    [Fact]
    public void Sanitize_DropsPathLikeRecordValues()
    {
        var good = CreateRecord(1);
        var bad = good with
        {
            ModelKey = "synthetic/path/movie.mp4",
        };

        var sanitized = ConversionPerformanceHistory.Sanitize([good, bad]);

        Assert.Single(sanitized);
        Assert.Same(good, sanitized[0]);
    }

    private static ConversionEstimateInput CreateInput() => new(
        Duration: TimeSpan.FromMinutes(20),
        FrameRate: 24,
        SourceWidth: 1920,
        SourceHeight: 1080,
        ModelKey: "depth-anything-v2-small",
        ModelDisplayName: "Depth Anything V2 Small",
        OutputPresetId: "recommended-3d-tv",
        OutputPresetName: "Recommended 3D TV",
        OutputContainer: OutputContainer.MP4,
        QualityPreset: AiQualityPreset.Balanced,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
        DeviceBucket: "local engine ready");

    private static ConversionPerformanceRecord CreateRecord(int index) => new(
        SchemaVersion: ConversionPerformanceHistory.CurrentSchemaVersion,
        OperationType: index % 2 == 0
            ? ConversionPerformanceOperationType.FullConversion
            : ConversionPerformanceOperationType.Preview,
        ModelKey: "depth-anything-v2-small",
        ModelDisplayName: "Depth Anything V2 Small",
        OutputPresetId: "recommended-3d-tv",
        Width: 1920,
        Height: 1080,
        FrameCount: 2400,
        DurationSeconds: 100,
        ElapsedSeconds: 50,
        EffectiveFramesPerSecond: 48,
        EffectiveMegapixelFramesPerSecond: 99.5,
        DeviceBucket: "local engine ready",
        TimestampUtc: DateTimeOffset.UtcNow.AddMinutes(index),
        AppVersion: "test");
}
