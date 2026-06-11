using V3dfy.Infrastructure.ModelPacks;

namespace V3dfy.App.Services;

public sealed class ModelPackFilePicker(
    Func<bool>? useSpanish = null) : IModelPackFilePicker
{
    public Task<string?> PickModelPackZipAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<string?>(cancellationToken);
        }

        var spanish = useSpanish?.Invoke() == true;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = spanish ? "Selecciona un paquete de modelos" : "Select model pack",
            Filter = (spanish ? "Paquetes de modelos ZIP (*.zip)" : "Model pack ZIP files (*.zip)") +
                "|*.zip|" +
                (spanish ? "Todos los archivos (*.*)" : "All files (*.*)") +
                "|*.*",
            Multiselect = false,
            CheckFileExists = true,
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }
}
