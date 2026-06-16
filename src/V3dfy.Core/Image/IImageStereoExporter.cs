namespace V3dfy.Core.Image;

public interface IImageStereoExporter
{
    Task<ImageStereoExportResult> ExportAsync(
        ImageStereoExportRequest request,
        IProgress<ImageStereoExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
