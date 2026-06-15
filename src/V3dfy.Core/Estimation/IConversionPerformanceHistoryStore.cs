namespace V3dfy.Core.Estimation;

public interface IConversionPerformanceHistoryStore
{
    string HistoryPath { get; }

    ConversionPerformanceHistoryLoadResult Load();

    ConversionPerformanceHistorySaveResult Save(
        IReadOnlyList<ConversionPerformanceRecord> records);
}
