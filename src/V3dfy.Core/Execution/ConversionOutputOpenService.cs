namespace V3dfy.Core.Execution;

public sealed class ConversionOutputOpenService
{
    private readonly IConversionOutputFileService _fileService;
    private readonly IOutputFileOpenService _openService;

    public ConversionOutputOpenService(
        IConversionOutputFileService fileService,
        IOutputFileOpenService openService)
    {
        _fileService = fileService;
        _openService = openService;
    }

    public ConversionOutputOpenResult OpenAfterSuccessfulConversion(
        ConversionExecutionResult result,
        string finalOutputPath,
        bool openOutputWhenFinished)
    {
        ArgumentNullException.ThrowIfNull(result);

        var outputPathToOpen = result.PreferredOpenOutputPath ?? finalOutputPath;

        if (!openOutputWhenFinished ||
            !result.Success ||
            result.WasCanceled ||
            string.IsNullOrWhiteSpace(outputPathToOpen))
        {
            return new(
                Attempted: false,
                Opened: false,
                EnglishWarning: null,
                SpanishWarning: null);
        }

        if (!_fileService.Exists(outputPathToOpen))
        {
            return new(
                Attempted: false,
                Opened: false,
                EnglishWarning:
                    "Open video skipped because the final output file was not found.",
                SpanishWarning:
                    "No se abrio el video porque no se encontro el archivo final.");
        }

        try
        {
            _openService.Open(outputPathToOpen);
            return new(
                Attempted: true,
                Opened: true,
                EnglishWarning: null,
                SpanishWarning: null);
        }
        catch (Exception exception)
        {
            return new(
                Attempted: true,
                Opened: false,
                EnglishWarning:
                    $"Conversion completed, but opening the video failed: {exception.Message}",
                SpanishWarning:
                    $"La conversion termino, pero no se pudo abrir el video: {exception.Message}");
        }
    }
}
