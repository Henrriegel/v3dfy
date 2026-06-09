namespace V3dfy.Tests.App;

public sealed class MainWindowViewModelLocalizationSourceTests
{
    [Fact]
    public void LiveConversionSummary_UsesLocalizedProfileAndCompatibilityLabels()
    {
        var source = ReadMainWindowViewModelSource();

        Assert.Contains("\"Live conversion\"", source);
        Assert.Contains("\"Conversion en vivo\"", source);
        Assert.Contains("\"Conversion summary\"", source);
        Assert.Contains("\"Resumen de conversion\"", source);
        Assert.Contains("\"Output profile\"", source);
        Assert.Contains("\"Perfil de salida\"", source);
        Assert.Contains("\"Custom based on", source);
        Assert.Contains("\"Personalizado basado en", source);
        Assert.Contains("\"Primary output\"", source);
        Assert.Contains("\"Salida principal\"", source);
        Assert.Contains("\"LG-compatible copy\"", source);
        Assert.Contains("\"Copia compatible LG\"", source);
        Assert.Contains("\"Create LG 3D TV 2012 compatible MP4 copy\"", source);
        Assert.Contains("\"Crear copia MP4 compatible con LG 3D TV 2012\"", source);
        Assert.Contains("\"Open LG-compatible copy when available\"", source);
        Assert.Contains("\"Abrir copia compatible LG cuando exista\"", source);
        Assert.Contains("\"Primary output was generated successfully", source);
        Assert.Contains("\"La salida principal se genero correctamente", source);
        Assert.Contains("\"LG-compatible copy was generated successfully", source);
        Assert.Contains("\"La copia compatible LG se genero correctamente", source);
        Assert.DoesNotContain("\"Selected preset\"", source);
    }

