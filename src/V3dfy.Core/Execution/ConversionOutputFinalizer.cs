using System.IO;
using V3dfy.Core.Processes;

namespace V3dfy.Core.Execution;

public sealed class ConversionOutputFinalizer
{
    private const int DefaultPartialDeleteAttempts = 5;
    private const string PartialOutputFileNamePrefix = "_tmp_";
    private static readonly TimeSpan DefaultPartialDeleteRetryDelay = TimeSpan.FromMilliseconds(150);

    private readonly IConversionOutputFileService _fileService;
    private readonly int _partialDeleteAttempts;
    private readonly TimeSpan _partialDeleteRetryDelay;

    public ConversionOutputFinalizer(
        IConversionOutputFileService fileService,
        int partialDeleteAttempts = DefaultPartialDeleteAttempts,
        TimeSpan? partialDeleteRetryDelay = null)
    {
        _fileService = fileService;
        if (partialDeleteAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partialDeleteAttempts),
                "Partial cleanup attempts must be greater than zero.");
        }

        _partialDeleteAttempts = partialDeleteAttempts;
        _partialDeleteRetryDelay = partialDeleteRetryDelay ?? DefaultPartialDeleteRetryDelay;
        if (_partialDeleteRetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partialDeleteRetryDelay),
                "Partial cleanup retry delay cannot be negative.");
        }
    }

    public ConversionOutputPreparationResult PreparePartialOutput(
        string finalOutputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalOutputPath);

        var partialOutputPath = CreatePartialOutputPath(finalOutputPath);
        var logs = new List<ConversionExecutionLogEntry>();
        logs.AddRange(CleanStalePartialOutputs(finalOutputPath));

        try
        {
            if (_fileService.Exists(partialOutputPath))
            {
                _fileService.DeleteIfExists(partialOutputPath);
            }

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

    public IReadOnlyList<ConversionExecutionLogEntry> CleanStalePartialOutputs(
        string finalOutputPath,
        string? activePartialOutputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalOutputPath);

        var logs = new List<ConversionExecutionLogEntry>();
        var fullFinalOutputPath = Path.GetFullPath(finalOutputPath);
        var outputDirectory = Path.GetDirectoryName(fullFinalOutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return logs;
        }

        try
        {
            IReadOnlyList<string> activePartialCandidates = string.IsNullOrWhiteSpace(activePartialOutputPath)
                ? []
                : GetCurrentAttemptPartialCleanupCandidates(
                    fullFinalOutputPath,
                    activePartialOutputPath);
            foreach (var candidatePath in _fileService.EnumerateFiles(outputDirectory))
            {
                if (!IsStalePartialOutputPath(fullFinalOutputPath, candidatePath) ||
                    activePartialCandidates.Any(activeCandidate =>
                        IsSamePath(candidatePath, activeCandidate)))
                {
                    continue;
                }

                try
                {
                    _fileService.DeleteIfExists(candidatePath);
                    logs.Add(CreateLog(
                        "Stale conversion partial file was cleaned.",
                        "Se limpi\u00f3 un archivo parcial anterior de conversi\u00f3n."));
                }
                catch (Exception exception)
                {
                    logs.Add(CreateLog(
                        $"Could not delete stale partial file. {exception.Message}",
                        $"No se pudo eliminar un archivo parcial anterior. {exception.Message}"));
                }
            }
        }
        catch (Exception exception)
        {
            logs.Add(CreateLog(
                $"Could not delete stale partial file. {exception.Message}",
                $"No se pudo eliminar un archivo parcial anterior. {exception.Message}"));
        }

        return logs;
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
            return CleanupCurrentAttemptPartialOutputs(
                finalOutputPath,
                partialOutputPath,
                "Conversion partial file was cleaned.",
                "El archivo parcial de conversi\u00f3n fue limpiado.");
        }

        var processSucceeded = processResult.Status == ProcessExecutionStatus.Completed &&
            processResult.ExitCode == 0;
        if (!processSucceeded)
        {
            return CleanupCurrentAttemptPartialOutputs(
                finalOutputPath,
                partialOutputPath,
                "Conversion partial file was cleaned.",
                "El archivo parcial de conversi\u00f3n fue limpiado.");
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
            var logs = new List<ConversionExecutionLogEntry>
            {
                CreateLog(
                    $"Final output saved to {finalOutputPath}.",
                    $"Salida final guardada en {finalOutputPath}."),
            };
            logs.AddRange(CleanupCurrentAttemptPartialOutputLogs(
                finalOutputPath,
                partialOutputPath,
                "Conversion partial file was cleaned.",
                "El archivo parcial de conversi\u00f3n fue limpiado."));

            return new(
                Success: true,
                FinalizationFailure: false,
                Logs: logs);
        }
        catch (Exception exception)
        {
            var logs = new List<ConversionExecutionLogEntry>
            {
                CreateLog(
                    $"Final output promotion failed. {exception.Message}",
                    $"La promocion de la salida final fallo. {exception.Message}"),
            };
            logs.AddRange(CleanupCurrentAttemptPartialOutputLogs(
                finalOutputPath,
                partialOutputPath,
                "Conversion partial file was cleaned.",
                "El archivo parcial de conversi\u00f3n fue limpiado."));

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
        var partialFileName = CreatePartialFileName(
            fileName,
            extension,
            includeTempPrefix: true);

        return string.IsNullOrWhiteSpace(directory)
            ? partialFileName
            : Path.Combine(directory, partialFileName);
    }

    public static IReadOnlyList<string> GetCurrentAttemptPartialCleanupCandidates(
        string finalOutputPath,
        string trackedPartialOutputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(trackedPartialOutputPath);

        var fullFinalOutputPath = Path.GetFullPath(finalOutputPath);
        var outputDirectory = Path.GetDirectoryName(fullFinalOutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return [];
        }

        var candidates = new List<string>();
        var trackedFileName = Path.GetFileName(trackedPartialOutputPath);
        AddCurrentAttemptCandidate(
            candidates,
            fullFinalOutputPath,
            Path.Combine(outputDirectory, trackedFileName));
        AddCurrentAttemptCandidate(
            candidates,
            fullFinalOutputPath,
            Path.Combine(outputDirectory, $"{PartialOutputFileNamePrefix}{trackedFileName}"));
        AddCurrentAttemptCandidate(
            candidates,
            fullFinalOutputPath,
            Path.Combine(
                outputDirectory,
                CreatePartialFileName(
                    Path.GetFileNameWithoutExtension(fullFinalOutputPath),
                    Path.GetExtension(fullFinalOutputPath),
                    includeTempPrefix: false)));

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsStalePartialOutputPath(
        string finalOutputPath,
        string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(finalOutputPath) ||
            string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        var fullFinalOutputPath = Path.GetFullPath(finalOutputPath);
        var fullCandidatePath = Path.GetFullPath(candidatePath);
        var outputDirectory = Path.GetDirectoryName(fullFinalOutputPath);
        var candidateDirectory = Path.GetDirectoryName(fullCandidatePath);
        if (string.IsNullOrWhiteSpace(outputDirectory) ||
            !string.Equals(outputDirectory, candidateDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedPartialName = Path.GetFileName(CreatePartialOutputPath(fullFinalOutputPath));
        var candidateFileName = Path.GetFileName(fullCandidatePath);
        if (string.Equals(candidateFileName, expectedPartialName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var legacyPartialName = CreatePartialFileName(
            Path.GetFileNameWithoutExtension(fullFinalOutputPath),
            Path.GetExtension(fullFinalOutputPath),
            includeTempPrefix: false);
        if (string.Equals(candidateFileName, legacyPartialName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var finalBaseName = Path.GetFileNameWithoutExtension(fullFinalOutputPath);
        return candidateFileName.Contains("tmp", StringComparison.OrdinalIgnoreCase) &&
            ContainsFileNameToken(candidateFileName, finalBaseName) &&
            candidateFileName.Contains(".v3dfy-partial.", StringComparison.OrdinalIgnoreCase);
    }

    private ConversionOutputFinalizationResult CleanupCurrentAttemptPartialOutputs(
        string finalOutputPath,
        string partialOutputPath,
        string englishMessage,
        string spanishMessage)
    {
        return new(
            Success: false,
            FinalizationFailure: false,
            Logs: CleanupCurrentAttemptPartialOutputLogs(
                finalOutputPath,
                partialOutputPath,
                englishMessage,
                spanishMessage));
    }

    private IReadOnlyList<ConversionExecutionLogEntry> CleanupCurrentAttemptPartialOutputLogs(
        string finalOutputPath,
        string partialOutputPath,
        string englishMessage,
        string spanishMessage)
    {
        var logs = new List<ConversionExecutionLogEntry>();
        var cleanedAny = false;
        var failedAny = false;
        Exception? lastException = null;

        foreach (var candidatePath in GetCurrentAttemptPartialCleanupCandidates(
                     finalOutputPath,
                     partialOutputPath))
        {
            if (!_fileService.Exists(candidatePath))
            {
                continue;
            }

            if (TryDeletePartialOutput(candidatePath, out var exception))
            {
                cleanedAny = true;
                continue;
            }

            failedAny = true;
            lastException = exception;
        }

        if (failedAny)
        {
            logs.Add(CreateLog(
                $"Could not delete conversion partial file. {lastException?.Message}",
                $"No se pudo eliminar el archivo parcial de conversi\u00f3n. {lastException?.Message}"));
        }
        else if (cleanedAny)
        {
            logs.Add(CreateLog(englishMessage, spanishMessage));
        }

        return logs;
    }

    private bool TryDeletePartialOutput(string partialOutputPath, out Exception? lastException)
    {
        lastException = null;

        for (var attempt = 1; attempt <= _partialDeleteAttempts; attempt++)
        {
            try
            {
                if (!_fileService.Exists(partialOutputPath))
                {
                    return true;
                }

                _fileService.DeleteIfExists(partialOutputPath);
                if (!_fileService.Exists(partialOutputPath))
                {
                    return true;
                }

                lastException = new IOException("The partial file still exists after deletion.");
            }
            catch (Exception exception)
            {
                lastException = exception;
            }

            if (attempt < _partialDeleteAttempts &&
                _partialDeleteRetryDelay > TimeSpan.Zero)
            {
                Thread.Sleep(_partialDeleteRetryDelay);
            }
        }

        return !_fileService.Exists(partialOutputPath);
    }

    private static ConversionExecutionLogEntry CreateLog(
        string englishMessage,
        string spanishMessage) => new(
        DateTimeOffset.UtcNow,
        englishMessage,
        spanishMessage);

    private static bool IsSamePath(string path, string? otherPath) =>
        !string.IsNullOrWhiteSpace(otherPath) &&
        string.Equals(
            Path.GetFullPath(path),
            Path.GetFullPath(otherPath),
            StringComparison.OrdinalIgnoreCase);

    private static void AddCurrentAttemptCandidate(
        List<string> candidates,
        string fullFinalOutputPath,
        string candidatePath)
    {
        var fullCandidatePath = Path.GetFullPath(candidatePath);
        if (IsSamePath(fullCandidatePath, fullFinalOutputPath) ||
            !IsStalePartialOutputPath(fullFinalOutputPath, fullCandidatePath))
        {
            return;
        }

        candidates.Add(fullCandidatePath);
    }

    private static bool ContainsFileNameToken(string fileName, string token)
    {
        var index = fileName.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var beforeIsBoundary = index == 0 ||
                IsFileNameTokenBoundary(fileName[index - 1]);
            var afterIndex = index + token.Length;
            var afterIsBoundary = afterIndex >= fileName.Length ||
                IsFileNameTokenBoundary(fileName[afterIndex]);
            if (beforeIsBoundary && afterIsBoundary)
            {
                return true;
            }

            index = fileName.IndexOf(token, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsFileNameTokenBoundary(char value) =>
        value is '.' or '_' or '-' or ' ';

    private static string CreatePartialFileName(
        string fileName,
        string extension,
        bool includeTempPrefix)
    {
        var prefix = includeTempPrefix ? PartialOutputFileNamePrefix : string.Empty;
        return string.IsNullOrWhiteSpace(extension)
            ? $"{prefix}{fileName}.v3dfy-partial"
            : $"{prefix}{fileName}.v3dfy-partial{extension}";
    }
}
