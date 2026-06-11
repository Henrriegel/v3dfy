using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed class ModelPackImportExecutionService : IModelPackImportExecutionService
{
    private readonly IModelPackElevatedProcessStarter processStarter;
    private readonly ModelPackImportOrchestrator orchestrator;

    public ModelPackImportExecutionService(
        IModelPackElevatedProcessStarter? processStarter = null,
        ModelPackImportOrchestrator? orchestrator = null)
    {
        this.processStarter = processStarter ?? new ModelPackElevatedProcessStarter();
        this.orchestrator = orchestrator ?? new ModelPackImportOrchestrator();
    }

    public async Task<ModelPackImportExecutionResult> ExecuteAsync(
        ModelPackImportLaunchPreparationResult preparation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparation);

        var errors = new List<string>();
        var warnings = new List<string>(preparation.Preparation.Warnings);
        ModelPackElevatedProcessResult? processResult = null;
        ModelPackImportCompletionResult? completion = null;
        var resultPath = preparation.LaunchRequest?.ResultPath;
        var logPath = preparation.LaunchRequest?.LogPath;

        if (!preparation.Preparation.IsValid)
        {
            errors.Add("Model pack import preparation is invalid.");
            errors.AddRange(preparation.Preparation.ValidationErrors);
            return CreateResult(success: false);
        }

        if (preparation.LaunchRequest is null)
        {
            errors.Add("Model pack import launch request is missing.");
            return CreateResult(success: false);
        }

        if (preparation.StartInfo is null)
        {
            errors.Add("Model pack import process start information is missing.");
            return CreateResult(success: false);
        }

        if (string.IsNullOrWhiteSpace(resultPath))
        {
            errors.Add("Model pack helper result path is missing.");
            return CreateResult(success: false);
        }

        try
        {
            processResult = await processStarter.StartAndWaitAsync(
                preparation.StartInfo,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            errors.Add("Model pack helper execution was canceled.");
            return CreateResult(success: false);
        }
        catch (Exception exception)
        {
            errors.Add($"Model pack helper execution failed: {exception.Message}");
            return CreateResult(success: false);
        }

        if (!processResult.Started)
        {
            errors.Add(processResult.ErrorMessage ?? "Model pack helper process was not started.");
            return CreateResult(success: false);
        }

        if (!string.IsNullOrWhiteSpace(processResult.ErrorMessage))
        {
            errors.Add(processResult.ErrorMessage);
            return CreateResult(success: false);
        }

        if (processResult.ExitCode is null)
        {
            errors.Add("Model pack helper did not report an exit code.");
            return CreateResult(success: false);
        }

        try
        {
            completion = await orchestrator.CompleteAfterHelperResultAsync(
                resultPath,
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            errors.Add($"Could not read model pack helper result: {exception.Message}");
            return CreateResult(success: false);
        }

        warnings.AddRange(completion.HelperResult.Warnings);
        if (processResult.ExitCode != 0)
        {
            errors.Add($"Model pack helper exited with code {processResult.ExitCode}.");
        }

        if (!completion.HelperResult.Success)
        {
            errors.AddRange(completion.HelperResult.Errors);
        }

        return CreateResult(
            success: processResult.ExitCode == 0 &&
                completion.HelperResult.Success &&
                errors.Count == 0);

        ModelPackImportExecutionResult CreateResult(bool success) => new(
            Success: success,
            HelperProcessStarted: processResult?.Started ?? false,
            ExitCode: processResult?.ExitCode,
            HelperResult: completion?.HelperResult,
            RefreshNeeded: completion?.RefreshNeeded ?? false,
            RefreshCompleted: completion?.RefreshCompleted ?? false,
            Errors: errors,
            Warnings: warnings,
            ResultPath: resultPath,
            LogPath: logPath);
    }
}
