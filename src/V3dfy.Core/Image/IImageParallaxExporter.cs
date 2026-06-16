namespace V3dfy.Core.Image;

public interface IImageParallaxExporter
{
    Task<ImageParallaxExportResult> ExportAsync(
        ImageParallaxExportRequest request,
        IProgress<ImageParallaxExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
