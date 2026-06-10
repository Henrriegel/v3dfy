using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace V3dfy.SetupHelper;

public static partial class SetupHelperUiRunner
{
    private static partial int RunPlatformUi(
        PayloadInstallOptions options,
        string? logPath,
        CancellationToken cancellationToken)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var form = new SetupProgressForm(options, logPath, cancellationToken);
        Application.Run(form);
        return form.ExitCode;
    }
}

internal sealed class SetupProgressForm : Form
{
    private readonly PayloadInstallOptions options;
    private readonly string? logPath;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly Label statusLabel;
    private readonly Label progressTextLabel;
    private readonly ProgressBar progressBar;
    private readonly ListBox logListBox;
    private readonly Button actionButton;
    private readonly Dictionary<string, int> loggedProgressBuckets = new(StringComparer.OrdinalIgnoreCase);
    private bool running;

    public SetupProgressForm(
        PayloadInstallOptions options,
        string? logPath,
        CancellationToken cancellationToken)
    {
        this.options = options;
        this.logPath = logPath;
        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Text = "v3dfy Setup";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        Size = new Size(820, 580);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headingLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            Text = "Installing v3dfy",
            Margin = new Padding(0, 0, 0, 12),
        };

        statusLabel = new Label
        {
            AutoSize = true,
            Text = "Preparing setup",
            Margin = new Padding(0, 0, 0, 8),
        };