    [Fact]
    public void PreviewWorkflow_UsesEnglishAndSpanishLabels()
    {
        var source = ReadMainWindowViewModelSource();

        Assert.Contains("\"Preview\"", source);
        Assert.Contains("\"Vista previa\"", source);
        Assert.Contains("\"Preview required\"", source);
        Assert.Contains("\"Vista previa requerida\"", source);
        Assert.Contains("\"Generate preview\"", source);
        Assert.Contains("\"Generar vista previa\"", source);
        Assert.Contains("\"Generating preview\"", source);
        Assert.Contains("\"Generando vista previa\"", source);
        Assert.Contains("\"Preparing preview...\"", source);
        Assert.Contains("\"Preparando vista previa...\"", source);
        Assert.Contains("\"Cancel preview\"", source);
        Assert.Contains("\"Cancelar vista previa\"", source);
        Assert.Contains("\"Preview ready\"", source);
        Assert.Contains("\"Vista previa lista\"", source);
        Assert.Contains("\"Open externally\"", source);
        Assert.Contains("\"Abrir externamente\"", source);
        Assert.Contains("\"If embedded playback is unavailable, use Open externally.\"", source);
        Assert.Contains("\"Si la reproduccion integrada no esta disponible, usa Abrir externamente.\"", source);
        Assert.Contains("\"Preview engine: FFmpeg source clip + bundled Python/iw3\"", source);
        Assert.Contains("\"Motor de vista previa: clip fuente FFmpeg + Python/iw3 incluido\"", source);
        Assert.Contains("\"Running with: FFmpeg source clip + bundled Python/iw3\"", source);
        Assert.Contains("\"Ejecutando con: clip fuente FFmpeg + Python/iw3 incluido\"", source);
        Assert.Contains("\"FFmpeg source clip\"", source);
        Assert.Contains("\"Bundled Python/iw3\"", source);
        Assert.Contains("\"GPU metrics show global adapter activity, not guaranteed per-process attribution.\"", source);
        Assert.Contains("\"Las metricas de GPU muestran actividad global del adaptador, no atribucion garantizada por proceso.\"", source);
        Assert.Contains("\"Copy preview log\"", source);
        Assert.Contains("\"Copiar log de vista previa\"", source);
        Assert.Contains("\"iw3 attempted to download a runtime dependency. This bundle is not fully offline-ready.\"", ReadIw3RuntimeDownloadDetectorSource());
        Assert.Contains("\"iw3 intento descargar una dependencia en tiempo de ejecucion. Este bundle aun no esta completamente listo para uso offline.\"", ReadIw3RuntimeDownloadDetectorSource());
        Assert.Contains("\"Missing iw3 runtime dependency\"", source);
        Assert.Contains("\"Dependencia de runtime iw3 faltante\"", source);
        Assert.Contains("Iw3DefaultStereoRuntimeDependencyRelativePath", source);
        Assert.Contains("\"Preview timing: modal opened.\"", source);
        Assert.Contains("\"Preview timing: preview ready modal opened.\"", source);
        Assert.Contains("\"Preview timings: source clip", ReadLocalIw3PreviewExecutorSource());
        Assert.Contains("\"Tiempos de vista previa: clip fuente", ReadLocalIw3PreviewExecutorSource());
        Assert.Contains("\"Play\"", source);
        Assert.Contains("\"Reproducir\"", source);
        Assert.Contains("\"Pause\"", source);
        Assert.Contains("\"Pausar\"", source);
        Assert.Contains("\"Replay\"", source);
        Assert.Contains("\"Repetir\"", source);
        Assert.Contains("\"Volume\"", source);
        Assert.Contains("\"Volumen\"", source);
        Assert.Contains("\"Muted\"", source);
        Assert.Contains("\"Silenciado\"", source);
        Assert.Contains("\"Mute\"", source);
        Assert.Contains("\"Silenciar\"", source);
        Assert.Contains("\"Unmute\"", source);
        Assert.Contains("\"Activar sonido\"", source);
        Assert.Contains("\"Preview ended\"", source);
        Assert.Contains("\"Vista previa finalizada\"", source);
        Assert.Contains("\"Embedded playback unavailable\"", source);
        Assert.Contains("\"Reproduccion integrada no disponible\"", source);
        Assert.Contains("\"GPU global\"", source);
        Assert.Contains("\"VRAM global\"", source);
        Assert.Contains("\"Continue\"", source);
        Assert.Contains("\"Continuar\"", source);
        Assert.Contains("\"From\"", source);
        Assert.Contains("\"Desde\"", source);
        Assert.Contains("\"To\"", source);
        Assert.Contains("\"Hasta\"", source);
        Assert.Contains("\"Maximum preview duration is 1 minute 30 seconds\"", source);
        Assert.Contains("\"La duracion maxima de la vista previa es de 1 minuto 30 segundos\"", source);
        Assert.Contains("\"Preview accepted\"", source);
        Assert.Contains("\"Vista previa aceptada\"", source);
        Assert.Contains("\"Preview outdated\"", source);
        Assert.Contains("\"Vista previa desactualizada\"", source);
        Assert.Contains("\"Open preview\"", source);
        Assert.Contains("\"Abrir vista previa\"", source);
        Assert.Contains("\"Delete preview\"", source);
        Assert.Contains("\"Eliminar vista previa\"", source);
        Assert.Contains("\"Preview status\"", source);
        Assert.Contains("\"Estado de vista previa\"", source);
        Assert.Contains("\"Preview duration\"", source);
        Assert.Contains("\"Duracion de vista previa\"", source);
        Assert.Contains("\"Preview start time\"", source);
        Assert.Contains("\"Tiempo de inicio de vista previa\"", source);
        Assert.Contains("\"View log\"", source);
        Assert.Contains("\"Ver log\"", source);
        Assert.Contains("\"Copy full log\"", source);
        Assert.Contains("\"Copiar todo el log\"", source);
    }

