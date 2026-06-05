using V3dfy.Core.Planning;
using V3dfy.Core.Readiness;
using V3dfy.Engine.Iw3.Commands;

namespace V3dfy.Engine.Iw3.Execution;

public sealed class Iw3ConversionReadinessService
{
    private const string EnglishUnmappedModelStatus =
        "Conversion unavailable. Selected local model is not mapped to a verified iw3 depth model yet.";
    private const string SpanishUnmappedModelStatus =
        "Conversion no disponible. El modelo local seleccionado aun no esta mapeado a un modelo de profundidad iw3 verificado.";
    private const string EnglishUnmappedModelIssue =
        "Selected local model is not mapped to a verified iw3 depth model yet.";
    private const string SpanishUnmappedModelIssue =
        "El modelo local seleccionado aun no esta mapeado a un modelo de profundidad iw3 verificado.";

    public ConversionReadiness ApplyIw3ExecutionRequirements(
        ConversionReadiness readiness,
        LocalModelPlanSelection? selectedLocalModel)
    {
        ArgumentNullException.ThrowIfNull(readiness);

        if (!readiness.CanConvert ||
            Iw3DepthModelMapper.TryMap(selectedLocalModel, out _))
        {
            return readiness;
        }

        return readiness with
        {
            CanConvert = false,
            EnglishStatus = EnglishUnmappedModelStatus,
            SpanishStatus = SpanishUnmappedModelStatus,
            Issues =
            [
                .. readiness.Issues,
                new ConversionReadinessIssue(
                    EnglishUnmappedModelIssue,
                    SpanishUnmappedModelIssue),
            ],
        };
    }
}