        var progressPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 12),
        };
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = 22,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Minimum = 0,
            Maximum = 1000,
        };
        progressTextLabel = new Label
        {
            AutoSize = true,
            Text = "Working...",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 6, 0, 0),
        };
        progressPanel.Controls.Add(progressBar, 0, 0);
        progressPanel.Controls.Add(progressTextLabel, 0, 1);

        var logLabel = new Label
        {
            AutoSize = true,
            Text = "Details",
            Margin = new Padding(0, 0, 0, 4),
        };

        logListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Font = new Font(FontFamily.GenericMonospace, 9),
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0),
        };

        actionButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
        };
        actionButton.Click += OnActionButtonClick;
        buttonPanel.Controls.Add(actionButton);

        root.Controls.Add(headingLabel, 0, 0);
        root.Controls.Add(statusLabel, 0, 1);
        root.Controls.Add(progressPanel, 0, 2);
        root.Controls.Add(logLabel, 0, 3);
        root.Controls.Add(logListBox, 0, 4);
        root.Controls.Add(buttonPanel, 0, 5);
        Controls.Add(root);
    }

    public int ExitCode { get; private set; } = 1;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        running = true;
        AppendLogLine("Prepare setup");

        _ = Task.Run(RunInstallAsync);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (running)
        {
            e.Cancel = true;
            RequestCancellation();
            return;
        }

        base.OnFormClosing(e);
    }

    private async Task RunInstallAsync()
    {
        using var log = new UiSetupLog(logPath, this);
        var progress = new UiSetupProgress(this);

        try
        {
            await new PayloadInstaller().InstallAsync(
                options,
                log,
                cancellationTokenSource.Token,
                progress);

            ExitCode = 0;
            PostSuccess();
        }
        catch (OperationCanceledException)
        {
            log.Error("Installation was cancelled.");
            PostFailure("Installation was cancelled.");
        }
        catch (PayloadInstallException ex)
        {
            log.Error(ex.Message);
            PostFailure(ex.Message);
        }
        catch (Exception ex)
        {
            log.Error($"Unexpected setup helper failure: {ex.Message}");
            PostFailure($"Unexpected setup helper failure: {ex.Message}");
        }
    }

    private void OnActionButtonClick(object? sender, EventArgs e)
    {
        if (running)
        {
            RequestCancellation();
            return;
        }

        Close();
    }

    private void RequestCancellation()
    {
        if (cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        AppendLogLine("Cancel requested");
        statusLabel.Text = "Canceling setup";
        actionButton.Enabled = false;
        cancellationTokenSource.Cancel();
    }

    public void ReportProgress(SetupProgressEvent progress)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() =>
        {
            statusLabel.Text = FormatStatus(progress);

            if (progress.Percent is { } percent)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.MarqueeAnimationSpeed = 0;
                progressBar.Value = Math.Clamp((int)Math.Round(percent * 10), progressBar.Minimum, progressBar.Maximum);
                progressTextLabel.Text = $"{FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({percent:0.0}%)";
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Marquee;
                progressBar.MarqueeAnimationSpeed = 30;
                progressTextLabel.Text = "Working...";
            }

            if (ShouldLogProgress(progress))
            {
                AppendLogLine(FormatLogLine(progress));
            }
        });
    }

    public void AppendLogLine(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLogLine(message));
            return;
        }

        logListBox.Items.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        logListBox.TopIndex = Math.Max(0, logListBox.Items.Count - 1);
    }

    private void PostSuccess()
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(async () =>
        {
            running = false;
            statusLabel.Text = "Installation complete";
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = progressBar.Maximum;
            progressTextLabel.Text = "Complete";
            actionButton.Enabled = false;
            AppendLogLine("Complete");
            await Task.Delay(1000);
            Close();
        });
    }

    private void PostFailure(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() =>
        {
            running = false;
            ExitCode = 1;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;
            progressTextLabel.Text = "Failed";
            statusLabel.Text = "Installation failed";
            actionButton.Text = "Close";
            actionButton.Enabled = true;
        });
    }

    private bool ShouldLogProgress(SetupProgressEvent progress)
    {
        if (progress.Percent is not { } percent)
        {
            return true;
        }

        var key = $"{progress.Phase}:{progress.CurrentFile}";
        if (progress.CurrentBytes == 0)
        {
            loggedProgressBuckets[key] = 0;
            return true;
        }

        if (progress.CurrentBytes == progress.TotalBytes)
        {
            return !loggedProgressBuckets.ContainsKey(key);
        }

        var bucket = (int)Math.Floor(percent / 10);
        if (loggedProgressBuckets.TryGetValue(key, out var existingBucket) && existingBucket >= bucket)
        {
            return false;
        }

        loggedProgressBuckets[key] = bucket;
        return true;
    }

    private string FormatStatus(SetupProgressEvent progress)
    {
        var partNumber = GetPayloadPartNumber(progress.CurrentFile);
        return progress.Phase switch
        {
            SetupProgressPhase.DownloadingPart when partNumber is not null =>
                $"Downloading payload part {partNumber} of 3",
            SetupProgressPhase.VerifyingPart when partNumber is not null =>
                $"Verifying payload part {partNumber} of 3",
            SetupProgressPhase.FindingPart when partNumber is not null =>
                $"Finding payload part {partNumber} of 3",
            SetupProgressPhase.RebuildingZip => "Rebuilding portable package",
            SetupProgressPhase.VerifyingZip => "Verifying portable package",
            SetupProgressPhase.ExtractingPayload => "Extracting files",
            SetupProgressPhase.InstallingPayload => "Installing v3dfy",
            SetupProgressPhase.CleaningUp => "Cleaning temporary files",
            SetupProgressPhase.Completed => "Installation complete",
            _ => progress.Message.TrimEnd('.'),
        };
    }

    private string FormatLogLine(SetupProgressEvent progress)
    {
        var fileName = ShortenFileName(progress.CurrentFile);
        return progress.Phase switch
        {
            SetupProgressPhase.DownloadingPart when progress.Percent is > 0 and < 100 =>
                $"Download progress: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.DownloadingPart => $"Download: {fileName}",
            SetupProgressPhase.FindingPart => $"Find: {fileName}",
            SetupProgressPhase.VerifyingPart when progress.Percent is > 0 and < 100 =>
                $"Verify progress: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.VerifyingPart => $"Verify: {fileName}",
            SetupProgressPhase.RebuildingZip when progress.Percent is > 0 and < 100 =>
                $"Rebuild progress: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.RebuildingZip => "Rebuild package",
            SetupProgressPhase.VerifyingZip when progress.Percent is > 0 and < 100 =>
                $"Verify package progress: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.VerifyingZip => "Verify package",
            SetupProgressPhase.ExtractingPayload when !string.IsNullOrWhiteSpace(progress.CurrentFile) =>
                $"Extract: {ShortenArchivePath(progress.CurrentFile)}",
            SetupProgressPhase.ExtractingPayload => "Extract files",
            SetupProgressPhase.InstallingPayload => "Install files",
            SetupProgressPhase.CleaningUp => "Clean temporary files",
            SetupProgressPhase.Completed => "Complete",
            _ => progress.Message.TrimEnd('.'),
        };
    }

    private static int? GetPayloadPartNumber(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var match = Regex.Match(fileName, @"\.part(?<number>\d{2})$", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["number"].Value, out var number)
            ? number
            : null;
    }

    private static string ShortenFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "payload";
        }

        return Path.GetFileName(fileName);
    }

    private static string ShortenArchivePath(string? archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            return "...";
        }

        const int maxLength = 96;
        var normalized = archivePath.Replace('/', Path.DirectorySeparatorChar);
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..45] + "..." + normalized[^45..];
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            cancellationTokenSource.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class UiSetupLog : ISetupLog, IDisposable
{
    private readonly SetupLog fileLog;
    private readonly SetupProgressForm form;

    public UiSetupLog(string? logPath, SetupProgressForm form)
    {
        fileLog = new SetupLog(logPath);
        this.form = form;
    }

    public void Info(string message) => fileLog.Info(message);

    public void Warning(string message)
    {
        fileLog.Warning(message);
        form.AppendLogLine("WARNING: " + message);
    }

    public void Error(string message)
    {
        fileLog.Error(message);
        form.AppendLogLine("ERROR: " + message);
    }

    public void Dispose() => fileLog.Dispose();
}

internal sealed class UiSetupProgress(SetupProgressForm form) : ISetupProgress
{
    public void Report(SetupProgressEvent progress) => form.ReportProgress(progress);
}
