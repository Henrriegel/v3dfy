using System.IO;
using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Processes;

namespace V3dfy.Engine.Iw3.Execution;

public sealed class LgCompatibilityCopyRequestBuilder
{
    public const string HalfSideBySideFilter =
        "scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1,format=yuv420p";

    public const string FullSideBySideToHalfSideBySideFilter =
        "scale=1920:ih,pad=1920:1080:0:(oh-ih)/2,setsar=1,format=yuv420p";
    public const string AudioStrategyEnglish =
        "Audio copied from the primary output to preserve dialogue and channel layout.";
    public const string AudioStrategySpanish =
        "El audio se copio desde la salida principal para preservar dialogos y distribucion de canales.";

    public LgCompatibilityCopyRequest Create(
        ConversionExecutionRequest request,
        string primaryOutputPath,
        string compatibilityPartialOutputPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(compatibilityPartialOutputPath);

        if (!request.Options.CreateLgCompatibilityCopy)
        {
            return LgCompatibilityCopyRequest.Skipped();
        }

        var finalOutputPath = CreateCompatibilityOutputPath(
            primaryOutputPath,
            request.ThreeDOutputFormat);
        if (request.ThreeDOutputFormat != ThreeDOutputFormat.HalfSideBySide)
        {
            return LgCompatibilityCopyRequest.UnsupportedLayout(
                finalOutputPath,
                new(
                    DateTimeOffset.UtcNow,
                    $"LG-compatible MP4 copy was skipped because {GetEnglishLayoutName(request.ThreeDOutputFormat)} post-processing has not been verified yet.",
                    $"La copia MP4 compatible con LG se omitio porque el postprocesamiento {GetSpanishLayoutName(request.ThreeDOutputFormat)} aun no esta verificado."));
        }

        var ffmpegRootDirectory = Path.GetDirectoryName(
            request.ExpectedToolPaths.FfmpegExecutable);
        var processRequest = new ProcessExecutionRequest(
            ExecutablePath: request.ExpectedToolPaths.FfmpegExecutable,
            Arguments:
            [
                "-y",
                "-i",
                primaryOutputPath,
                "-map",
                "0:v:0",
                "-map",
                "0:a?",
                "-vf",
                CreateVideoFilter(request.ThreeDOutputFormat),
                "-c:v",
                "libx264",
                "-pix_fmt",
                "yuv420p",
                "-movflags",
                "+faststart",
                "-c:a",
                "copy",
                compatibilityPartialOutputPath,
            ],
            WorkingDirectory: Path.GetDirectoryName(primaryOutputPath),
            AllowedRootDirectory: ffmpegRootDirectory);

        return LgCompatibilityCopyRequest.Ready(
            finalOutputPath,
            processRequest,
            new(
                DateTimeOffset.UtcNow,
                AudioStrategyEnglish,
                AudioStrategySpanish));
    }

    public static string CreateCompatibilityOutputPath(
        string primaryOutputPath,
        ThreeDOutputFormat outputFormat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryOutputPath);

        var directory = Path.GetDirectoryName(primaryOutputPath);
        var fileName = Path.GetFileNameWithoutExtension(primaryOutputPath);
        var outputName = $"{fileName}.lg3d.{GetSuffix(outputFormat)}.mp4";

        return string.IsNullOrWhiteSpace(directory)
            ? outputName
            : Path.Combine(directory, outputName);
    }

    public static string CreateVideoFilter(ThreeDOutputFormat sourceOutputFormat) =>
        sourceOutputFormat switch
        {
            ThreeDOutputFormat.HalfSideBySide => HalfSideBySideFilter,
            ThreeDOutputFormat.FullSideBySide =>
                FullSideBySideToHalfSideBySideFilter,
            ThreeDOutputFormat.HalfTopBottom => throw new NotSupportedException(
                "LG MP4 post-processing is currently verified only for Half Side-by-Side."),
            ThreeDOutputFormat.Anaglyph => throw new NotSupportedException(
                "LG MP4 post-processing is currently verified only for Half Side-by-Side."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(sourceOutputFormat),
                sourceOutputFormat,
                null),
        };

    private static string GetSuffix(ThreeDOutputFormat outputFormat) =>
        outputFormat switch
        {
            ThreeDOutputFormat.HalfSideBySide => "hsbs",
            ThreeDOutputFormat.HalfTopBottom => "htab",
            ThreeDOutputFormat.FullSideBySide => "sbs",
            ThreeDOutputFormat.Anaglyph => "anaglyph",
            _ => throw new ArgumentOutOfRangeException(
                nameof(outputFormat),
                outputFormat,
                null),
        };

    private static string GetEnglishLayoutName(ThreeDOutputFormat outputFormat) =>
        outputFormat switch
        {
            ThreeDOutputFormat.HalfTopBottom => "Half Top-Bottom",
            ThreeDOutputFormat.HalfSideBySide => "Half Side-by-Side",
            ThreeDOutputFormat.FullSideBySide => "Full Side-by-Side",
            ThreeDOutputFormat.Anaglyph => "Anaglyph",
            _ => outputFormat.ToString(),
        };

    private static string GetSpanishLayoutName(ThreeDOutputFormat outputFormat) =>
        outputFormat switch
        {
            ThreeDOutputFormat.HalfTopBottom => "Medio arriba-abajo",
            ThreeDOutputFormat.HalfSideBySide => "Medio lado a lado",
            ThreeDOutputFormat.FullSideBySide => "Completo lado a lado",
            ThreeDOutputFormat.Anaglyph => "Anaglifo",
            _ => outputFormat.ToString(),
        };
}
