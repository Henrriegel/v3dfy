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
    private const int OverallProgressBarHeight = 18;
    private const int CurrentProgressBarHeight = 22;

    private readonly PayloadInstallOptions options;
    private readonly string? logPath;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly TableLayoutPanel root;
    private readonly Label headingLabel;
    private readonly Label subtitleLabel;
    private readonly FlowLayoutPanel setupOptionsPanel;
    private readonly Label languageLabel;
    private readonly ComboBox languageComboBox;
    private readonly Label themeLabel;
    private readonly ComboBox themeComboBox;
    private readonly TableLayoutPanel progressPanel;
    private readonly Label statusLabel;
    private readonly Label overallProgressTextLabel;
    private readonly ProgressBar overallProgressBar;
    private readonly Label currentProgressHeaderLabel;
    private readonly Label progressTextLabel;
    private readonly ProgressBar progressBar;
    private readonly Label logLabel;
    private readonly ListBox logListBox;
    private readonly TableLayoutPanel modelPackSelectionPanel;
    private Label modelPackBodyLabel = null!;
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
    private bool payloadInstalled;
    private bool replaceExistingTargetConfirmed;
    private bool running;
    private SetupUiLanguage selectedLanguage = SetupUiLanguage.English;
    private SetupUiThemeKind selectedThemeKind = SetupUiThemeKind.Dark;
    private SetupUiText uiText = SetupUiText.For(SetupUiLanguage.English);
    private SetupUiThemeDefinition uiTheme = SetupUiThemeDefinition.For(SetupUiThemeKind.Dark);

    public SetupProgressForm(
        PayloadInstallOptions options,
        string? logPath,
        CancellationToken cancellationToken)
    {
        this.options = options;
        this.logPath = logPath;
        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Text = uiText.WindowTitle;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 560);
        Size = new Size(880, 640);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(18),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        headingLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            Text = uiText.InstallingTitle,
            Margin = new Padding(0, 0, 0, 3),
        };

        subtitleLabel = new Label
        {
            AutoSize = true,
            Text = uiText.Subtitle,
            Margin = new Padding(0, 0, 0, 10),
        };

        setupOptionsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(0),
        };

        languageLabel = new Label
        {
            AutoSize = true,
            Text = uiText.LanguageLabel,
            Margin = new Padding(0, 6, 6, 0),
        };
        languageComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 130,
            Margin = new Padding(0, 0, 18, 0),
        };
        languageComboBox.Items.Add(new SetupComboBoxItem<SetupUiLanguage>(
            SetupUiLanguage.English,
            uiText.EnglishLanguageName));
        languageComboBox.Items.Add(new SetupComboBoxItem<SetupUiLanguage>(
            SetupUiLanguage.Spanish,
            uiText.SpanishLanguageName));
        languageComboBox.SelectedIndex = 0;
        languageComboBox.SelectedIndexChanged += OnLanguageSelectionChanged;

        themeLabel = new Label
        {
            AutoSize = true,
            Text = uiText.ThemeLabel,
            Margin = new Padding(0, 6, 6, 0),
        };
        themeComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 110,
            Margin = new Padding(0),
        };
        themeComboBox.Items.Add(new SetupComboBoxItem<SetupUiThemeKind>(
            SetupUiThemeKind.Light,
            uiText.LightThemeName));
        themeComboBox.Items.Add(new SetupComboBoxItem<SetupUiThemeKind>(
            SetupUiThemeKind.Dark,
            uiText.DarkThemeName));
        themeComboBox.SelectedIndex = 1;
        themeComboBox.SelectedIndexChanged += OnThemeSelectionChanged;

        setupOptionsPanel.Controls.Add(languageLabel);
        setupOptionsPanel.Controls.Add(languageComboBox);
        setupOptionsPanel.Controls.Add(themeLabel);
        setupOptionsPanel.Controls.Add(themeComboBox);

        var headingPanel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 6),
        };
        headingPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headingPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headingPanel.Controls.Add(headingLabel, 0, 0);
        headingPanel.Controls.Add(subtitleLabel, 0, 1);

        statusLabel = new Label
        {
            AutoSize = true,
            Text = uiText.PreparingSetup,
            Margin = new Padding(0, 0, 0, 8),
        };

        progressPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 6,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(14),
        };
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, OverallProgressBarHeight));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, CurrentProgressBarHeight));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        overallProgressTextLabel = new Label
        {
            AutoSize = true,
            Text = uiText.OverallProgress,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 4),
        };
        overallProgressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = OverallProgressBarHeight,
            MinimumSize = new Size(0, OverallProgressBarHeight),
            Style = ProgressBarStyle.Continuous,
            MarqueeAnimationSpeed = 0,
            Minimum = 0,
            Maximum = 1000,
            Value = 0,
        };

        statusLabel.Margin = new Padding(0, 8, 0, 4);

        currentProgressHeaderLabel = new Label
        {
            AutoSize = true,
            Text = uiText.CurrentProgress,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 12, 0, 4),
        };

        progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = CurrentProgressBarHeight,
            MinimumSize = new Size(0, CurrentProgressBarHeight),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Minimum = 0,
            Maximum = 1000,
            Value = 0,
        };
        progressTextLabel = new Label
        {
            AutoSize = true,
            Text = uiText.Working,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 6, 0, 0),
        };
        progressPanel.Controls.Add(overallProgressTextLabel, 0, 0);
        progressPanel.Controls.Add(overallProgressBar, 0, 1);
        progressPanel.Controls.Add(currentProgressHeaderLabel, 0, 2);
        progressPanel.Controls.Add(progressBar, 0, 3);
        progressPanel.Controls.Add(statusLabel, 0, 4);
        progressPanel.Controls.Add(progressTextLabel, 0, 5);

        logLabel = new Label
        {
            AutoSize = true,
            Text = uiText.Details,
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
            Text = uiText.CancelButton,
            AutoSize = true,
            MinimumSize = new Size(96, 32),
        };
        actionButton.Click += OnActionButtonClick;
        continueButton = new Button
        {
            Text = uiText.ContinueButton,
            AutoSize = true,
            MinimumSize = new Size(104, 32),
            Visible = false,
        };
        continueButton.Click += OnContinueModelPackSelectionClick;
        buttonPanel.Controls.Add(continueButton);
        buttonPanel.Controls.Add(actionButton);

        root.Controls.Add(headingPanel, 0, 0);
        root.Controls.Add(setupOptionsPanel, 0, 1);
        root.Controls.Add(progressPanel, 0, 2);
        root.Controls.Add(modelPackSelectionPanel, 0, 2);
        root.SetRowSpan(modelPackSelectionPanel, 3);
        root.Controls.Add(logLabel, 0, 3);
        root.Controls.Add(logListBox, 0, 4);
        root.Controls.Add(buttonPanel, 0, 5);
        Controls.Add(root);
        ApplyLocalizedText();
        ApplyTheme();
    }

    public int ExitCode { get; private set; } = 1;

    public IReadOnlyList<InstallerModelPackSelectionRow> SelectedModelPackRows => selectedModelPackRows;

    public IReadOnlyList<InstallerModelPackAcquiredFile> AcquiredModelPackFiles => acquiredModelPackFiles;

    public IReadOnlyList<InstallerModelPackImportedPack> ImportedModelPackFiles => importedModelPackFiles;

    public string WarningPrefix => uiText.WarningPrefix;

    public string ErrorPrefix => uiText.ErrorPrefix;

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

        modelPackBodyLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Text = uiText.OptionalModelPacksBody,
            Margin = new Padding(0, 0, 0, 12),
        };

        modelPackTopCheckBox = new CheckBox
        {
            AutoSize = true,
            ThreeState = true,
            Text = uiText.SelectAllVisibleOptionalModelPacks,
            Margin = new Padding(0, 0, 0, 8),
        };
        modelPackTopCheckBox.Click += OnModelPackTopCheckBoxClick;

        modelPackNoPacksLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Text = uiText.OfflineNoPacksText,
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
            EnableHeadersVisualStyles = false,
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
            HeaderText = uiText.ModelColumn,
            ReadOnly = true,
            Width = 170,
        });
        modelPackGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "BestUse",
            HeaderText = uiText.BestUseColumn,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True },
        });
        modelPackGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Size",
            HeaderText = uiText.SizeColumn,
            ReadOnly = true,
            Width = 86,
        });
        modelPackGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "SourceStatus",
            HeaderText = uiText.SourceStatusColumn,
            ReadOnly = true,
            Width = 140,
        });
        modelPackGrid.CurrentCellDirtyStateChanged += OnModelPackGridCurrentCellDirtyStateChanged;
        modelPackGrid.CellValueChanged += OnModelPackGridCellValueChanged;

        modelPackSelectedSummaryLabel = new Label
        {
            AutoSize = true,
            Text = uiText.FormatSelectedSummary(0, "0 B"),
            Margin = new Padding(0, 8, 0, 0),
        };

        panel.Controls.Add(modelPackBodyLabel, 0, 0);
        panel.Controls.Add(modelPackTopCheckBox, 0, 1);
        panel.Controls.Add(modelPackNoPacksLabel, 0, 2);
        panel.Controls.Add(modelPackGrid, 0, 3);
        panel.Controls.Add(modelPackSelectedSummaryLabel, 0, 4);
        return panel;
    }

    private void OnLanguageSelectionChanged(object? sender, EventArgs e)
    {
        if (languageComboBox.SelectedItem is not SetupComboBoxItem<SetupUiLanguage> item ||
            item.Value == selectedLanguage)
        {
            return;
        }

        selectedLanguage = item.Value;
        uiText = SetupUiText.For(selectedLanguage);
        ApplyLocalizedText();
    }

    private void OnThemeSelectionChanged(object? sender, EventArgs e)
    {
        if (themeComboBox.SelectedItem is not SetupComboBoxItem<SetupUiThemeKind> item ||
            item.Value == selectedThemeKind)
        {
            return;
        }

        selectedThemeKind = item.Value;
        uiTheme = SetupUiThemeDefinition.For(selectedThemeKind);
        ApplyTheme();
    }

    private void ApplyLocalizedText()
    {
        Text = uiText.WindowTitle;
        subtitleLabel.Text = uiText.Subtitle;
        languageLabel.Text = uiText.LanguageLabel;
        themeLabel.Text = uiText.ThemeLabel;
        RefreshThemeComboBoxItems();

        if (modelPackSelectionPanel.Visible)
        {
            headingLabel.Text = uiText.OptionalModelPacksTitle;
        }
        else
        {
            headingLabel.Text = uiText.InstallingTitle;
        }

        modelPackBodyLabel.Text = uiText.OptionalModelPacksBody;
        modelPackTopCheckBox.Text = uiText.SelectAllVisibleOptionalModelPacks;
        modelPackNoPacksLabel.Text = uiText.OfflineNoPacksText;
        UpdateModelPackGridHeaders();
        UpdateModelPackBestUseCells();
        UpdateModelPackSourceStatusCells();

        continueButton.Text = uiText.ContinueButton;
        actionButton.Text = running || !payloadInstallStarted
            ? uiText.CancelButton
            : uiText.CloseButton;
        logLabel.Text = uiText.Details;
        currentProgressHeaderLabel.Text = uiText.CurrentProgress;

        if (!payloadInstallStarted)
        {
            overallProgressTextLabel.Text = uiText.OverallProgress;
            statusLabel.Text = uiText.PreparingSetup;
            progressTextLabel.Text = uiText.Working;
        }

        UpdateModelPackSelectionControls();
    }

    private void RefreshThemeComboBoxItems()
    {
        var selectedTheme = selectedThemeKind;
        themeComboBox.SelectedIndexChanged -= OnThemeSelectionChanged;
        try
        {
            themeComboBox.Items.Clear();
            themeComboBox.Items.Add(new SetupComboBoxItem<SetupUiThemeKind>(
                SetupUiThemeKind.Light,
                uiText.LightThemeName));
            themeComboBox.Items.Add(new SetupComboBoxItem<SetupUiThemeKind>(
                SetupUiThemeKind.Dark,
                uiText.DarkThemeName));
            themeComboBox.SelectedIndex = selectedTheme == SetupUiThemeKind.Light ? 0 : 1;
        }
        finally
        {
            themeComboBox.SelectedIndexChanged += OnThemeSelectionChanged;
        }
    }

    private void UpdateModelPackGridHeaders()
    {
        if (modelPackGrid.Columns["Model"] is { } modelColumn)
        {
            modelColumn.HeaderText = uiText.ModelColumn;
        }

        if (modelPackGrid.Columns["BestUse"] is { } bestUseColumn)
        {
            bestUseColumn.HeaderText = uiText.BestUseColumn;
        }

        if (modelPackGrid.Columns["Size"] is { } sizeColumn)
        {
            sizeColumn.HeaderText = uiText.SizeColumn;
        }

        if (modelPackGrid.Columns["SourceStatus"] is { } sourceStatusColumn)
        {
            sourceStatusColumn.HeaderText = uiText.SourceStatusColumn;
        }
    }

    private void UpdateModelPackSourceStatusCells()
    {
        foreach (DataGridViewRow gridRow in modelPackGrid.Rows)
        {
            if (gridRow.Tag is not InstallerModelPackSelectionRow selectionRow)
            {
                continue;
            }

            gridRow.Cells["SourceStatus"].Value = GetModelPackSourceStatusText(selectionRow);
        }
    }

    private void UpdateModelPackBestUseCells()
    {
        foreach (DataGridViewRow gridRow in modelPackGrid.Rows)
        {
            if (gridRow.Tag is not InstallerModelPackSelectionRow selectionRow)
            {
                continue;
            }

            gridRow.Cells["BestUse"].Value = selectionRow.GetBestUse(selectedLanguage);
        }
    }

    private string GetModelPackSourceStatusText(InstallerModelPackSelectionRow selectionRow) =>
        selectionRow.SourceKind switch
        {
            InstallerModelPackSourceKind.OfflineLocalZip => uiText.OfflineLocalZipStatus,
            _ => uiText.WebReleaseAssetStatus,
        };

    private void ApplyTheme()
    {
        if (SystemInformation.HighContrast)
        {
            return;
        }

        var windowBackground = FromThemeColor(uiTheme.WindowBackground);
        var panelBackground = FromThemeColor(uiTheme.PanelBackground);
        var elevatedBackground = FromThemeColor(uiTheme.ElevatedBackground);
        var text = FromThemeColor(uiTheme.Text);
        var mutedText = FromThemeColor(uiTheme.MutedText);
        var accent = FromThemeColor(uiTheme.Accent);
        var buttonBackground = FromThemeColor(uiTheme.ButtonBackground);
        var buttonText = FromThemeColor(uiTheme.ButtonText);
        var border = FromThemeColor(uiTheme.Border);
        var gridBackground = FromThemeColor(uiTheme.GridBackground);
        var gridAlternate = FromThemeColor(uiTheme.GridAlternateBackground);
        var logBackground = FromThemeColor(uiTheme.LogBackground);

        BackColor = windowBackground;
        ForeColor = text;
        ApplyThemeToControl(root, windowBackground, text);
        ApplyThemeToControl(setupOptionsPanel, windowBackground, text);
        ApplyThemeToControl(progressPanel, panelBackground, text);
        ApplyThemeToControl(modelPackSelectionPanel, panelBackground, text);
        ApplyThemeToControl(logLabel, windowBackground, mutedText);
        ApplyThemeToControl(subtitleLabel, windowBackground, mutedText);

        overallProgressBar.BackColor = elevatedBackground;
        overallProgressBar.ForeColor = accent;
        progressBar.BackColor = elevatedBackground;
        progressBar.ForeColor = accent;

        StyleButton(continueButton, buttonBackground, buttonText, accent);
        StyleButton(actionButton, elevatedBackground, text, border);

        logListBox.BackColor = logBackground;
        logListBox.ForeColor = text;
        modelPackGrid.BackgroundColor = gridBackground;
        modelPackGrid.GridColor = border;
        modelPackGrid.DefaultCellStyle.BackColor = gridBackground;
        modelPackGrid.DefaultCellStyle.ForeColor = text;
        modelPackGrid.DefaultCellStyle.SelectionBackColor = accent;
        modelPackGrid.DefaultCellStyle.SelectionForeColor = buttonText;
        modelPackGrid.AlternatingRowsDefaultCellStyle.BackColor = gridAlternate;
        modelPackGrid.AlternatingRowsDefaultCellStyle.ForeColor = text;
        modelPackGrid.ColumnHeadersDefaultCellStyle.BackColor = elevatedBackground;
        modelPackGrid.ColumnHeadersDefaultCellStyle.ForeColor = text;
        modelPackGrid.RowHeadersDefaultCellStyle.BackColor = elevatedBackground;
        modelPackGrid.RowHeadersDefaultCellStyle.ForeColor = text;
        modelPackGrid.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void ApplyThemeToControl(Control control, Color background, Color foreground)
    {
        control.BackColor = background;
        control.ForeColor = foreground;
        foreach (Control child in control.Controls)
        {
            ApplyThemeToControl(child, background, foreground);
        }
    }

    private static void StyleButton(Button button, Color background, Color foreground, Color border)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = background;
        button.ForeColor = foreground;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.BorderSize = 1;
    }

    private static Color FromThemeColor(string htmlColor) =>
        ColorTranslator.FromHtml(htmlColor);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        running = true;
        AppendLogLine(uiText.PreparingSetup);

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

            if (!ConfirmTargetReplacementBeforeInstall())
            {
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
            var useSpanish = selectedLanguage == SetupUiLanguage.Spanish;
            var discovery = options.Mode == PayloadInstallMode.Offline
                ? InstallerModelPackDiscovery.DiscoverOffline(
                    manifest,
                    options.ModelPacksSourceDirectory ?? options.PartsDirectory,
                    useSpanish)
                : InstallerModelPackDiscovery.DiscoverWeb(manifest, useSpanish);

            return new InstallerModelPackSelectionPageModel(discovery, uiText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InstallerModelPackManifestException or IOException or UnauthorizedAccessException)
        {
            var warning = uiText.OptionalModelPackCatalogLoadFailed;
            pendingSetupWarningMessages.Add(warning);
            AppendLogLine($"{uiText.WarningPrefix}: {warning}");
            AppendLogLine($"{uiText.WarningPrefix}: {ex.Message}");
            return null;
        }
    }

    private void ShowModelPackSelectionPage(InstallerModelPackSelectionPageModel pageModel)
    {
        modelPackSelectionPageModel = pageModel;
        headingLabel.Text = uiText.OptionalModelPacksTitle;
        progressPanel.Visible = false;
        logLabel.Visible = false;
        logListBox.Visible = false;
        modelPackSelectionPanel.Visible = true;
        continueButton.Visible = true;
        continueButton.Enabled = true;
        actionButton.Text = uiText.CancelButton;
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
                    row.GetBestUse(selectedLanguage),
                    row.SizeText,
                    GetModelPackSourceStatusText(row));
                modelPackGrid.Rows[index].Tag = row;
            }

            modelPackNoPacksLabel.Text = !string.IsNullOrWhiteSpace(pageModel.NoPacksMessage)
                ? pageModel.NoPacksMessage
                : uiText.OfflineNoPacksText;
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
            modelPackSelectedSummaryLabel.Text = uiText.FormatSelectedSummary(
                modelPackSelectionPageModel.SelectionState.SelectedCount,
                modelPackSelectionPageModel.SelectionState.SelectedTotalSizeText);

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
                var message = uiText.FormatNoOptionalModelPacksSelected();
                pendingSetupInfoMessages.Add(message);
                AppendLogLine(message);
            }
            else
            {
                var message = uiText.FormatOptionalModelPacksSelected(selectedModelPackRows.Count);
                pendingSetupInfoMessages.Add(message);
                AppendLogLine(message);
            }
        }

        if (!ConfirmTargetReplacementBeforeInstall())
        {
            return;
        }

        StartPayloadInstall();
    }

    private bool ConfirmTargetReplacementBeforeInstall()
    {
        bool hasExistingContent;
        try
        {
            hasExistingContent = PayloadInstaller.TargetHasExistingContent(options.TargetDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ExitCode = 1;
            running = false;
            AppendLogLine($"{uiText.ErrorPrefix}: {ex.Message}");
            Close();
            return false;
        }

        if (options.AllowTargetReplacement || !hasExistingContent)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            uiText.FormatExistingInstallPrompt(options.TargetDirectory),
            uiText.ExistingInstallTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result == DialogResult.Yes)
        {
            replaceExistingTargetConfirmed = true;
            AppendLogLine(uiText.ExistingInstallReplacementAccepted);
            return true;
        }

        ExitCode = 3;
        running = false;
        AppendLogLine(uiText.InstallationCanceled);
        Close();
        return false;
    }

    private void StartPayloadInstall()
    {
        payloadInstallStarted = true;
        headingLabel.Text = uiText.InstallingTitle;
        ShowInstallProgressLayout();
        continueButton.Visible = false;
        actionButton.Text = uiText.CancelButton;
        actionButton.Enabled = true;

        _ = Task.Run(RunInstallAsync);
    }

    private void ShowInstallProgressLayout()
    {
        modelPackSelectionPanel.Visible = false;
        progressPanel.Visible = true;
        overallProgressTextLabel.Visible = true;
        overallProgressBar.Visible = true;
        currentProgressHeaderLabel.Visible = true;
        statusLabel.Visible = true;
        progressBar.Visible = true;
        progressTextLabel.Visible = true;
        logLabel.Visible = true;
        logListBox.Visible = true;
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
        ISetupProgress progress = new UiSetupProgress(this);
        var hasSelectedModelPacks = selectedModelPackRows.Count > 0;
        if (hasSelectedModelPacks)
        {
            progress = new SetupOptionalModelPackOverallProgressTracker(
                progress,
                selectedModelPackRows
                    .Select(static row => new SetupOptionalModelPackProgressItem(
                        row.AssetFileName,
                        row.ZipSizeBytes))
                    .ToArray());
        }

        try
        {
            payloadInstalled = false;
            foreach (var warning in pendingSetupWarningMessages)
            {
                log.Warning(warning);
            }

            foreach (var info in pendingSetupInfoMessages)
            {
                log.Info(info);
            }

            await new PayloadInstaller().InstallAsync(
                CreatePayloadInstallOptions(),
                log,
                cancellationTokenSource.Token,
                progress);
            payloadInstalled = true;

            var acquisitionResult = await AcquireSelectedModelPacksAsync(
                log,
                progress,
                cancellationTokenSource.Token);
            var importResult = await ImportAcquiredModelPacksAsync(
                acquisitionResult,
                log,
                progress,
                cancellationTokenSource.Token);
            var hasOptionalWarnings =
                acquisitionResult?.HasFailures == true ||
                importResult?.HasFailures == true;
            if (hasOptionalWarnings)
            {
                var warning = uiText.OptionalModelPackWarnings;
                log.Warning(warning);
            }

            var successMessage = CreateSuccessMessage(
                acquisitionResult,
                importResult,
                hasOptionalWarnings);
            if (hasSelectedModelPacks)
            {
                progress.Report(new SetupProgressEvent(
                    SetupProgressPhase.Completed,
                    successMessage));
            }

            ExitCode = 0;
            PostSuccess(successMessage);
        }
        catch (OperationCanceledException)
        {
            TryDeleteInstalledPayloadAfterFailure(log);
            log.Error(uiText.InstallationCanceled);
            PostFailure(uiText.InstallationCanceled);
        }
        catch (PayloadInstallException ex)
        {
            TryDeleteInstalledPayloadAfterFailure(log);
            log.Error(ex.Message);
            PostFailure(ex.Message);
        }
        catch (Exception ex)
        {
            TryDeleteInstalledPayloadAfterFailure(log);
            log.Error($"Unexpected setup helper failure: {ex.Message}");
            PostFailure($"Unexpected setup helper failure: {ex.Message}");
        }
    }

    private void TryDeleteInstalledPayloadAfterFailure(ISetupLog log)
    {
        var targetDirectory = Path.GetFullPath(options.TargetDirectory);
        if (!payloadInstallStarted ||
            !payloadInstalled ||
            !File.Exists(Path.Combine(targetDirectory, "V3dfy.App.exe")) ||
            !Directory.Exists(targetDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(targetDirectory, recursive: true);
            payloadInstalled = false;
            log.Warning(uiText.InstalledPayloadRemovedAfterFailure);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warning(uiText.FormatInstalledPayloadCleanupFailed(ex.Message));
        }
    }

    private PayloadInstallOptions CreatePayloadInstallOptions() => new()
    {
        Mode = options.Mode,
        ManifestPath = options.ManifestPath,
        TargetDirectory = options.TargetDirectory,
        WorkDirectory = options.WorkDirectory,
        PartsDirectory = options.PartsDirectory,
        ReleaseBaseUrlOverride = options.ReleaseBaseUrlOverride,
        ModelPacksManifestPath = options.ModelPacksManifestPath,
        ModelPacksSourceDirectory = options.ModelPacksSourceDirectory,
        KeepWorkDirectory = options.KeepWorkDirectory,
        AllowTargetReplacement = options.AllowTargetReplacement || replaceExistingTargetConfirmed,
    };

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
        var message = uiText.FormatDownloadingVerifyingOptionalModelPacks(selectedCount);
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
            log.Warning(uiText.FormatOptionalModelPackAcquisitionFailed(ex.Message));
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
            AppendLogLine(uiText.FormatOptionalModelPacksVerified(
                result.SuccessCount,
                result.FailureCount));
            return result;
        }

        var summary = uiText.FormatOptionalModelPacksVerified(result.SuccessCount, result.FailureCount);
        log.Info(summary);
        AppendLogLine(summary);
        return result;
    }

    private string CreateSuccessMessage(
        InstallerModelPackAcquisitionResult? acquisitionResult,
        InstallerModelPackImportResult? importResult,
        bool hasOptionalWarnings)
    {
        if (hasOptionalWarnings)
        {
            return uiText.OptionalModelPackWarnings;
        }

        if (selectedModelPackRows.Count == 0)
        {
            return uiText.SuccessWithBaseModel;
        }

        if (acquisitionResult?.SuccessCount > 0 || importResult?.SuccessCount > 0)
        {
            return uiText.SuccessWithOptionalModelPacks;
        }

        return uiText.SuccessWithBaseModel;
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
            log.Warning(uiText.FormatOptionalModelPackImportFailed(ex.Message));
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
            AppendLogLine(uiText.FormatOptionalModelPacksInstalled(
                result.SuccessCount,
                result.FailureCount));
            return result;
        }

        var summary = uiText.FormatOptionalModelPacksInstalled(result.SuccessCount, result.FailureCount);
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

        AppendLogLine(uiText.CancelRequested);
        statusLabel.Text = uiText.CancelingSetup;
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
                progressTextLabel.Text = uiText.Working;
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

    private void PostSuccess(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(async () =>
        {
            running = false;
            statusLabel.Text = message;
            overallProgressBar.Style = ProgressBarStyle.Continuous;
            overallProgressBar.Value = overallProgressBar.Maximum;
            overallProgressTextLabel.Text = uiText.InstallationComplete;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = progressBar.Maximum;
            progressTextLabel.Text = uiText.Complete;
            actionButton.Enabled = false;
            AppendLogLine(message);
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
            progressTextLabel.Text = uiText.Failed;
            overallProgressTextLabel.Text = uiText.Failed;
            statusLabel.Text = uiText.InstallationFailed;
            actionButton.Text = uiText.CloseButton;
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
                FormatPartStatus(uiText.DownloadingAction, partNumber.Value),
            SetupProgressPhase.VerifyingPart when partNumber is not null =>
                FormatPartStatus(uiText.VerifyingAction, partNumber.Value),
            SetupProgressPhase.FindingPart when partNumber is not null =>
                FormatPartStatus(uiText.FindingAction, partNumber.Value),
            SetupProgressPhase.RebuildingZip => uiText.RebuildingPortablePackage,
            SetupProgressPhase.VerifyingZip => uiText.VerifyingPortablePackage,
            SetupProgressPhase.ExtractingPayload => uiText.ExtractingFiles,
            SetupProgressPhase.InstallingPayload => uiText.InstallingV3dfy,
            SetupProgressPhase.DownloadingModelPack => uiText.DownloadingOptionalModelPack,
            SetupProgressPhase.VerifyingModelPack => uiText.VerifyingOptionalModelPack,
            SetupProgressPhase.ValidatingModelPack => uiText.ValidatingOptionalModelPack,
            SetupProgressPhase.InstallingModelPack => uiText.InstallingOptionalModelPack,
            SetupProgressPhase.CleaningUp => uiText.CleaningTemporaryFiles,
            SetupProgressPhase.Completed => uiText.InstallationComplete,
            SetupProgressPhase.Preparing => uiText.PreparingSetup,
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
            ? uiText.TranslateOverallMessage(progress.OverallMessage)
            : FormatOverallProgressText(progress.OverallCompletedUnits, progress.OverallTotalUnits);
    }

    private string FormatPartStatus(string action, int partNumber) =>
        uiText.FormatPayloadPartStatus(action, partNumber);

    private string FormatOverallProgressText(int? completedUnits, int? totalUnits)
    {
        if (completedUnits is not { } completed || totalUnits is not { } total || total <= 0)
        {
            return uiText.OverallProgress;
        }

        return completed >= total
            ? uiText.InstallationComplete
            : uiText.OverallProgress;
    }

    private string FormatLogLine(SetupProgressEvent progress)
    {
        var fileName = ShortenFileName(progress.CurrentFile);
        return progress.Phase switch
        {
            SetupProgressPhase.DownloadingPart when progress.Percent is > 0 and < 100 =>
                $"{uiText.LogDownloadProgressLabel}: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.DownloadingPart => $"{uiText.LogDownloadLabel}: {fileName}",
            SetupProgressPhase.FindingPart => $"{uiText.LogFindLabel}: {fileName}",
            SetupProgressPhase.VerifyingPart when progress.Percent is > 0 and < 100 =>
                $"{uiText.LogVerifyProgressLabel}: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.VerifyingPart => $"{uiText.LogVerifyLabel}: {fileName}",
            SetupProgressPhase.RebuildingZip when progress.Percent is > 0 and < 100 =>
                $"{uiText.LogRebuildProgressLabel}: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.RebuildingZip => uiText.LogRebuildPackageLabel,
            SetupProgressPhase.VerifyingZip when progress.Percent is > 0 and < 100 =>
                $"{uiText.LogVerifyPackageProgressLabel}: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.VerifyingZip => uiText.LogVerifyPackageLabel,
            SetupProgressPhase.ExtractingPayload when !string.IsNullOrWhiteSpace(progress.CurrentFile) =>
                $"{uiText.LogExtractLabel}: {ShortenArchivePath(progress.CurrentFile)}",
            SetupProgressPhase.ExtractingPayload => uiText.LogExtractFilesLabel,
            SetupProgressPhase.InstallingPayload => uiText.LogInstallFilesLabel,
            SetupProgressPhase.DownloadingModelPack when progress.Percent is > 0 and < 100 =>
                $"{uiText.LogDownloadOptionalModelPackProgressLabel}: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.DownloadingModelPack => $"{uiText.LogDownloadOptionalModelPackLabel}: {fileName}",
            SetupProgressPhase.VerifyingModelPack when progress.Percent is > 0 and < 100 =>
                $"{uiText.LogVerifyOptionalModelPackProgressLabel}: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.VerifyingModelPack => $"{uiText.LogVerifyOptionalModelPackLabel}: {fileName}",
            SetupProgressPhase.ValidatingModelPack => $"{uiText.LogValidateOptionalModelPackLabel}: {fileName}",
            SetupProgressPhase.InstallingModelPack when progress.Percent is > 0 and < 100 =>
                $"{uiText.LogInstallOptionalModelPackProgressLabel}: {FormatBytes(progress.CurrentBytes ?? 0)} / {FormatBytes(progress.TotalBytes ?? 0)} ({progress.Percent:0.0}%)",
            SetupProgressPhase.InstallingModelPack => $"{uiText.LogInstallOptionalModelPackLabel}: {fileName}",
            SetupProgressPhase.CleaningUp => uiText.LogCleanTemporaryFilesLabel,
            SetupProgressPhase.Completed => uiText.LogCompleteLabel,
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
        form.AppendLogLine($"{form.WarningPrefix}: {message}");
    }

    public void Error(string message)
    {
        fileLog.Error(message);
        form.AppendLogLine($"{form.ErrorPrefix}: {message}");
    }

    public void Dispose() => fileLog.Dispose();
}

internal sealed class UiSetupProgress(SetupProgressForm form) : ISetupProgress
{
    public void Report(SetupProgressEvent progress) => form.ReportProgress(progress);
}

internal sealed class SetupComboBoxItem<T>(T value, string text)
{
    public T Value { get; } = value;

    public override string ToString() => text;
}
