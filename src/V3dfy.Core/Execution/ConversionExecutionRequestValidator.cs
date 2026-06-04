using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Core.Execution;

public sealed class ConversionExecutionRequestValidator
{
    public ConversionExecutionRequestValidationResult Validate(
        ConversionExecutionRequest? request)
    {
        var issues = new List<ConversionExecutionRequestValidationIssue>();
        if (request is null)
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.RequestMissing,
                nameof(request),
                "Conversion execution request is required.",
                "Se requiere una solicitud de ejecucion de conversion.");
            return CreateResult(
                issues,
                isDryRun: true,
                ConversionExecutionRequestModelState.NoSelectedLocalModel);
        }

        ValidateRequiredObjects(request, issues);
        ValidateRequestPaths(request, issues);
        ValidateToolPaths(request.ExpectedToolPaths, issues);
        ValidateCommandPreview(request.CommandPreview, issues);
        var modelState = ValidateSelectedModel(request.SelectedLocalModel, issues);

        return CreateResult(issues, IsDryRunRequest(request), modelState);
    }

    private static ConversionExecutionRequestValidationResult CreateResult(
        IReadOnlyList<ConversionExecutionRequestValidationIssue> issues,
        bool isDryRun,
        ConversionExecutionRequestModelState modelState) => new(
        Issues: issues,
        IsDryRun: isDryRun,
        CanStartLocalProcess: issues.Count == 0 && !isDryRun,
        ModelState: modelState);

    private static bool IsDryRunRequest(ConversionExecutionRequest request) =>
        request.IsDryRun ||
        request.PlanStatus == VideoConversionPlanStatus.DryRun ||
        request.DryRunReason != ConversionDryRunReason.None ||
        request.Plan?.IsDryRun == true;

    private static void ValidateRequiredObjects(
        ConversionExecutionRequest request,
        ICollection<ConversionExecutionRequestValidationIssue> issues)
    {
        if (request.Plan is null)
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.PlanMissing,
                nameof(request.Plan),
                "Conversion plan is required.",
                "Se requiere un plan de conversion.");
        }

        if (request.SelectedPreset is null)
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.SelectedPresetMissing,
                nameof(request.SelectedPreset),
                "Selected output preset is required.",
                "Se requiere un perfil de salida seleccionado.");
        }

        if (request.Options is null)
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.OptionsMissing,
                nameof(request.Options),
                "Conversion plan options are required.",
                "Se requieren las opciones del plan de conversion.");
        }

        if (request.ExpectedToolPaths is null)
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.ExpectedToolPathsMissing,
                nameof(request.ExpectedToolPaths),
                "Expected internal tool paths are required.",
                "Se requieren las rutas esperadas de herramientas internas.");
        }
    }

    private static void ValidateRequestPaths(
        ConversionExecutionRequest request,
        ICollection<ConversionExecutionRequestValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(request.SourcePath))
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.SourcePathMissing,
                nameof(request.SourcePath),
                "Source video path is required.",
                "Se requiere la ruta del video de origen.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.OutputPathMissing,
                nameof(request.OutputPath),
                "Output video path is required.",
                "Se requiere la ruta del video de salida.");
        }

        if (!string.IsNullOrWhiteSpace(request.SourcePath) &&
            !string.IsNullOrWhiteSpace(request.OutputPath) &&
            PathsMatch(request.SourcePath, request.OutputPath))
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.SourceAndOutputPathMatch,
                nameof(request.OutputPath),
                "Source and output paths must be different.",
                "Las rutas de origen y salida deben ser diferentes.");
        }
    }

    private static void ValidateToolPaths(
        InternalToolPaths? paths,
        ICollection<ConversionExecutionRequestValidationIssue> issues)
    {
        if (paths is null)
        {
            return;
        }

        ValidateExpectedToolPath(
            paths.FfmpegExecutable,
            nameof(paths.FfmpegExecutable),
            "FFmpeg executable",
            "ejecutable FFmpeg",
            issues);
        ValidateExpectedToolPath(
            paths.FfprobeExecutable,
            nameof(paths.FfprobeExecutable),
            "FFprobe executable",
            "ejecutable FFprobe",
            issues);
        ValidateExpectedToolPath(
            paths.PythonExecutable,
            nameof(paths.PythonExecutable),
            "embedded Python executable",
            "ejecutable de Python embebido",
            issues);
        ValidateExpectedToolPath(
            paths.Iw3EngineDirectory,
            nameof(paths.Iw3EngineDirectory),
            "iw3 engine directory",
            "carpeta del motor iw3",
            issues);
        ValidateExpectedToolPath(
            paths.ModelsDirectory,
            nameof(paths.ModelsDirectory),
            "models directory",
            "carpeta de modelos",
            issues);
    }

    private static void ValidateExpectedToolPath(
        string path,
        string fieldName,
        string englishName,
        string spanishName,
        ICollection<ConversionExecutionRequestValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.ExpectedToolPathMissing,
                fieldName,
                $"Expected {englishName} path is required.",
                $"Se requiere la ruta esperada de {spanishName}.");
            return;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.ExpectedToolPathNotAbsolute,
                fieldName,
                $"Expected {englishName} path must be absolute.",
                $"La ruta esperada de {spanishName} debe ser absoluta.");
        }
    }

    private static void ValidateCommandPreview(
        string commandPreview,
        ICollection<ConversionExecutionRequestValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(commandPreview))
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.CommandPreviewMissing,
                nameof(ConversionExecutionRequest.CommandPreview),
                "Future command preview text is required.",
                "Se requiere el texto de vista previa del comando futuro.");
        }
    }

    private static ConversionExecutionRequestModelState ValidateSelectedModel(
        LocalModelPlanSelection? selectedModel,
        ICollection<ConversionExecutionRequestValidationIssue> issues)
    {
        if (selectedModel is null)
        {
            return ConversionExecutionRequestModelState.NoSelectedLocalModel;
        }

        var modelIssueCount = issues.Count;
        if (string.IsNullOrWhiteSpace(selectedModel.DisplayName))
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.SelectedModelDisplayNameMissing,
                nameof(selectedModel.DisplayName),
                "Selected local model display name is required.",
                "Se requiere el nombre visible del modelo local seleccionado.");
        }

        if (string.IsNullOrWhiteSpace(selectedModel.RelativePath))
        {
            AddIssue(
                issues,
                ConversionExecutionRequestValidationIssueKind.SelectedModelRelativePathMissing,
                nameof(selectedModel.RelativePath),
                "Selected local model relative path is required.",
                "Se requiere la ruta relativa del modelo local seleccionado.");
        }
        else
        {
            if (Path.IsPathFullyQualified(selectedModel.RelativePath) ||
                Path.IsPathRooted(selectedModel.RelativePath))
            {
                AddIssue(
                    issues,
                    ConversionExecutionRequestValidationIssueKind.SelectedModelPathMustBeRelative,
                    nameof(selectedModel.RelativePath),
                    "Selected local model path must be relative to the models directory.",
                    "La ruta del modelo local seleccionado debe ser relativa a la carpeta de modelos.");
            }

            if (ContainsParentTraversal(selectedModel.RelativePath))
            {
                AddIssue(
                    issues,
                    ConversionExecutionRequestValidationIssueKind.SelectedModelPathContainsParentTraversal,
                    nameof(selectedModel.RelativePath),
                    "Selected local model path must not contain parent traversal.",
                    "La ruta del modelo local seleccionado no debe contener navegacion a carpetas superiores.");
            }
        }

        return issues.Count == modelIssueCount
            ? ConversionExecutionRequestModelState.SelectedLocalModel
            : ConversionExecutionRequestModelState.InvalidSelectedLocalModel;
    }

    private static bool PathsMatch(string firstPath, string secondPath)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(firstPath),
                Path.GetFullPath(secondPath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is NotSupportedException ||
            exception is PathTooLongException)
        {
            return string.Equals(
                firstPath.Trim(),
                secondPath.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool ContainsParentTraversal(string relativePath) =>
        relativePath
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\'],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment == "..");

    private static void AddIssue(
        ICollection<ConversionExecutionRequestValidationIssue> issues,
        ConversionExecutionRequestValidationIssueKind kind,
        string fieldName,
        string englishMessage,
        string spanishMessage) =>
        issues.Add(new(kind, fieldName, englishMessage, spanishMessage));
}
