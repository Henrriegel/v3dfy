using System.Text.Json;
using V3dfy.Core.Estimation;

namespace V3dfy.Infrastructure.Estimation;

public sealed class FileSystemConversionPerformanceHistoryStore :
    IConversionPerformanceHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public FileSystemConversionPerformanceHistoryStore(string? historyPath = null)
    {
        HistoryPath = string.IsNullOrWhiteSpace(historyPath)
            ? GetDefaultHistoryPath()
            : historyPath;
    }

    public string HistoryPath { get; }

    public static string GetDefaultHistoryPath()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "Local");
        }

        return Path.Combine(
            localAppData,
            "v3dfy",
            "conversion-performance-history.json");
    }

    public ConversionPerformanceHistoryLoadResult Load()
    {
        try
        {
            if (!File.Exists(HistoryPath))
            {
                return new([], Warning: null);
            }

            var json = File.ReadAllText(HistoryPath);
            var records = JsonSerializer.Deserialize<List<ConversionPerformanceRecord>>(
                json,
                JsonOptions);
            return new(
                ConversionPerformanceHistory.Sanitize(records),
                Warning: null);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException)
        {
            return new(
                [],
                $"Performance history could not be loaded and will be ignored: {exception.Message}");
        }
    }

    public ConversionPerformanceHistorySaveResult Save(
        IReadOnlyList<ConversionPerformanceRecord> records)
    {
        try
        {
            var directory = Path.GetDirectoryName(HistoryPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sanitized = ConversionPerformanceHistory.Sanitize(records);
            var json = JsonSerializer.Serialize(sanitized, JsonOptions);
            File.WriteAllText(HistoryPath, json);
            return new(Success: true, Warning: null);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException)
        {
            return new(
                Success: false,
                Warning: $"Performance history could not be saved: {exception.Message}");
        }
    }
}
