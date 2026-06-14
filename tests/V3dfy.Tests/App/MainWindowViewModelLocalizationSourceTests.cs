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
    public void UiOnlyLanguageAndThemeRefreshes_SuppressWorkflowSelectionSideEffects()
    {
        var source = ReadMainWindowViewModelSource();
        var languageProperty = ExtractSourceRange(
            source,
            "public string SelectedLanguage",
            "public IReadOnlyList<string> ThemeOptions");
        var themeProperty = ExtractSourceRange(
            source,
            "public string SelectedTheme",
            "public string SubtitleText");
        var localModelProperty = ExtractSourceRange(
            source,
            "public LocalModelSelectionCandidate? SelectedLocalModelCandidate",
            "public string LocalModelSelectionStatusText");
        var resetMethod = ExtractSourceRange(
            source,
            "private void ResetConversionExecutionState",
            "private static string QualityPresetText");

        Assert.Contains("ApplyUiOnlyRefresh", languageProperty);
        Assert.Contains("UpdateLocalModelSelectionCandidates(regenerateCurrentPlan: false)", languageProperty);
        Assert.Contains("ApplyUiOnlyRefresh(() => _themeService.Apply(value))", themeProperty);
        Assert.Contains("_isApplyingUiOnlyRefresh && value is null", localModelProperty);
        Assert.Contains("regeneratePlan: !_isApplyingUiOnlyRefresh", localModelProperty);
        Assert.Contains("if (IsConversionRunning)", resetMethod);
        Assert.Contains("return;", resetMethod);
    }

    [Fact]
    public void ActiveWorkflowState_TakesPrecedenceOverIdlePreviewGateInSystemStatus()
    {
        var source = ReadMainWindowViewModelSource();
        var previewRequirementProperty = ExtractSourceRange(
            source,
            "public Visibility PreviewRequirementVisibility",
            "public string CancelPreviewText");
        var readinessStatusProperty = ExtractSourceRange(
            source,
            "public string ConversionReadinessStatusText",
            "public string ConversionReadinessIssuesText");
        var missingComponentsProperty = ExtractSourceRange(
            source,
            "public string ConversionReadinessMissingComponentsSummaryText",
            "public string ConversionReadinessRequiredComponentsText");
        var missingVisibilityProperty = ExtractSourceRange(
            source,
            "public Visibility ConversionMissingRequirementsVisibility",
            "public string ConversionReadinessStatusText");
        var readySummaryProperty = ExtractSourceRange(
            source,
            "public Visibility ConversionReadySummaryVisibility",
            "public Visibility CancelConversionPrimaryActionVisibility");
        var runningStatusProperty = ExtractSourceRange(
            source,
            "public Visibility ConversionRunningStatusVisibility",
            "public double ConversionProgressBarValue");
        var blockedReasonProperty = ExtractSourceRange(
            source,
            "public string ConversionBlockedReasonText",
            "public bool CanStartConversion");

        Assert.Contains("IsConversionRunning || IsPreviewGenerating || IsCurrentPreviewAccepted", previewRequirementProperty);
        Assert.Contains("!IsConversionRunning && !IsPreviewGenerating && !IsCurrentPreviewAccepted", previewRequirementProperty);
        Assert.Contains("IsCurrentPreviewAccepted && !IsConversionRunning && !IsPreviewGenerating", previewRequirementProperty);
        Assert.Contains("CanStartConversion ? Visibility.Visible : Visibility.Collapsed", readySummaryProperty);
        Assert.Contains("IsConversionRunning ? Visibility.Visible : Visibility.Collapsed", runningStatusProperty);
        Assert.Contains("ShouldShowConversionMissingRequirements()", missingVisibilityProperty);
        Assert.Contains("startGate.Blocker != ConversionExecutionBlocker.PreviewRequired", source);
        Assert.Contains("if (IsConversionRunning)", readinessStatusProperty);
        Assert.Contains("Text(\"Converting\", \"Convirtiendo\")", readinessStatusProperty);
        Assert.Contains("if (IsPreviewGenerating)", readinessStatusProperty);
        Assert.Contains("IsConversionRunning || IsPreviewGenerating", missingComponentsProperty);
        Assert.Contains("ConversionExecutionDetailText", blockedReasonProperty);
        Assert.Contains("PreviewModalDetailText", blockedReasonProperty);
    }

    [Fact]
    public void PreviewStart_CleansStalePreviewPartialsBeforeMarkingAttemptActive()
    {
        var source = ReadMainWindowViewModelSource();
        var generateCore = ExtractSourceRange(
            source,
            "private async Task GeneratePreviewCoreAsync",
            "private void RecordPreviewCanceled");
        var cleanupMethod = ExtractSourceRange(
            source,
            "private async Task CleanStalePreviewPartialFilesBeforeStartAsync",
            "private async Task GeneratePreviewAsync");

        var staleCleanupIndex = generateCore.IndexOf(
            "await CleanStalePreviewPartialFilesBeforeStartAsync();",
            StringComparison.Ordinal);
        var generatingStateIndex = generateCore.IndexOf(
            "_previewState = _previewState.Generating(configuration, startedAt);",
            StringComparison.Ordinal);

        Assert.True(staleCleanupIndex >= 0);
        Assert.True(generatingStateIndex > staleCleanupIndex);
        Assert.Contains("_previewCacheCleaner.DeletePartialFiles", cleanupMethod);
        Assert.Contains("\"Stale preview partial file was cleaned.\"", cleanupMethod);
        Assert.Contains("\"Se limpi", cleanupMethod);
        Assert.Contains("\"Could not delete stale partial file.\"", cleanupMethod);
        Assert.Contains("\"No se pudo eliminar un archivo parcial anterior.\"", cleanupMethod);
    }

    [Fact]
    public void ConversionResultActivityLog_SurfacesConciseStalePartialCleanupMessages()
    {
        var source = ReadMainWindowViewModelSource();
        var activityMethod = ExtractSourceRange(
            source,
            "private void AddConversionResultActivityLogs",
            "private IEnumerable<ConversionExecutionLogEntry> GetConversionResultLogsForLivePanel");

        Assert.Contains("AddCurrentAttemptConversionPartialCleanupActivityLogs(result);", activityMethod);
        Assert.Contains("\"Conversion partial file was cleaned.\"", activityMethod);
        Assert.Contains("\"El archivo parcial de conversi", activityMethod);
        Assert.Contains("\"Could not delete conversion partial file.\"", activityMethod);
        Assert.Contains("\"No se pudo eliminar el archivo parcial de conversi", activityMethod);
        Assert.Contains("\"Stale conversion partial file was cleaned.\"", activityMethod);
        Assert.Contains("\"Se limpi", activityMethod);
        Assert.Contains("\"Could not delete stale partial file.\"", activityMethod);
        Assert.Contains("\"No se pudo eliminar un archivo parcial anterior.\"", activityMethod);
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
    public void FinalConversionSuccess_ShowsCompletionModalInsteadOfOpeningImmediately()
    {
        var source = ReadMainWindowViewModelSource();
        var startMethod = ExtractSourceRange(
            source,
            "private async Task StartConversionAsync",
            "private void BlockConversionStart");

        Assert.Contains("if (result.Success && !result.WasCanceled)", startMethod);
        Assert.Contains("ShowConversionCompletedModal(result, request.OutputPath);", startMethod);
        Assert.DoesNotContain("HandleOpenOutputWhenFinished(result, request.OutputPath);", startMethod);
        Assert.DoesNotContain("MessageBox", source);
    }

    [Fact]
    public void ConversionProgress_UpdatesTimingEstimatesFromNormalizedProgress()
    {
        var source = ReadMainWindowViewModelSource();
        var progressMethod = ExtractSourceRange(
            source,
            "private void ApplyConversionProgressUpdate",
            "private void AddConversionOutputLine");
        var timingMethod = ExtractSourceRange(
            source,
            "private int UpdateConversionTimingEstimate",
            "private void AddConversionOutputLine");
        var startMethod = ExtractSourceRange(
            source,
            "private async Task StartConversionAsync",
            "private void BlockConversionStart");
        var resetMethod = ExtractSourceRange(
            source,
            "private void ResetWorkflowAfterSuccessfulConversion",
            "private static ConversionExecutionState CreateFinishedConversionState");

        Assert.Contains("var progressPercent = UpdateConversionTimingEstimate(normalizedUpdate);", progressMethod);
        Assert.Contains("ProgressPercent = progressPercent", progressMethod);
        Assert.Contains("ConversionProgressTimingEstimator.Estimate", timingMethod);
        Assert.Contains("progressUpdate.OutputLine?.Text", timingMethod);
        Assert.Contains("estimate?.ProgressPercent", timingMethod);
        Assert.Contains("_conversionTimingEstimate = _conversionTimingSmoother.Smooth(estimate);", timingMethod);
        Assert.Contains("private readonly ConversionProgressTimingSmoother _conversionTimingSmoother = new();", source);
        Assert.Contains("private void ResetConversionTimingEstimate()", source);
        Assert.Contains("ResetConversionTimingEstimate();", startMethod);
        Assert.Contains("ResetConversionTimingEstimate();", resetMethod);
    }

    [Fact]
    public void PreviewProgress_ExposesDeterminateAndIndeterminateState()
    {
        var source = ReadMainWindowViewModelSource();
        var previewProgressMethod = ExtractSourceRange(
            source,
            "private void ApplyPreviewProgressUpdateOnUiThread",
            "private void StartOrCancelConversion");

        Assert.Contains("public int PreviewProgressPercent", source);
        Assert.Contains("public string PreviewProgressText", source);
        Assert.Contains("public bool PreviewProgressIsIndeterminate", source);
        Assert.Contains("_previewProgressPercent = PreviewProgressResolver.Resolve(", previewProgressMethod);
        Assert.DoesNotContain("MapPreviewProgressToGlobal", source);
        Assert.DoesNotContain("ScalePreviewStageProgress", source);
        Assert.Contains("_previewProgressPercent = 100;", source);
        Assert.Contains("_previewProgressPercent = 0;", source);
        Assert.Contains("OnPropertyChanged(nameof(PreviewProgressPercent));", source);
        Assert.Contains("OnPropertyChanged(nameof(PreviewProgressText));", source);
        Assert.Contains("OnPropertyChanged(nameof(PreviewProgressIsIndeterminate));", source);
    }

    [Fact]
    public void ConversionCompletedAccept_OpensOutputThenResetsWorkflow()
    {
        var source = ReadMainWindowViewModelSource();
        var acceptMethod = ExtractSourceRange(
            source,
            "private void AcceptConversionCompleted",
            "private void ResetWorkflowAfterSuccessfulConversion");

        Assert.Contains("public RelayCommand AcceptConversionCompletedCommand", source);
        Assert.Contains("HandleOpenOutputWhenFinished(result, finalOutputPath);", acceptMethod);
        Assert.Contains("ResetWorkflowAfterSuccessfulConversion();", acceptMethod);
        Assert.Contains("IsConversionCompletedModalOpen = false;", acceptMethod);
        Assert.Contains("_completedConversionResult = null;", acceptMethod);
    }

    [Fact]
    public void ResetWorkflowAfterSuccessfulConversion_ClearsOnlyVideoSpecificState()
    {
        var source = ReadMainWindowViewModelSource();
        var resetMethod = ExtractSourceRange(
            source,
            "private void ResetWorkflowAfterSuccessfulConversion",
            "private static ConversionExecutionState CreateFinishedConversionState");

        Assert.Contains("SelectedVideoPath = null;", resetMethod);
        Assert.Contains("HasCompletedAnalysis = false;", resetMethod);
        Assert.Contains("_analysis = null;", resetMethod);
        Assert.Contains("_conversionRecommendation = null;", resetMethod);
        Assert.Contains("_conversionPlan = null;", resetMethod);
        Assert.Contains("_conversionReadiness = null;", resetMethod);
        Assert.Contains("_outputPathState.ClearCustomOutputPath();", resetMethod);
        Assert.Contains("SetOutputPathText(string.Empty);", resetMethod);
        Assert.Contains("PreviewWorkflowState.NotGenerated", resetMethod);
        Assert.Contains("_conversionExecutionState = ConversionExecutionState.NotStarted();", resetMethod);
        Assert.Contains("UpdateConversionReadiness();", resetMethod);
        Assert.DoesNotContain("Logs.Clear", resetMethod);
        Assert.DoesNotContain("ToolStatuses.Clear", resetMethod);
        Assert.DoesNotContain("LocalModelCandidates.Clear", resetMethod);
        Assert.DoesNotContain("DeletePreview", resetMethod);
    }

    [Fact]
    public void ConversionCompletedModal_IsPartOfActiveModalState()
    {
        var source = ReadMainWindowViewModelSource();

        Assert.Contains("public bool IsConversionCompletedModalOpen", source);
        Assert.Contains("public Visibility ConversionCompletedModalContentVisibility", source);
        Assert.Contains("if (IsConversionCompletedModalOpen)", source);
        Assert.Contains("return ConversionCompletedTitleText;", source);
        Assert.Contains("IsConversionCompletedModalOpen ||", source);
        Assert.Contains("OnPropertyChanged(nameof(ConversionCompletedModalContentVisibility));", source);
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
