namespace V3dfy.Tests.App;

public sealed class MainWindowModelPackImportSourceTests
{
    [Fact]
    public void ImportModelPackCommand_IsUnavailableDuringActiveWorkflows()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var commandInitialization = ExtractSourceRange(
            source,
            "ImportModelPackCommand = new AsyncRelayCommand",
            "ConfirmReplaceVideoCommand = new RelayCommand");
        var canImportProperty = ExtractSourceRange(
            source,
            "public bool CanImportModelPack",
            "public string ImportModelPackText");

        Assert.Contains("ImportModelPackAsync", commandInitialization);
        Assert.Contains("() => CanImportModelPack", commandInitialization);
        Assert.Contains("!IsConversionRunning", canImportProperty);
        Assert.Contains("!IsPreviewGenerating", canImportProperty);
        Assert.Contains("!IsAnalyzing", canImportProperty);
        Assert.Contains("!IsModelPackImportRunning", canImportProperty);
    }

    [Fact]
    public void ImportModelPackCommand_ResetsRunningFlagInFinally()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var method = ExtractSourceRange(
            source,
            "private async Task ImportModelPackAsync",
            "private ModelPackAppImportRequest CreateModelPackAppImportRequest");

        Assert.Contains("IsModelPackImportRunning = true;", method);
        Assert.Contains("ShowGlobalBusyOverlay(", method);
        Assert.Contains("catch (Exception exception)", method);
        Assert.Contains("finally", method);
        Assert.Contains("IsModelPackImportRunning = false;", method);
        Assert.Contains("HideGlobalBusyOverlay();", method);
    }

    [Fact]
    public void GlobalBusyOverlay_BlocksAppDuringShortModelPackAndRefreshOperations()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var overlay = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"GlobalBusyOverlay\"",
            "</Window>");
        var importMethod = ExtractSourceRange(
            source,
            "private async Task ImportModelPackAsync",
            "private ModelPackAppImportRequest CreateModelPackAppImportRequest");
        var requestFactory = ExtractSourceRange(
            source,
            "private ModelPackAppImportRequest CreateModelPackAppImportRequest",
            "private async Task<bool> ConfirmModelPackImportAsync");
        var confirmMethod = ExtractSourceRange(
            source,
            "private void ConfirmModelPackImport",
            "private void CancelModelPackImport");

        Assert.Contains("Visibility=\"{Binding GlobalBusyOverlayVisibility}\"", overlay);
        Assert.Contains("Background=\"#80000000\"", overlay);
        Assert.Contains("Text=\"{Binding GlobalBusyText}\"", overlay);
        Assert.Contains("IsIndeterminate=\"True\"", overlay);
        Assert.Contains("Panel.ZIndex=\"30\"", xaml);
        Assert.Contains("public bool IsGlobalBusyOverlayVisible", source);
        Assert.Contains("public Visibility GlobalBusyOverlayVisibility", source);
        Assert.Contains("public string GlobalBusyText", source);
        Assert.Contains("\"Loading...\"", source);
        Assert.Contains("\"Cargando...\"", source);
        Assert.Contains("\"Validating model pack...\"", importMethod);
        Assert.Contains("\"Validando paquete de modelos...\"", importMethod);
        Assert.Contains("HideGlobalBusyOverlay();", requestFactory);
        Assert.Contains("\"Refreshing model inventory...\"", requestFactory);
        Assert.Contains("\"Actualizando inventario de modelos...\"", requestFactory);
        Assert.Contains("\"Importing model pack...\"", confirmMethod);
        Assert.Contains("\"Importando paquete de modelos...\"", confirmMethod);
    }

    [Fact]
    public void GlobalBusyOverlay_IsNotUsedAsPreviewOrConversionProgressUi()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var previewMethod = ExtractSourceRange(
            source,
            "private async Task GeneratePreviewCoreAsync",
            "private void ApplyPreviewProgressUpdate");
        var conversionMethod = ExtractSourceRange(
            source,
            "private async Task StartConversionAsync",
            "private void ApplyConversionProgressUpdate");

        Assert.DoesNotContain("ShowGlobalBusyOverlay", previewMethod);
        Assert.DoesNotContain("GlobalBusy", previewMethod);
        Assert.DoesNotContain("ShowGlobalBusyOverlay", conversionMethod);
        Assert.DoesNotContain("GlobalBusy", conversionMethod);
        Assert.Contains("IsPreviewGeneratingModalOpen = true;", previewMethod);
        Assert.Contains("ConversionExecutionStatus.Running", conversionMethod);
    }

    [Fact]
    public void ImportModelPackCommand_UsesPickerCoordinatorAndExistingRefreshPath()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var constructor = ExtractSourceRange(
            source,
            "_modelPackImportCoordinator = new ModelPackAppImportCoordinator",
            "SelectVideoCommand = new RelayCommand");
        var requestFactory = ExtractSourceRange(
            source,
            "private ModelPackAppImportRequest CreateModelPackAppImportRequest",
            "private void ApplyModelPackImportResult");

        Assert.Contains("new ModelPackFilePicker(() => IsSpanish)", constructor);
        Assert.Contains("new InAppModelPackImportConfirmationService(ConfirmModelPackImportAsync)", constructor);
        Assert.Contains("Path.Combine(AppContext.BaseDirectory, SetupHelperExecutableName)", requestFactory);
        Assert.Contains("_dependencyHealth?.Iw3CliCapabilities.BundledIw3Version", requestFactory);
        Assert.Contains("BeforeExecutionAsync", requestFactory);
        Assert.Contains("RefreshAfterSuccessfulImportAsync", requestFactory);
        Assert.Contains("RefreshEngineStatusAsync(logRefresh: true)", requestFactory);
        Assert.DoesNotContain("MarkPreviewOutdated", requestFactory);
    }

    [Fact]
    public void ImportModelPackCommand_InvalidPreparationDoesNotReportSuccess()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var applyResult = ExtractSourceRange(
            source,
            "private void ApplyModelPackImportResult",
            "private void RecordValidModelPackPreparation");
        var invalidHandler = ExtractSourceRange(
            source,
            "private void RecordInvalidModelPackPreparation",
            "private void RecordFailedModelPackImport");
        var failedHandler = ExtractSourceRange(
            source,
            "private void RecordFailedModelPackImport",
            "private void RecordSuccessfulModelPackImport");

        Assert.Contains("ModelPackAppImportStatus.Invalid", applyResult);
        Assert.Contains("RecordInvalidModelPackPreparation(result);", applyResult);
        Assert.Contains("Model pack validation failed.", invalidHandler);
        Assert.Contains("Helper was not launched.", invalidHandler);
        Assert.Contains("Model pack import failed.", failedHandler);
        Assert.DoesNotContain("Model pack import completed.", invalidHandler);
        Assert.DoesNotContain("Model pack import completed.", failedHandler);
    }

    [Fact]
    public void ImportModelPackCommand_HandlesConfirmationCancelWithoutFailure()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var applyResult = ExtractSourceRange(
            source,
            "private void ApplyModelPackImportResult",
            "private void RecordValidModelPackPreparation");
        var canceledHandler = ExtractSourceRange(
            source,
            "private void RecordCanceledModelPackImport",
            "private void RecordInvalidModelPackPreparation");

        Assert.Contains("result.ConfirmationCanceled", applyResult);
        Assert.Contains("RecordCanceledModelPackImport(result);", applyResult);
        Assert.Contains("canceled before Windows administrator permission", canceledHandler);
        Assert.Contains("No files were installed.", canceledHandler);
        Assert.DoesNotContain("failed", canceledHandler, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportModelPackCommand_UsesConfirmationPromptAndClearUacFailureText()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var validHandler = ExtractSourceRange(
            source,
            "private void RecordValidModelPackPreparation",
            "private void RecordCanceledModelPackImport");
        var failedHandler = ExtractSourceRange(
            source,
            "private void RecordFailedModelPackImport",
            "private void RecordSuccessfulModelPackImport");
        var coordinator = ReadRepoFile("src", "V3dfy.Infrastructure", "ModelPacks", "ModelPackAppImportCoordinator.cs");

        Assert.Contains("ModelPackImportConfirmationFormatter.CreatePrompt(preparation)", validHandler);
        Assert.Contains("administrator permission", validHandler, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("helperWasNotStarted", failedHandler);
        Assert.Contains("Windows administrator permission may have been canceled", failedHandler);
        Assert.Contains("helper executable was not found", coordinator, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfirmationFormatter_IncludesSelectabilityAndBilingualUacText()
    {
        var formatter = ReadRepoFile("src", "V3dfy.Infrastructure", "ModelPacks", "ModelPackImportConfirmationFormatter.cs");

        Assert.Contains("Administrator/UAC permission", formatter);
        Assert.Contains("Permiso de administrador/UAC", formatter);
        Assert.Contains("Files to install", formatter);
        Assert.Contains("Already installed files", formatter);
        Assert.Contains("Conflicts", formatter);
        Assert.Contains("Target folder", formatter);
        Assert.Contains("supports or maps", formatter);
        Assert.Contains("soporte o los tenga mapeados", formatter);
    }

    [Fact]
    public void MainWindowXaml_ExposesModelPackImportFromModelInventoryModalOnly()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var inventoryModal = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding ModelInventoryModalContentVisibility}\"",
            "Visibility=\"{Binding ModelPackImportConfirmationModalContentVisibility}\"");
        var settingsModelsSection = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ModelsSettingsSection\"",
            "AutomationProperties.AutomationId=\"ToolsEngineSettingsSection\"");

        Assert.Contains("AutomationProperties.AutomationId=\"ImportModelPackButton\"", xaml);
        Assert.Contains("Command=\"{Binding ImportModelPackCommand}\"", xaml);
        Assert.Contains("Content=\"{Binding ImportModelPackText}\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding CanImportModelPack}\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SettingsImportModelPackButton\"", settingsModelsSection);
        Assert.Contains("Command=\"{Binding ShowModelInventoryCommand}\"", settingsModelsSection);
        Assert.Contains("Text=\"{Binding ModelPackImportStatusText}\"", settingsModelsSection);
        Assert.Contains("Text=\"{Binding LastModelPackImportSummary}\"", settingsModelsSection);
        Assert.Contains("Text=\"{Binding ModelPackImportInstructionText}\"", inventoryModal);
        Assert.Contains("Text=\"{Binding ModelPackImportStatusText}\"", inventoryModal);
        Assert.Contains("Text=\"{Binding LastModelPackImportSummary}\"", inventoryModal);
    }

    [Fact]
    public void ToolStatusModelRow_ExposesViewModelsContextAction()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var rowViewModel = ReadRepoFile("src", "V3dfy.App", "ViewModels", "ToolStatusItemViewModel.cs");
        var createToolStatus = ExtractSourceRange(
            source,
            "private ToolStatusItemViewModel CreateToolStatus",
            "private string ToolStatusReasonText");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");

        Assert.Contains("ToolStatusComponent.Models", createToolStatus);
        Assert.Contains("ViewModelsText", createToolStatus);
        Assert.Contains("ViewModelsToolTipText", createToolStatus);
        Assert.Contains("ShowModelInventoryCommand", createToolStatus);
        Assert.Contains("ICommand? ContextActionCommand", rowViewModel);
        Assert.Contains("Command=\"{Binding ContextActionCommand}\"", xaml);
        Assert.Contains("public string ViewModelsText => Text(\"View models\", \"Ver modelos\")", source);
    }

    [Fact]
    public void ModelPackConfirmation_DoesNotUseNativeWindowsMessageBox()
    {
        var appSources = ReadRepoFiles("src", "V3dfy.App", "*.cs");

        Assert.DoesNotContain("System.Windows.MessageBox", appSources);
        Assert.DoesNotContain("MessageBox.Show", appSources);
        Assert.DoesNotContain("WpfModelPackImportConfirmationService", appSources);
    }

    [Fact]
    public void ModelPackConfirmation_UsesInAppModalStateAndCommands()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var confirmationMethod = ExtractSourceRange(
            source,
            "private async Task<bool> ConfirmModelPackImportAsync",
            "private void ApplyModelPackImportResult");

        Assert.Contains("TaskCompletionSource<bool>? _modelPackImportConfirmationCompletion", source);
        Assert.Contains("ModelPackImportConfirmationPrompt? _modelPackImportConfirmationPrompt", source);
        Assert.Contains("public bool IsModelPackImportConfirmationModalOpen", source);
        Assert.Contains("public Visibility ModelPackImportConfirmationModalContentVisibility", source);
        Assert.Contains("public RelayCommand ConfirmModelPackImportCommand", source);
        Assert.Contains("public RelayCommand CancelModelPackImportCommand", source);
        Assert.Contains("ConfirmModelPackImportAsync", confirmationMethod);
        Assert.Contains("IsModelPackImportConfirmationModalOpen = true;", confirmationMethod);
        Assert.Contains("WaitAsync(cancellationToken)", confirmationMethod);
        Assert.Contains("InAppModelPackImportConfirmationService", source);
    }

    [Fact]
    public void ModelPackConfirmation_ResetsModalStateAfterCancelAndConfirm()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var modalCompletionMethods = ExtractSourceRange(
            source,
            "private void ConfirmModelPackImport",
            "private void CompleteReplaceVideoConfirmation");

        Assert.Contains("CompleteModelPackImportConfirmation(confirmImport: true);", modalCompletionMethods);
        Assert.Contains("CompleteModelPackImportConfirmation(confirmImport: false);", modalCompletionMethods);
        Assert.Contains("ResetModelPackImportConfirmationModal();", modalCompletionMethods);
        Assert.Contains("completion?.TrySetResult(confirmImport);", modalCompletionMethods);
        Assert.Contains("IsModelPackImportConfirmationModalOpen = false;", modalCompletionMethods);
        Assert.Contains("_modelPackImportConfirmationCompletion = null;", modalCompletionMethods);
        Assert.Contains("_modelPackImportConfirmationPrompt = null;", modalCompletionMethods);
    }

    [Fact]
    public void MainWindowXaml_ExposesStyledModelPackConfirmationModal()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var modalBody = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding ModelPackImportConfirmationModalContentVisibility}\"",
            "Visibility=\"{Binding ReplaceVideoConfirmationModalContentVisibility}\"");

        Assert.Contains("Style=\"{StaticResource V3dfyModalOverlayStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource V3dfyModalCardStyle}\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ModelPackImportConfirmationSummary\"", modalBody);
        Assert.Contains("Text=\"{Binding ModelPackImportConfirmationIntroText}\"", modalBody);
        Assert.Contains("Text=\"{Binding ModelPackImportConfirmationMessageText, Mode=OneWay}\"", modalBody);
        Assert.Contains("Background=\"{DynamicResource LogBackgroundBrush}\"", modalBody);
        Assert.Contains("BorderBrush=\"{DynamicResource CardBorderBrush}\"", modalBody);
        Assert.Contains("Style BasedOn=\"{StaticResource ModernVerticalScrollBarStyle}\"", modalBody);
        Assert.DoesNotContain("#FFFFFF", modalBody);
        Assert.DoesNotContain("#000000", modalBody);
    }

    [Fact]
    public void MainWindowXaml_ModelPackConfirmationButtonsUseLocalizedBindings()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var footer = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"CancelModelPackImportButton\"",
            "AutomationProperties.AutomationId=\"CancelReplaceVideoButton\"");

        Assert.Contains("Command=\"{Binding CancelModelPackImportCommand}\"", footer);
        Assert.Contains("Content=\"{Binding CancelDialogText}\"", footer);
        Assert.Contains("Style=\"{StaticResource SecondaryButtonStyle}\"", footer);
        Assert.Contains("Command=\"{Binding ConfirmModelPackImportCommand}\"", footer);
        Assert.Contains("Content=\"{Binding ModelPackImportConfirmationContinueText}\"", footer);
        Assert.Contains("public string ModelPackImportConfirmationTitleText => Text(", source);
        Assert.Contains("public string ModelPackImportConfirmationMessageText => Text(", source);
        Assert.Contains("public string ModelPackImportConfirmationContinueText => Text(\"Continue\", \"Continuar\")", source);
        Assert.Contains("return ModelPackImportConfirmationTitleText;", source);
    }

    [Fact]
    public void MainWindowXaml_ExposesStyledModelInventoryModal()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var modalBody = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding ModelInventoryModalContentVisibility}\"",
            "Visibility=\"{Binding ModelPackImportConfirmationModalContentVisibility}\"");

        Assert.Contains("Style=\"{StaticResource V3dfyModalOverlayStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource V3dfyModalCardStyle}\"", xaml);
        Assert.Contains("Width=\"{Binding ActiveModalWidth}\"", xaml);
        Assert.Contains("ActiveModalWidth => IsModelInventoryModalOpen || IsModelHelpModalOpen || IsSettingsModalOpen ? 1000d : 760d", source);
        Assert.Contains("Text=\"{Binding ModelInventoryIntroText}\"", modalBody);
        Assert.Contains("Text=\"{Binding ModelInventoryFolderPathText}\"", modalBody);
        Assert.Contains("Text=\"{Binding SelectableModelsSectionTitleText}\"", modalBody);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectableModelsInventory\"", modalBody);
        Assert.Contains("Text=\"{Binding DiagnosticModelsSectionTitleText}\"", modalBody);
        Assert.Contains("AutomationProperties.AutomationId=\"DiagnosticModelsInventory\"", modalBody);
        Assert.Contains("Text=\"{Binding RuntimeDependenciesSectionTitleText}\"", modalBody);
        Assert.Contains("AutomationProperties.AutomationId=\"RuntimeDependenciesInventory\"", modalBody);
        Assert.Contains("Background=\"{DynamicResource LogBackgroundBrush}\"", modalBody);
        Assert.Contains("BorderBrush=\"{DynamicResource CardBorderBrush}\"", modalBody);
        Assert.DoesNotContain("#FFFFFF", modalBody);
        Assert.DoesNotContain("#000000", modalBody);
    }

    [Fact]
    public void MainWindowXaml_ModelInventoryModalUsesTwoColumnContentLayout()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var modalBody = ExtractSourceRange(
            xaml,
            "x:Name=\"ModelInventoryModalHorizontalViewport\"",
            "Visibility=\"{Binding ModelPackImportConfirmationModalContentVisibility}\"");

        Assert.Contains("x:Name=\"ModelInventoryModalHorizontalViewport\"", modalBody);
        Assert.Contains("Style=\"{StaticResource ResponsiveHorizontalViewportScrollViewerStyle}\"", modalBody);
        Assert.Contains("AutomationProperties.AutomationId=\"ModelInventoryResponsiveContent\"", modalBody);
        Assert.Contains("<Grid.ColumnDefinitions>", modalBody);
        Assert.Contains("<ColumnDefinition Width=\"7*\"", modalBody);
        Assert.Contains("MinWidth=\"620\"", modalBody);
        Assert.Contains("<ColumnDefinition Width=\"3*\"", modalBody);
        Assert.Contains("MinWidth=\"260\"", modalBody);
        Assert.Contains("<ScrollViewer Grid.Column=\"0\"", modalBody);
        Assert.Contains("<ScrollViewer Grid.Column=\"2\"", modalBody);
        Assert.DoesNotContain("Margin=\"18,0,0,0\"", modalBody);
    }

    [Fact]
    public void MainWindowXaml_ModelInventoryLeftColumnContainsInventorySections()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var modalBody = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding ModelInventoryModalContentVisibility}\"",
            "Visibility=\"{Binding ModelPackImportConfirmationModalContentVisibility}\"");
        var leftColumn = ExtractSourceRange(
            modalBody,
            "<ScrollViewer Grid.Column=\"0\"",
            "<ScrollViewer Grid.Column=\"2\"");

        Assert.Contains("Text=\"{Binding ModelInventoryFolderLabelText}\"", leftColumn);
        Assert.Contains("Text=\"{Binding ModelInventoryFolderPathText}\"", leftColumn);
        Assert.Contains("Text=\"{Binding SelectableModelsSectionTitleText}\"", leftColumn);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectableModelsInventory\"", leftColumn);
        Assert.Contains("Text=\"{Binding DiagnosticModelsSectionTitleText}\"", leftColumn);
        Assert.Contains("AutomationProperties.AutomationId=\"DiagnosticModelsInventory\"", leftColumn);
        Assert.Contains("Text=\"{Binding RuntimeDependenciesSectionTitleText}\"", leftColumn);
        Assert.Contains("AutomationProperties.AutomationId=\"RuntimeDependenciesInventory\"", leftColumn);
        Assert.DoesNotContain("ModelInventoryActionsTitleText", leftColumn);
        Assert.DoesNotContain("ModelPackImportStatusText", leftColumn);
    }

    [Fact]
    public void MainWindowXaml_ModelInventoryRightColumnContainsStatusOnly()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var modalBody = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding ModelInventoryModalContentVisibility}\"",
            "Visibility=\"{Binding ModelPackImportConfirmationModalContentVisibility}\"");
        var rightColumn = ExtractSourceRange(
            modalBody,
            "<ScrollViewer Grid.Column=\"2\"",
            "</Grid>");

        Assert.Contains("Text=\"{Binding ModelInventoryActionsTitleText}\"", rightColumn);
        Assert.Contains("Text=\"{Binding ModelPackImportInstructionText}\"", rightColumn);
        Assert.Contains("Text=\"{Binding ModelPackImportStatusText}\"", rightColumn);
        Assert.Contains("Text=\"{Binding LastModelPackImportSummary}\"", rightColumn);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImportModelPackButton\"", rightColumn);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"OpenModelsFolderButton\"", rightColumn);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"CloseModelInventoryButton\"", rightColumn);
        Assert.DoesNotContain("SelectableModelsInventory", rightColumn);
        Assert.DoesNotContain("DiagnosticModelsInventory", rightColumn);
        Assert.DoesNotContain("RuntimeDependenciesInventory", rightColumn);
    }

    [Fact]
    public void ModelInventoryModal_UsesLocalizedTitleSectionsAndInventoryText()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("public string ModelInventoryTitleText => Text(\"3D models\", \"Modelos 3D\")", source);
        Assert.Contains("public string SelectableModelsSectionTitleText => Text(", source);
        Assert.Contains("\"Selectable models\"", source);
        Assert.Contains("\"Modelos seleccionables\"", source);
        Assert.Contains("\"Detected but not selectable\"", source);
        Assert.Contains("\"Detectados no seleccionables\"", source);
        Assert.Contains("\"Runtime dependencies\"", source);
        Assert.Contains("\"Dependencias de runtime\"", source);
        Assert.Contains("Reason: no verified v3dfy mapping / diagnostic only", source);
        Assert.Contains("Motivo: sin mapeo verificado de v3dfy / solo diagnostico", source);
        Assert.Contains("runtime dependency, not a selectable model", source);
        Assert.Contains("dependencia de runtime, no es un modelo seleccionable", source);
    }

    [Fact]
    public void ModelInventoryModal_HasImportOpenFolderAndCloseActions()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var footer = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImportModelPackButton\"",
            "AutomationProperties.AutomationId=\"CancelReplaceVideoButton\"");
        var modalBody = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding ModelInventoryModalContentVisibility}\"",
            "Visibility=\"{Binding ModelPackImportConfirmationModalContentVisibility}\"");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("Command=\"{Binding ImportModelPackCommand}\"", footer);
        Assert.Contains("Content=\"{Binding ImportModelPackText}\"", footer);
        Assert.Contains("IsEnabled=\"{Binding CanImportModelPack}\"", footer);
        Assert.Contains("Command=\"{Binding OpenModelsFolderCommand}\"", footer);
        Assert.Contains("Content=\"{Binding OpenModelsFolderText}\"", footer);
        Assert.Contains("Command=\"{Binding CloseModelInventoryCommand}\"", footer);
        Assert.Contains("Content=\"{Binding CloseDialogText}\"", footer);
        Assert.Contains("private void OpenModelsFolder()", source);
        Assert.Contains("private void CloseModelInventory()", source);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImportModelPackButton\"", modalBody);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"OpenModelsFolderButton\"", modalBody);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"CloseModelInventoryButton\"", modalBody);
    }

    [Fact]
    public void ImportModelPackCommand_RestoresModelInventoryModalAfterImportFlow()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var method = ExtractSourceRange(
            source,
            "private async Task ImportModelPackAsync",
            "private ModelPackAppImportRequest CreateModelPackAppImportRequest");
        var confirmationMethod = ExtractSourceRange(
            source,
            "private async Task<bool> ConfirmModelPackImportAsync",
            "private void ApplyModelPackImportResult");

        Assert.Contains("_reopenModelInventoryAfterImport = IsModelInventoryModalOpen;", method);
        Assert.Contains("if (_reopenModelInventoryAfterImport && !IsAnyModalOpen)", method);
        Assert.Contains("IsModelInventoryModalOpen = true;", method);
        Assert.Contains("IsModelInventoryModalOpen = false;", confirmationMethod);
        Assert.Contains("RefreshEngineStatusAsync(logRefresh: true)", source);
    }

    [Fact]
    public void ModelInventoryOpenedFromSettings_ReturnsToSettingsModelsOnClose()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var showModelInventory = ExtractSourceRange(
            source,
            "private void ShowModelInventory()",
            "private void CloseModelInventory()");
        var closeModelInventory = ExtractSourceRange(
            source,
            "private void CloseModelInventory()",
            "private void ShowModelHelp()");
        var importMethod = ExtractSourceRange(
            source,
            "private async Task ImportModelPackAsync()",
            "private ModelPackAppImportRequest CreateModelPackAppImportRequest");

        Assert.Contains("SettingsSection? _settingsSectionToRestoreAfterChildModal", source);
        Assert.Contains("private void CaptureSettingsReturnContext()", source);
        Assert.Contains("private void RestoreSettingsAfterChildModalIfNeeded()", source);
        Assert.Contains("CaptureSettingsReturnContext();", showModelInventory);
        Assert.Contains("IsSettingsModalOpen = false;", showModelInventory);
        Assert.Contains("IsModelInventoryModalOpen = true;", showModelInventory);
        Assert.Contains("RestoreSettingsAfterChildModalIfNeeded();", closeModelInventory);
        Assert.Contains("SelectedSettingsSection = section;", source);
        Assert.Contains("IsSettingsModalOpen = true;", source);
        Assert.Contains("if (IsSettingsModalOpen)", importMethod);
        Assert.Contains("CaptureSettingsReturnContext();", importMethod);
        Assert.Contains("RestoreSettingsAfterChildModalIfNeeded();", importMethod);
    }

    private static string ReadRepoFile(params string[] relativePath)
    {
        var candidate = FindRepoPath(relativePath);
        return File.ReadAllText(candidate);
    }

    private static string ReadRepoFiles(params string[] relativePathAndPattern)
    {
        Assert.True(relativePathAndPattern.Length >= 2);
        var searchPattern = relativePathAndPattern[^1];
        var directory = FindRepoPath(relativePathAndPattern[..^1]);
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string FindRepoPath(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var pathParts = new string[relativePath.Length + 1];
            pathParts[0] = directory.FullName;
            Array.Copy(relativePath, 0, pathParts, 1, relativePath.Length);

            var candidate = Path.Combine(pathParts);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativePath)}.");
    }

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        return source[start..end];
    }
}
