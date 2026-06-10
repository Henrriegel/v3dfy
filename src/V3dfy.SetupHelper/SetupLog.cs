namespace V3dfy.SetupHelper;

public interface ISetupLog
{
    void Info(string message);

    void Warning(string message);

    void Error(string message);
}

public sealed class SetupLog : ISetupLog, IDisposable
{
    private readonly StreamWriter? writer;

    public SetupLog(string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(logPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        writer = new StreamWriter(File.Open(logPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
    }

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message) => Write("ERROR", message);

    public void Dispose() => writer?.Dispose();

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}";
        Console.WriteLine(line);
        writer?.WriteLine(line);
    }
}
