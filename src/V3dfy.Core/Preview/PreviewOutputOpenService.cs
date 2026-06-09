using V3dfy.Core.Execution;

namespace V3dfy.Core.Preview;

public sealed class PreviewOutputOpenService
{
    private readonly IConversionOutputFileService _fileService;
    private readonly IOutputFileOpenService _openService;

    public PreviewOutputOpenService(
        IConversionOutputFileService fileService,
        IOutputFileOpenService openService)
    {
        _fileService = fileService;
        _openService = openService;
    }

    public PreviewOutputOpenResult OpenCurrentPreview(PreviewWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status != PreviewGenerationStatus.Ready ||
            string.IsNullOrWhiteSpace(state.OutputPath))
        {
            return new(
                Opened: false,
                EnglishWarning: "Open preview skipped because no current preview is ready.",
                SpanishWarning: "Abrir vista previa se omitio porque no hay una vista previa actual lista.");
        }

        if (!_fileService.Exists(state.OutputPath))
        {
            return new(
                Opened: false,
                EnglishWarning: "Open preview skipped because the preview file was not found.",
                SpanishWarning: "Abrir vista previa se omitio porque no se encontro el archivo de vista previa.");
        }

        try
        {
            _openService.Open(state.OutputPath);
            return new(Opened: true, EnglishWarning: null, SpanishWarning: null);
        }
        catch (Exception exception)
        {
            return new(
                Opened: false,
                EnglishWarning: $"Open preview failed. {exception.Message}",
                SpanishWarning: $"No se pudo abrir la vista previa. {exception.Message}");
        }
    }
}
