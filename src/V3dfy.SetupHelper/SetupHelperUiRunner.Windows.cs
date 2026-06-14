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
    private readonly TableLayoutPanel root;
    private readonly Label headingLabel;
    private readonly TableLayoutPanel progressPanel;
    private readonly Label statusLabel;
    private readonly Label overallProgressTextLabel;
    private readonly ProgressBar overallProgressBar;
    private readonly Label progressTextLabel;
    private readonly ProgressBar progressBar;
    private readonly Label logLabel;
    private readonly ListBox logListBox;
    private readonly TableLayoutPanel modelPackSelectionPanel;
    private CheckBox modelPackTopCheckBox = null!;
    private DataGridView modelPackGrid = null!;
    private Label modelPackNoPacksLabel = null!;
    private Label modelPackSelectedSummaryLabel = null!;
    private readonly Button actionButton;
    private readonly Button continueButton;
    private readonly Dictionary<string, int> loggedProgressBuckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> pendingSetupInfoMessages = [];
    private readonly List<string> pendingSetupWarningMessages = [];
    private InstallerModelPackSelectionPageModel? modelPackSelectionPageModel;
    private IReadOnlyList<InstallerModelPackSelectionRow> selectedModelPackRows = [];
    private IReadOnlyList<InstallerModelPackAcquiredFile> acquiredModelPackFiles = [];
    private IReadOnlyList<InstallerModelPackImportedPack> importedModelPackFiles = [];
    private string? modelPackCatalogCurrentIw3Version;
    private string? modelPackCatalogV3dfyVersion;
    private bool updatingModelPackControls;
    private bool payloadInstallStarted;
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

        root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        headingLabel = new Label
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

        progressPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0, 0, 0, 12),
        };
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        overallProgressTextLabel = new Label
        {
            AutoSize = true,
            Text = "Overall progress",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 4),
        };
        overallProgressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = 18,
            Style = ProgressBarStyle.Continuous,
            MarqueeAnimationSpeed = 0,
            Minimum = 0,
            Maximum = 1000,
            Value = 0,
        };

        statusLabel.Margin = new Padding(0, 10, 0, 4);

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
        progressPanel.Controls.Add(overallProgressTextLabel, 0, 0);
        progressPanel.Controls.Add(overallProgressBar, 0, 1);
        progressPanel.Controls.Add(statusLabel, 0, 2);
        progressPanel.Controls.Add(progressBar, 0, 3);
        progressPanel.Controls.Add(progressTextLabel, 0, 4);

        logLabel = new Label
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

        modelPackSelectionPanel = CreateModelPackSelectionPanel();
        modelPackSelectionPanel.Visible = false;

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
        continueButton = new Button
        {
            Text = "Continue",
            AutoSize = true,
            Visible = false,
        };
        continueButton.Click += OnContinueModelPackSelectionClick;
        buttonPanel.Controls.Add(continueButton);
        buttonPanel.Controls.Add(actionButton);

        root.Controls.Add(headingLabel, 0, 0);
        root.Controls.Add(progressPanel, 0, 1);
        root.Controls.Add(modelPackSelectionPanel, 0, 1);
        root.SetRowSpan(modelPackSelectionPanel, 3);
        root.Controls.Add(logLabel, 0, 2);
        root.Controls.Add(logListBox, 0, 3);
        root.Controls.Add(buttonPanel, 0, 4);
        Controls.Add(root);
    }

    public int ExitCode { get; private set; } = 1;

    public IReadOnlyList<InstallerModelPackSelectionRow> SelectedModelPackRows => selectedModelPackRows;

    public IReadOnlyList<InstallerModelPackAcquiredFile> AcquiredModelPackFiles => acquiredModelPackFiles;

    public IReadOnlyList<InstallerModelPackImportedPack> ImportedModelPackFiles => importedModelPackFiles;

    private TableLayoutPanel CreateModelPackSelectionPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0, 0, 0, 12),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var bodyLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Text = InstallerModelPackSelectionPageModel.BodyText,
            Margin = new Padding(0, 0, 0, 12),
        };

        modelPackTopCheckBox = new CheckBox
        {
            AutoSize = true,
            ThreeState = true,
            Text = "Select all visible optional model packs",
            Margin = new Padding(0, 0, 0, 8),
        };
        modelPackTopCheckBox.Click += OnModelPackTopCheckBoxClick;

        modelPackNoPacksLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Text = InstallerModelPackSelectionPageModel.OfflineNoPacksText,
            Visible = false,
            Margin = new Padding(0, 0, 0, 12),
        };

        modelPackGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            EditMode = DataGridViewEditMode.EditOnEnter,
            MultiSelect = false,
            ReadOnly = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        modelPackGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Selected",
            HeaderText = string.Empty,
            Width = 34,
        });
        modelPackGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Model",
            HeaderText = "Model",
            ReadOnly = true,
            Width = 170,
        });
        modelPackGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "BestUse",
            HeaderText = "Best use",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True },
        });
        modelPackGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Size",
            HeaderText = "Size",
            ReadOnly = true,
            Width = 86,
        });
        modelPackGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "SourceStatus",
            HeaderText = "Source / Status",
            ReadOnly = true,
            Width = 140,
        });
        modelPackGrid.CurrentCellDirtyStateChanged += OnModelPackGridCurrentCellDirtyStateChanged;
        modelPackGrid.CellValueChanged += OnModelPackGridCellValueChanged;

        modelPackSelectedSummaryLabel = new Label
        {
            AutoSize = true,
            Text = "Selected: 0 model packs - 0 B",
            Margin = new Padding(0, 8, 0, 0),
        };

        panel.Controls.Add(bodyLabel, 0, 0);
        panel.Controls.Add(modelPackTopCheckBox, 0, 1);
        panel.Controls.Add(modelPackNoPacksLabel, 0, 2);
        panel.Controls.Add(modelPackGrid, 0, 3);
        panel.Controls.Add(modelPackSelectedSummaryLabel, 0, 4);
        return panel;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        running = true;
        AppendLogLine("Prepare setup");

        _ = BeginSetupFlowAsync();
    }

    private async Task BeginSetupFlowAsync()
    {
        try
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            var selectionPage = await PrepareModelPackSelectionPageAsync();
            if (selectionPage is not null && !cancellationTokenSource.IsCancellationRequested)
            {
                ShowModelPackSelectionPage(selectionPage);
                return;
            }

            StartPayloadInstall();
        }
        catch (OperationCanceledException)
        {
            ExitCode = 3;
            running = false;
            Close();
        }
    }

    private async Task<InstallerModelPackSelectionPageModel?> PrepareModelPackSelectionPageAsync()
    {
        if (string.IsNullOrWhiteSpace(options.ModelPacksManifestPath))
        {
            return null;
        }

        try
        {
            var manifest = await InstallerModelPackManifest.LoadAsync(
                options.ModelPacksManifestPath,
                cancellationTokenSource.Token);
            modelPackCatalogCurrentIw3Version = manifest.CurrentIw3Version;
            modelPackCatalogV3dfyVersion = manifest.V3dfyVersion;
            var discovery = options.Mode == PayloadInstallMode.Offline
                ? InstallerModelPackDiscovery.DiscoverOffline(
                    manifest,
                    options.ModelPacksSourceDirectory ?? options.PartsDirectory)
                : InstallerModelPackDiscovery.DiscoverWeb(manifest);

            return new InstallerModelPackSelectionPageModel(discovery);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InstallerModelPackManifestException or IOException or UnauthorizedAccessException)
        {
            const string warning =
                "Optional model-pack catalog could not be loaded. v3dfy will continue installing with its base model.";
            pendingSetupWarningMessages.Add(warning);
            AppendLogLine("WARNING: " + warning);
            AppendLogLine("WARNING: " + ex.Message);
            return null;
        }
    }

    private void ShowModelPackSelectionPage(InstallerModelPackSelectionPageModel pageModel)
    {
        modelPackSelectionPageModel = pageModel;
        headingLabel.Text = InstallerModelPackSelectionPageModel.TitleText;
        progressPanel.Visible = false;
        logLabel.Visible = false;
        logListBox.Visible = false;
        modelPackSelectionPanel.Visible = true;
        continueButton.Visible = true;
        continueButton.Enabled = true;
        actionButton.Text = "Cancel";
        actionButton.Enabled = true;

        LoadModelPackRows(pageModel);
        UpdateModelPackSelectionControls();
    }

    private void LoadModelPackRows(InstallerModelPackSelectionPageModel pageModel)
    {
        updatingModelPackControls = true;
        try
        {
            modelPackGrid.Rows.Clear();
            foreach (var row in pageModel.Rows)
            {
                var index = modelPackGrid.Rows.Add(
                    row.IsSelected,
                    row.DisplayName,
                    row.BestUse,
                    row.SizeText,
                    row.StatusText);
                modelPackGrid.Rows[index].Tag = row;
            }

            modelPackNoPacksLabel.Text = !string.IsNullOrWhiteSpace(pageModel.NoPacksMessage)
                ? pageModel.NoPacksMessage
                : InstallerModelPackSelectionPageModel.OfflineNoPacksText;
            modelPackNoPacksLabel.Visible = !pageModel.HasRows;
            modelPackGrid.Visible = pageModel.HasRows;
        }
        finally
        {
            updatingModelPackControls = false;
        }
    }

    private void UpdateModelPackSelectionControls()
    {
        if (modelPackSelectionPageModel is null)
        {
            return;
        }

        updatingModelPackControls = true;
        try
        {
            modelPackTopCheckBox.Enabled = modelPackSelectionPageModel.SelectionState.CanUseTopCheckbox;
            modelPackTopCheckBox.CheckState = modelPackSelectionPageModel.SelectionState.TopCheckboxState switch
            {
                InstallerModelPackTopSelectionState.Checked => CheckState.Checked,
                InstallerModelPackTopSelectionState.Indeterminate => CheckState.Indeterminate,
                _ => CheckState.Unchecked,
            };
            modelPackSelectedSummaryLabel.Text = modelPackSelectionPageModel.SelectedSummaryText;

            foreach (DataGridViewRow gridRow in modelPackGrid.Rows)
            {
                if (gridRow.Tag is InstallerModelPackSelectionRow selectionRow)
                {
                    gridRow.Cells[0].Value = selectionRow.IsSelected;
                }
            }
        }
        finally
        {
            updatingModelPackControls = false;
        }
    }

    private void OnModelPackTopCheckBoxClick(object? sender, EventArgs e)
    {
        if (modelPackSelectionPageModel is null)
        {
            return;
        }

        modelPackSelectionPageModel.SelectionState.ApplyTopCheckboxAction();
        UpdateModelPackSelectionControls();
    }

    private void OnModelPackGridCurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (modelPackGrid.IsCurrentCellDirty)
        {
            modelPackGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void OnModelPackGridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (updatingModelPackControls ||
            modelPackSelectionPageModel is null ||
            e.RowIndex < 0 ||
            e.ColumnIndex != 0)
        {
            return;
        }

        var gridRow = modelPackGrid.Rows[e.RowIndex];
        if (gridRow.Tag is not InstallerModelPackSelectionRow selectionRow)
        {
            return;
        }

        var selected = gridRow.Cells[0].Value is bool value && value;
        modelPackSelectionPageModel.SelectionState.SetSelected(selectionRow.PackId, selected);
        UpdateModelPackSelectionControls();
    }

    private void OnContinueModelPackSelectionClick(object? sender, EventArgs e)
    {
        if (modelPackSelectionPageModel is not null)
        {
            selectedModelPackRows = modelPackSelectionPageModel.SelectionState.SelectedRows;
            if (selectedModelPackRows.Count == 0)
            {
                pendingSetupInfoMessages.Add("No optional model packs selected.");
                AppendLogLine("No optional model packs selected.");
            }
            else
            {
                var message =
                    $"Optional model packs selected for download, verification, and install after app payload: {selectedModelPackRows.Count}.";
                pendingSetupInfoMessages.Add(message);
                AppendLogLine(message);
            }
        }

        StartPayloadInstall();
    }

    private void StartPayloadInstall()
    {
        payloadInstallStarted = true;
        headingLabel.Text = "Installing v3dfy";
        modelPackSelectionPanel.Visible = false;
        progressPanel.Visible = true;
        logLabel.Visible = true;
        logListBox.Visible = true;
        continueButton.Visible = false;
        actionButton.Text = "Cancel";
        actionButton.Enabled = true;

        _ = Task.Run(RunInstallAsync);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (running && payloadInstallStarted)
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
            foreach (var warning in pendingSetupWarningMessages)
            {
                log.Warning(warning);
            }

            foreach (var info in pendingSetupInfoMessages)
            {
                log.Info(info);
            }

            await new PayloadInstaller().InstallAsync(
                options,
                log,
                cancellationTokenSource.Token,
                progress);

            var acquisitionResult = await AcquireSelectedModelPacksAsync(
                log,
                progress,
                cancellationTokenSource.Token);
            var importResult = await ImportAcquiredModelPacksAsync(
                acquisitionResult,
                log,
                progress,
                cancellationTokenSource.Token);
            if (acquisitionResult?.HasFailures == true || importResult?.HasFailures == true)
            {
                const string warning = "v3dfy installed with optional model-pack warnings.";
                log.Warning(warning);
            }

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

    private async Task<InstallerModelPackAcquisitionResult?> AcquireSelectedModelPacksAsync(
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        if (selectedModelPackRows.Count == 0)
        {
            return null;
        }

        var selectedCount = selectedModelPackRows.Count;
        var message = $"Downloading/verifying optional model packs: {selectedCount}.";
        log.Info(message);
        AppendLogLine(message);

        InstallerModelPackAcquisitionResult result;
        try
        {
            var workDirectory = Path.Combine(options.WorkDirectory, "model-packs");
            result = await new InstallerModelPackAcquisitionService().AcquireAsync(
                selectedModelPackRows,
                workDirectory,
                log,
                cancellationToken,
                progress);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.Warning($"Optional model-pack acquisition failed: {ex.Message}");
            result = new InstallerModelPackAcquisitionResult(
                AcquiredFiles: [],
                Failures: selectedModelPackRows.Select(row => new InstallerModelPackAcquisitionFailure(
                    row.PackId,
                    row.DisplayName,
                    row.AssetFileName,
                    row.SourceKind,
                    ex.Message)).ToArray());
        }

        acquiredModelPackFiles = result.AcquiredFiles;
        if (result.HasFailures)
        {
            AppendLogLine(
                $"Optional model packs verified: {result.SuccessCount}; acquisition failed: {result.FailureCount}.");
            return result;
        }

        var summary = $"Optional model packs verified: {result.SuccessCount}.";
        log.Info(summary);
        AppendLogLine(summary);
        return result;
    }

    private async Task<InstallerModelPackImportResult?> ImportAcquiredModelPacksAsync(
        InstallerModelPackAcquisitionResult? acquisitionResult,
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        if (acquisitionResult is null || acquisitionResult.AcquiredFiles.Count == 0)
        {
            return null;
        }

        InstallerModelPackImportResult result;
        try
        {
            result = await new InstallerModelPackImportService().ImportAsync(
                acquisitionResult.AcquiredFiles,
                options.TargetDirectory,
                options.WorkDirectory,
                modelPackCatalogCurrentIw3Version,
                modelPackCatalogV3dfyVersion,
                log,
                cancellationToken,
                progress);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.Warning($"Optional model-pack import failed: {ex.Message}");
            result = new InstallerModelPackImportResult(
                ImportedPacks: [],
                Failures: acquisitionResult.AcquiredFiles.Select(file => new InstallerModelPackImportFailure(
                    file.PackId,
                    file.DisplayName,
                    file.AssetFileName,
                    file.LocalZipPath,
                    ex.Message,
                    [])).ToArray());
        }

        importedModelPackFiles = result.ImportedPacks;
        if (result.HasFailures)
        {
            AppendLogLine(
                $"Optional model packs installed: {result.SuccessCount}; import failed: {result.FailureCount}.");
            return result;
        }

        var summary = $"Optional model packs installed: {result.SuccessCount}.";
        log.Info(summary);
        AppendLogLine(summary);
        return result;
    }

    private void OnActionButtonClick(object? sender, EventArgs e)
    {
        if (running && !payloadInstallStarted)
        {
            ExitCode = 3;
            running = false;
            cancellationTokenSource.Cancel();
            Close();
            return;
        }

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
            UpdateOverallProgress(progress);
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
            overallProgressBar.Style = ProgressBarStyle.Continuous;
            overallProgressBar.Value = overallProgressBar.Maximum;
            overallProgressTextLabel.Text = "Installation complete";
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
            overallProgressTextLabel.Text = "Failed";
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
                FormatPartStatus("Downloading", partNumber.Value),
            SetupProgressPhase.VerifyingPart when partNumber is not null =>
                FormatPartStatus("Verifying", partNumber.Value),
            SetupProgressPhase.FindingPart when partNumber is not null =>
                FormatPartStatus("Finding", partNumber.Value),
            SetupProgressPhase.RebuildingZip => "Rebuilding portable package",
            SetupProgressPhase.VerifyingZip => "Verifying portable package",
            SetupProgressPhase.ExtractingPayload => "Extracting files",
            SetupProgressPhase.InstallingPayload => "Installing v3dfy",
            SetupProgressPhase.DownloadingModelPack => "Downloading optional model pack",
            SetupProgressPhase.VerifyingModelPack => "Verifying optional model pack",
            SetupProgressPhase.ValidatingModelPack => "Validating optional model pack",
            SetupProgressPhase.InstallingModelPack => "Installing optional model pack",
            SetupProgressPhase.CleaningUp => "Cleaning temporary files",
            SetupProgressPhase.Completed => "Installation complete",
            _ => progress.Message.TrimEnd('.'),
        };
    }

    private void UpdateOverallProgress(SetupProgressEvent progress)
    {
        if (progress.OverallPercent is not { } percent)
        {
            return;
        }

        overallProgressBar.Style = ProgressBarStyle.Continuous;
        overallProgressBar.MarqueeAnimationSpeed = 0;
        overallProgressBar.Value = Math.Clamp(
            (int)Math.Round(percent * 10),
            overallProgressBar.Minimum,
            overallProgressBar.Maximum);
        overallProgressTextLabel.Text = !string.IsNullOrWhiteSpace(progress.OverallMessage)
            ? progress.OverallMessage
            : FormatOverallProgressText(progress.OverallCompletedUnits, progress.OverallTotalUnits);
    }

    private static string FormatPartStatus(string action, int partNumber) =>
        $"{action} payload part {partNumber}";

    private static string FormatOverallProgressText(int? completedUnits, int? totalUnits)
    {
        if (completedUnits is not { } completed || totalUnits is not { } total || total <= 0)
        {
            return "Overall progress";
        }

        return completed >= total
            ? "Installation complete"
            : "Overall progress";
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
            SetupProgressPhase.DownloadingModelPack when progress.Percent is > 0 and < 100 =>
                $"Download optional model pack progress: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.DownloadingModelPack => $"Download optional model pack: {fileName}",
            SetupProgressPhase.VerifyingModelPack when progress.Percent is > 0 and < 100 =>
                $"Verify optional model pack progress: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.VerifyingModelPack => $"Verify optional model pack: {fileName}",
            SetupProgressPhase.ValidatingModelPack => $"Validate optional model pack: {fileName}",
            SetupProgressPhase.InstallingModelPack when progress.Percent is > 0 and < 100 =>
                $"Install optional model pack progress: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.InstallingModelPack => $"Install optional model pack: {fileName}",
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