    [Fact]
    public void PreviewWorkflow_DoesNotExposePreviewAllModelsOrComparisonTable()
    {
        var source = ReadMainWindowViewModelSource();

        Assert.DoesNotContain("Preview all", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Comparison table", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("!IsPreviewGenerating", source);
    }

    [Fact]
    public void ActivityLogModal_ExposesViewAndCopyCommands()
    {
        var source = ReadMainWindowViewModelSource();
        var copyFullLogMethod = ExtractSourceRange(
            source,
            "private void CopyFullLog",
            "private void CopyPreviewLog");
        var copyPreviewLogMethod = ExtractSourceRange(
            source,
            "private void CopyPreviewLog",
            "private void CopyLogToClipboard");
        var copyHelperMethod = ExtractSourceRange(
            source,
            "private void CopyLogToClipboard",
            "private void CloseActivityLog");

        Assert.Contains("public string ActivityLogPanelText", source);
        Assert.Contains("public RelayCommand ViewActivityLogCommand", source);
        Assert.Contains("public RelayCommand CopyFullLogCommand", source);
        Assert.Contains("public RelayCommand CopyPreviewLogCommand", source);
        Assert.Contains("public RelayCommand CloseActivityLogCommand", source);
        Assert.Contains("var logText = CreateFullActivityLogText();", copyFullLogMethod);
        Assert.Contains("ActivityLogModalText = logText;", copyFullLogMethod);
        Assert.Contains("PreviewGenerationLogText", copyPreviewLogMethod);
        Assert.Contains("System.Windows.Clipboard.SetText(logText);", copyHelperMethod);
        Assert.Contains("ShowLogCopySuccessNotification();", copyHelperMethod);
        Assert.Contains("ShowLogCopyFailureNotification();", copyHelperMethod);
        Assert.Contains("Could not copy {englishLogName} to clipboard", copyHelperMethod);
        Assert.Contains("No se pudo copiar el {spanishLogName} al portapapeles", copyHelperMethod);
        Assert.Contains("AppendPreviewLogLine(Text(englishMessage, spanishMessage));", copyHelperMethod);
    }

    [Fact]
    public void LogCopyFeedback_UsesLocalizedTransientNotificationState()
    {
        var source = ReadMainWindowViewModelSource();

        Assert.Contains("public string LogCopiedText => Text(\"Log copied\", \"Log copiado\")", source);
        Assert.Contains("\"Could not copy log\"", source);
        Assert.Contains("\"No se pudo copiar el log\"", source);
        Assert.Contains("public string LogCopyNotificationText", source);
        Assert.Contains("public Visibility LogCopyNotificationVisibility", source);
        Assert.Contains("private void ShowLogCopySuccessNotification()", source);
        Assert.Contains("private void ShowLogCopyFailureNotification()", source);
        Assert.Contains("private void ShowLogCopyNotification(string englishText, string spanishText)", source);
        Assert.Contains("Task.Delay(TimeSpan.FromSeconds(2)", source);
        Assert.Contains("_isLogCopyNotificationVisible = true;", source);
        Assert.Contains("_isLogCopyNotificationVisible = false;", source);
        Assert.Contains("OnPropertyChanged(nameof(LogCopyNotificationText));", source);
        Assert.Contains("OnPropertyChanged(nameof(LogCopyNotificationVisibility));", source);
    }

    [Fact]
    public void FinalConversionStart_UsesPreviewConversionGate()
    {
        var source = ReadMainWindowViewModelSource();

        Assert.Contains("PreviewConversionGate.Evaluate", source);
        Assert.Contains("EvaluateConversionStartGate().CanStart", source);
        Assert.Contains("ConversionExecutionBlocker.PreviewRequired", source);
    }

    [Fact]
    public void PrimaryActionVisibility_UsesAcceptedCurrentPreviewGate()
    {
        var source = ReadMainWindowViewModelSource();

        Assert.Contains("public Visibility PreviewRequirementVisibility", source);
        Assert.Contains("public Visibility GeneratePreviewPrimaryActionVisibility", source);
        Assert.Contains("public Visibility ConvertPrimaryActionVisibility", source);
        Assert.Contains("public Visibility CancelConversionPrimaryActionVisibility", source);
        Assert.Contains("IsCurrentPreviewAccepted && !IsConversionRunning", source);
        Assert.Contains("IsConversionRunning", source);
        Assert.Contains("private bool IsCurrentPreviewAccepted", source);
        Assert.Contains("PreviewConversionGate.Evaluate(_previewState, CreateCurrentPreviewConfiguration()).CanStart", source);
        Assert.Contains("PreviewOutputFileExists()", source);
    }

    [Fact]
    public void PreviewProgress_RoutesDetailedOutputToPreviewSpecificLogOnly()
    {
        var source = ReadMainWindowViewModelSource();
        var progressMethod = ExtractSourceRange(
            source,
            "private void ApplyPreviewProgressUpdate",
            "private void StartOrCancelConversion");
        var generateMethod = ExtractSourceRange(
            source,
            "private async Task GeneratePreviewAsync",
            "private void CancelPreview");

        Assert.Contains("PreviewGenerationLogs", source);
        Assert.Contains("AppendPreviewLogLine(message);", progressMethod);
        Assert.DoesNotContain("AddLog(message, message);", progressMethod);
        Assert.Contains("AppendPreviewResultLog(log);", generateMethod);
        Assert.DoesNotContain("AddLog(log.EnglishMessage, log.SpanishMessage);", generateMethod);
        Assert.Contains("AddLog(result.EnglishSummary, result.SpanishSummary);", generateMethod);
    }

    [Fact]
    public void PreviewStartup_OpensModalBeforeCleanupOrProcessPreparation()
    {
        var source = ReadMainWindowViewModelSource();
        var generateMethod = ExtractSourceRange(
            source,
            "private async Task GeneratePreviewAsync",
            "private void CancelPreview");

        var modalIndex = generateMethod.IndexOf(
            "IsPreviewGeneratingModalOpen = true;",
            StringComparison.Ordinal);
        var yieldIndex = generateMethod.IndexOf("await Task.Yield();", StringComparison.Ordinal);
        var cleanupIndex = generateMethod.IndexOf(
            "await DeletePreviewFilesAsync(cleanupPaths, logDeletion: false);",
            StringComparison.Ordinal);
        var executeIndex = generateMethod.IndexOf(
            "_previewExecutor.ExecuteAsync",
            StringComparison.Ordinal);

        Assert.True(modalIndex >= 0);
        Assert.True(yieldIndex > modalIndex);
        Assert.True(cleanupIndex > yieldIndex);
        Assert.True(executeIndex > cleanupIndex);
    }

    [Fact]
    public void PreviewRangeEditability_AllowsRequiredAndOutdatedAfterAnalysisOnly()
    {
        var source = ReadMainWindowViewModelSource();
        var hasCompletedAnalysisProperty = ExtractSourceRange(
            source,
            "public bool HasCompletedAnalysis",
            "public IReadOnlyList<string> LanguageOptions");
        var editabilityProperty = ExtractSourceRange(
            source,
            "public bool CanEditPreviewTimeRange",
            "public string PreviewOutdatedText");
        var generateProperty = ExtractSourceRange(
            source,
            "public bool CanGeneratePreview",
            "public bool CanCancelPreview");

        Assert.Contains("RaisePreviewPropertiesChanged();", hasCompletedAnalysisProperty);
        Assert.Contains("HasCompletedAnalysis", editabilityProperty);
        Assert.Contains("_analysis?.File.Duration is not null", editabilityProperty);
        Assert.Contains("!IsConversionRunning", editabilityProperty);
        Assert.Contains("!IsPreviewGenerating", editabilityProperty);
        Assert.Contains("!IsPreviewRangeEditingBlockedByModal", editabilityProperty);
        Assert.DoesNotContain("PreviewGenerationStatus", editabilityProperty);
        Assert.DoesNotContain("_previewState", editabilityProperty);
        Assert.Contains("CanEditPreviewTimeRange", generateProperty);
        Assert.Contains("_conversionReadiness?.CanConvert != true", source);
    }

    [Fact]
    public void GeneratePreview_CatchesUnexpectedExceptionsAndLogsRecoverableFailure()
    {
        var source = ReadMainWindowViewModelSource();
        var generateWrapper = ExtractSourceRange(
            source,
            "private async Task GeneratePreviewAsync",
            "private async Task GeneratePreviewCoreAsync");
        var generateCore = ExtractSourceRange(
            source,
            "private async Task GeneratePreviewCoreAsync",
            "private void CancelPreview");

        Assert.Contains("AppErrorLogService.BeginOperation(\"Generate preview\")", generateWrapper);
        Assert.Contains("catch (Exception exception)", generateWrapper);
        Assert.Contains("RecordUnexpectedPreviewFailure(exception);", generateWrapper);
        Assert.Contains("finally", generateWrapper);
        Assert.Contains("catch (Exception exception)", generateCore);
        Assert.Contains("RecordUnexpectedPreviewFailure(exception);", generateCore);
        Assert.Contains("PreviewGenerationStatus.Failed", generateCore);
        Assert.Contains("AppErrorLogService.LogRecoverableException", generateCore);
    }

    [Fact]
    public void CancelPreview_ResetsStateCleansPartialsAndReenablesPreviewAfterActiveTokenEnds()
    {
        var source = ReadMainWindowViewModelSource();
        var canGenerateProperty = ExtractSourceRange(
            source,
            "public bool CanGeneratePreview",
            "public bool CanCancelPreview");
        var cancelMethod = ExtractSourceRange(
            source,
            "private void CancelPreview",
            "private void OpenPreview");
        var recordCanceledMethod = ExtractSourceRange(
            source,
            "private void RecordPreviewCanceled",
            "private void RecordUnexpectedPreviewFailure");
        var generateCore = ExtractSourceRange(
            source,
            "private async Task GeneratePreviewCoreAsync",
            "private void RecordPreviewCanceled");
        var cleanupMethod = ExtractSourceRange(
            source,
            "private async Task DeleteCanceledPreviewAttemptFilesAsync",
            "private PreviewConfigurationSnapshot?");

        Assert.Contains("_previewCancellationTokenSource is null", canGenerateProperty);
        Assert.Contains("_isPreviewCancellationRequested = true;", cancelMethod);
        Assert.Contains("_previewCancellationTokenSource?.Cancel();", cancelMethod);
        Assert.Contains("IsPreviewGeneratingModalOpen = false;", cancelMethod);
        Assert.Contains("RecordPreviewCanceled();", cancelMethod);
        Assert.Contains("Status = PreviewGenerationStatus.Canceled", recordCanceledMethod);
        Assert.Contains("OutputPath = null", recordCanceledMethod);
        Assert.Contains("\"Preview generation was canceled.\"", recordCanceledMethod);
        Assert.Contains("\"La generacion de vista previa fue cancelada.\"", recordCanceledMethod);
        Assert.Contains("RaiseConversionRunningModePropertiesChanged();", recordCanceledMethod);
        Assert.Contains("result.WasCanceled", generateCore);
        Assert.Contains("_previewCancellationTokenSource?.IsCancellationRequested == true", generateCore);
        Assert.Contains("await DeleteCanceledPreviewAttemptFilesAsync(logDeletion: true);", generateCore);
        Assert.Contains("_previewCancellationTokenSource = null;", generateCore);
        Assert.Contains("_isPreviewCancellationRequested = false;", generateCore);
        Assert.Contains("_lastPreviewCachePaths?.AllPaths", cleanupMethod);
        Assert.Contains("DeletePreviewFilesAsync(paths, logDeletion: false)", cleanupMethod);
        Assert.Contains("_previewCacheCleaner.DeletePartialFiles", cleanupMethod);
        Assert.Contains("\"Preview partial files were cleaned.\"", cleanupMethod);
        Assert.Contains("\"Los archivos parciales de vista previa fueron limpiados.\"", cleanupMethod);
    }

    [Fact]
    public void PreviewProgressAfterCancellation_IsIgnored()
    {
        var source = ReadMainWindowViewModelSource();
        var progressMethod = ExtractSourceRange(
            source,
            "private void ApplyPreviewProgressUpdateOnUiThread",
            "private void StartOrCancelConversion");

        Assert.Contains("_isPreviewCancellationRequested", progressMethod);
        Assert.Contains("_previewState.Status == PreviewGenerationStatus.Canceled", progressMethod);
        Assert.Contains("return;", progressMethod);
    }

    [Fact]
    public void PreviewLogAndProgressUpdates_AreMarshaledToUiThread()
    {
        var source = ReadMainWindowViewModelSource();
        var progressMethod = ExtractSourceRange(
            source,
            "private void ApplyPreviewProgressUpdate",
            "private void ApplyPreviewProgressUpdateOnUiThread");
        var resetLogMethod = ExtractSourceRange(
            source,
            "private void ResetPreviewGenerationLog",
            "private void AppendPreviewLogLine");
        var appendLogMethod = ExtractSourceRange(
            source,
            "private void AppendPreviewLogLine",
            "private bool DispatchToUiThreadIfNeeded");
        var dispatchMethod = ExtractSourceRange(
            source,
            "private bool DispatchToUiThreadIfNeeded",
            "private void RecordRecoverablePreviewWarning");

        Assert.Contains("DispatchToUiThreadIfNeeded(() => ApplyPreviewProgressUpdate(progressUpdate))", progressMethod);
        Assert.Contains("DispatchToUiThreadIfNeeded(ResetPreviewGenerationLog)", resetLogMethod);
        Assert.Contains("DispatchToUiThreadIfNeeded(() => AppendPreviewLogLine(message))", appendLogMethod);
        Assert.Contains("_previewGenerationLogTextBuilder.Append(message);", appendLogMethod);
        Assert.DoesNotContain("OnPropertyChanged(nameof(PreviewGenerationLogText));", appendLogMethod);
        Assert.Contains("Application.Current?.Dispatcher", dispatchMethod);
        Assert.Contains("dispatcher.CheckAccess()", dispatchMethod);
        Assert.Contains("dispatcher.BeginInvoke", dispatchMethod);
        Assert.Contains("AppErrorLogService.LogRecoverableException(\"Preview UI dispatch\", exception)", dispatchMethod);
    }

    [Fact]
    public void RuntimeDownloadWarnings_AreSurfacedWithoutGeneralPreviewLogSpam()
    {
        var source = ReadMainWindowViewModelSource();
        var previewProgressMethod = ExtractSourceRange(
            source,
            "private void ApplyPreviewProgressUpdate",
            "private void StartOrCancelConversion");
        var conversionProgressMethod = ExtractSourceRange(
            source,
            "private void ApplyConversionProgressUpdate",
            "private void AddConversionOutputLine");

        Assert.Contains("AppendPreviewOfflineDependencyWarningIfNeeded(normalizedUpdate)", previewProgressMethod);
        Assert.Contains("AddConversionOfflineDependencyWarningIfNeeded(normalizedUpdate)", conversionProgressMethod);
        Assert.Contains("Iw3RuntimeDownloadDetector.EnglishWarning", source);
        Assert.Contains("AddLog(result.EnglishSummary, result.SpanishSummary);", source);
        Assert.DoesNotContain("AddLog(log.EnglishMessage, log.SpanishMessage);", previewProgressMethod);
    }

    [Fact]
    public void PreviewSourceClip_UsesFastCopyWithSafeReencodeFallback()
    {
        var source = ReadLocalIw3PreviewExecutorSource();

        Assert.Contains("CreateFastShortSourceClipRequest", source);
        Assert.Contains("CreateSafeReencodeShortSourceClipRequest", source);
        Assert.Contains("\"0:v:0\"", source);
        Assert.Contains("\"0:a:0?\"", source);
        Assert.Contains("\"-sn\"", source);
        Assert.Contains("\"-dn\"", source);
        Assert.Contains("\"-c\"", source);
        Assert.Contains("\"copy\"", source);
        Assert.Contains("\"-c:v\"", source);
        Assert.Contains("\"libx264\"", source);
        Assert.Contains("\"Fast preview source stream-copy failed; retrying with safe reencode.\"", source);
        Assert.DoesNotContain("\"-map\", \"0\"", source);
    }

    [Fact]
    public void VideoSelectionAndEngineRefresh_AvoidSynchronousUiThreadWork()
    {
        var source = ReadMainWindowViewModelSource();
        var refreshMethod = ExtractSourceRange(
            source,
            "private async Task RefreshEngineStatusAsync",
            "private void OpenEngineFolder");
        var setSelectedVideoMethod = ExtractSourceRange(
            source,
            "private void SetSelectedVideo",
            "private void ResetAnalysisState");

        Assert.Contains("RefreshEngineStatusCommand = new AsyncRelayCommand", source);
        Assert.Contains("_healthChecker.CheckDetailed(_toolPaths)", refreshMethod);
        Assert.Contains(".Run", refreshMethod);
        Assert.Contains("await Task.Yield();", source);
        Assert.Contains("_ = DeletePreviewFilesAsync(cleanupPaths, logDeletion: false);", setSelectedVideoMethod);
        Assert.DoesNotContain("DeleteCurrentPreviewFiles(logDeletion: false);", setSelectedVideoMethod);
    }

    [Fact]
    public void UserEditedPreviewRange_IsPreservedAcrossUnrelatedSettingChanges()
    {
        var source = ReadMainWindowViewModelSource();
        var setDefaultRangeMethod = ExtractSourceRange(
            source,
            "private void SetDefaultPreviewTimeRangeFromAnalysis",
            "private void SetPreviewTimeRangeText");
        var raisePreviewPropertiesMethod = ExtractSourceRange(
            source,
            "private void RaisePreviewPropertiesChanged",
            "private void RaiseConversionReadinessPropertiesChanged");

        Assert.Contains("_hasUserEditedPreviewRange", source);
        Assert.Contains("!_hasUserEditedPreviewRange || !currentValidation.IsValid", setDefaultRangeMethod);
        Assert.DoesNotContain("OnPropertyChanged(nameof(PreviewFromText))", raisePreviewPropertiesMethod);
        Assert.DoesNotContain("OnPropertyChanged(nameof(PreviewToText))", raisePreviewPropertiesMethod);
    }

    [Fact]
    public void OutputPathAndOpenAfterFinish_DoNotInvalidatePreviewOrResetRange()
    {
        var source = ReadMainWindowViewModelSource();
        var commitOutputPathMethod = ExtractSourceRange(
            source,
            "private void CommitOutputPath",
            "private void BrowseOutputFolder");
        var setCustomOutputPathMethod = ExtractSourceRange(
            source,
            "private void SetCustomOutputPath",
            "private string? GetAutomaticOutputPath");
        var resetOutputPathMethod = ExtractSourceRange(
            source,
            "private void ResetOutputPath",
            "private void SetCustomOutputPath");
        var openAfterFinishProperty = ExtractSourceRange(
            source,
            "public bool OpenOutputWhenFinished",
            "public bool CanChangeOpenOutputWhenFinished");

        Assert.DoesNotContain("MarkPreviewOutdated", commitOutputPathMethod);
        Assert.DoesNotContain("SetDefaultPreviewTimeRangeFromAnalysis", commitOutputPathMethod);
        Assert.DoesNotContain("MarkPreviewOutdated", setCustomOutputPathMethod);
        Assert.DoesNotContain("SetDefaultPreviewTimeRangeFromAnalysis", setCustomOutputPathMethod);
        Assert.DoesNotContain("MarkPreviewOutdated", resetOutputPathMethod);
        Assert.DoesNotContain("SetDefaultPreviewTimeRangeFromAnalysis", resetOutputPathMethod);
        Assert.DoesNotContain("MarkPreviewOutdated", openAfterFinishProperty);
        Assert.DoesNotContain("RegenerateConversionPlan", openAfterFinishProperty);
    }

    [Fact]
    public void PreviewFingerprint_CanRestoreAcceptedStateWhenSettingsMatchAgain()
    {
        var source = ReadMainWindowViewModelSource();

        Assert.Contains("UpdateForCurrentConfiguration", source);
        Assert.Contains("RestoreAcceptedIfCurrent", ReadPreviewWorkflowStateSource());
        Assert.Contains("Accepted preview matches the current settings again.", source);
        Assert.Contains("PreviewOutputFileExists()", source);
    }

    private static string ReadMainWindowViewModelSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "V3dfy.App",
                "ViewModels",
                "MainWindowViewModel.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate src/V3dfy.App/ViewModels/MainWindowViewModel.cs.");
    }

    private static string ReadPreviewWorkflowStateSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "V3dfy.Core",
                "Preview",
                "PreviewWorkflowState.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate src/V3dfy.Core/Preview/PreviewWorkflowState.cs.");
    }

    private static string ReadIw3RuntimeDownloadDetectorSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "V3dfy.Engine.Iw3",
                "Execution",
                "Iw3RuntimeDownloadDetector.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate src/V3dfy.Engine.Iw3/Execution/Iw3RuntimeDownloadDetector.cs.");
    }

    private static string ReadLocalIw3PreviewExecutorSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "V3dfy.Engine.Iw3",
                "Execution",
                "LocalIw3PreviewExecutor.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate src/V3dfy.Engine.Iw3/Execution/LocalIw3PreviewExecutor.cs.");
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
