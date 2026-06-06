using System.IO;
using V3dfy.Core.Processes;

namespace V3dfy.Core.Execution;

public sealed class ConversionOutputFinalizer
{
    private readonly IConversionOutputFileService _fileService;

    public ConversionOutputFinalizer(IConversionOutputFileService fileService)
    {
        _fileService = fileService;
    }

    public ConversionOutputPreparationResult PreparePartialOutput(
        string finalOutputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalOutputPath);

        var partialOutputPath = CreatePartialOutputPath(finalOutputPath);
        var logs = new List<ConversionExecutionLogEntry>();

        try
        {
            _fileService.DeleteIfExists(partialOutputPath);
            logs.Add(CreateLog(
                $"Partial output path prepared: {partialOutputPath}",
                $"Ruta de salida parcial preparada: {partialOutputPath}"));

            return new(
                Success: true,
                FinalOutputPath: finalOutputPath,
                PartialOutputPath: partialOutputPath,
                Logs: logs);
        }
        catch (Exception exception)
        {
            logs.Add(CreateLog(
                $"Could not prepare partial output. No local process was started. {exception.Message}",
                $"No se pudo preparar el archivo parcial. No se inicio ningun proceso local. {exception.Message}"));

            return new(
                Success: false,
                FinalOutputPath: finalOutputPath,
                PartialOutputPath: partialOutputPath,
                Logs: logs);
        }
    }

    public ConversionOutputFinalizationResult FinalizeAfterProcess(
        ProcessExecutionResult processResult,
        string finalOutputPath,
        string partialOutputPath)
    {
        ArgumentNullException.ThrowIfNull(processResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(partialOutputPath);

        if (processResult.Status == ProcessExecutionStatus.Canceled)
        {
            return CleanupPartialOutput(
                partialOutputPath,
                "Conversion canceled. Partial output was deleted.",
                "Conversion cancelada. El archivo parcial fue eliminado.");
        }

        var processSucceeded = processResult.Status == ProcessExecutionStatus.Completed &&
            processResult.ExitCode == 0;
        if (!processSucceeded)
        {
            return CleanupPartialOutput(
                partialOutputPath,
                "Conversion failed. Partial output was deleted.",
                "La conversion fallo. El archivo parcial fue eliminado.");
        }

        if (!_fileService.Exists(partialOutputPath))
        {
            return new(
                Success: false,
                FinalizationFailure: true,
                Logs:
                [
                    CreateLog(
                        "Conversion process completed, but partial output was not found. Final output was not replaced.",
                        "El proceso de conversion termino, pero no se encontro el archivo parcial. La salida final no fue reemplazada."),
                ]);
        }

        try
        {
            _fileService.Move(partialOutputPath, finalOutputPath, overwrite: true);
            return new(
                Success: true,
                FinalizationFailure: false,
                Logs:
                [
                    CreateLog(
                        $"Final output saved to {finalOutputPath}.",
                        $"Salida final guardada en {finalOutputPath}."),
                ]);
        }
        catch (Exception exception)
        {
            var logs = new List<ConversionExecutionLogEntry>
            {
                CreateLog(
                    $"Final output promotion failed. {exception.Message}",
                    $"La promocion de la salida final fallo. {exception.Message}"),
            };
            logs.AddRange(CleanupPartialOutput(
                partialOutputPath,
                "Conversion failed. Partial output was deleted.",
                "La conversion fallo. El archivo parcial fue eliminado.").Logs);

            return new(
                Success: false,
                FinalizationFailure: true,
                Logs: logs);
        }
    }

    public static string CreatePartialOutputPath(string finalOutputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalOutputPath);

        var directory = Path.GetDirectoryName(finalOutputPath);
        var fileName = Path.GetFileNameWithoutExtension(finalOutputPath);
        var extension = Path.GetExtension(finalOutputPath);
        var partialFileName = string.IsNullOrWhiteSpace(extension)
            ? $"{fileName}.v3dfy-partial"
            : $"{fileName}.v3dfy-partial{extension}";

        return string.IsNullOrWhiteSpace(directory)
            ? partialFileName
            : Path.Combine(directory, partialFileName);
    }

    private ConversionOutputFinalizationResult CleanupPartialOutput(
        string partialOutputPath,
        string englishMessage,
        string spanishMessage)
    {
        var logs = new List<ConversionExecutionLogEntry>();

        try
        {
            _fileService.DeleteIfExists(partialOutputPath);
            logs.Add(CreateLog(englishMessage, spanishMessage));
        }
        catch (Exception exception)
        {
            logs.Add(CreateLog(
                $"{englishMessage} Cleanup warning: {exception.Message}",
                $"{spanishMessage} Advertencia de limpieza: {exception.Message}"));
        }

        return new(
            Success: false,
            FinalizationFailure: false,
            Logs: logs);
    }

    private static ConversionExecutionLogEntry CreateLog(
        string englishMessage,
        string spanishMessage) => new(
        DateTimeOffset.UtcNow,
        englishMessage,
        spanishMessage);
}
