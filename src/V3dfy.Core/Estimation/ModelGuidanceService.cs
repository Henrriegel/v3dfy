namespace V3dfy.Core.Estimation;

public sealed class ModelGuidanceService
{
    public ModelGuidance Create(
        string? modelKey,
        string? iw3DepthModelName,
        string? displayName,
        bool isEmbeddedBase)
    {
        var source = Normalize($"{modelKey} {iw3DepthModelName} {displayName}");

        if (source.Contains("any_v2_s") ||
            source.Contains("depth-anything-v2-small"))
        {
            return new(
                EnglishHeadline: "Recommended first optional model",
                SpanishHeadline: "Primer modelo opcional recomendado",
                EnglishBestFor: "movies, anime, mixed scenes, and quick tests",
                SpanishBestFor: "peliculas, anime, escenas mixtas y pruebas rapidas",
                EnglishSpeed: "Fast",
                SpanishSpeed: "Rapida",
                EnglishQuality: "Good",
                SpanishQuality: "Buena",
                EnglishSize: "Small",
                SpanishSize: "Pequeno",
                IsRecommendedFirstOptionalModel: true,
                IsBaseModel: false,
                IsExperimental: false);
        }

        if (source.Contains("3-mono") ||
            source.Contains("depth-anything-3"))
        {
            return new(
                EnglishHeadline: "Experimental large model",
                SpanishHeadline: "Modelo grande experimental",
                EnglishBestFor: "testing detailed scenes when speed matters less",
                SpanishBestFor: "probar escenas detalladas cuando la velocidad importa menos",
                EnglishSpeed: "Slow",
                SpanishSpeed: "Lenta",
                EnglishQuality: "Experimental",
                SpanishQuality: "Experimental",
                EnglishSize: "Large",
                SpanishSize: "Grande",
                IsRecommendedFirstOptionalModel: false,
                IsBaseModel: false,
                IsExperimental: true);
        }

        if (source.Contains("indoor") || source.Contains("hypersim") || source.Contains("any_n"))
        {
            return CreateMetricGuidance(
                isEmbeddedBase,
                "Indoor / room scenes",
                "interiores / habitaciones",
                "rooms, dialogue scenes, offices, and indoor animation",
                "habitaciones, dialogos, oficinas y animacion interior");
        }

        if (source.Contains("outdoor") || source.Contains("vkitti") || source.Contains("any_k"))
        {
            return CreateMetricGuidance(
                isEmbeddedBase,
                "Outdoor / road scenes",
                "exteriores / carreteras",
                "roads, landscapes, city streets, sea, and boats",
                "carreteras, paisajes, calles, mar y barcos");
        }

        if (source.Contains("large"))
        {
            return new(
                EnglishHeadline: "Large quality-focused model",
                SpanishHeadline: "Modelo grande enfocado en calidad",
                EnglishBestFor: "detailed movies and complex scenes",
                SpanishBestFor: "peliculas detalladas y escenas complejas",
                EnglishSpeed: "Slow",
                SpanishSpeed: "Lenta",
                EnglishQuality: "High",
                SpanishQuality: "Alta",
                EnglishSize: "Large",
                SpanishSize: "Grande",
                IsRecommendedFirstOptionalModel: false,
                IsBaseModel: isEmbeddedBase,
                IsExperimental: false);
        }

        if (source.Contains("base"))
        {
            return new(
                EnglishHeadline: isEmbeddedBase ? "Included base model" : "Balanced model",
                SpanishHeadline: isEmbeddedBase ? "Modelo base incluido" : "Modelo balanceado",
                EnglishBestFor: "general movies and mixed indoor/outdoor scenes",
                SpanishBestFor: "peliculas generales y escenas mixtas interiores/exteriores",
                EnglishSpeed: "Medium",
                SpanishSpeed: "Media",
                EnglishQuality: "Better",
                SpanishQuality: "Mejor",
                EnglishSize: "Base",
                SpanishSize: "Base",
                IsRecommendedFirstOptionalModel: false,
                IsBaseModel: isEmbeddedBase,
                IsExperimental: false);
        }

        return new(
            EnglishHeadline: isEmbeddedBase ? "Included base model" : "Small general model",
            SpanishHeadline: isEmbeddedBase ? "Modelo base incluido" : "Modelo general pequeno",
            EnglishBestFor: isEmbeddedBase
                ? "starting conversions without any optional model pack"
                : "general scenes, quick tests, and older machines",
            SpanishBestFor: isEmbeddedBase
                ? "iniciar conversiones sin paquetes de modelos opcionales"
                : "escenas generales, pruebas rapidas y equipos antiguos",
            EnglishSpeed: "Fast",
            SpanishSpeed: "Rapida",
            EnglishQuality: isEmbeddedBase ? "Usable" : "Good",
            SpanishQuality: isEmbeddedBase ? "Utilizable" : "Buena",
            EnglishSize: "Small",
            SpanishSize: "Pequeno",
            IsRecommendedFirstOptionalModel: false,
            IsBaseModel: isEmbeddedBase,
            IsExperimental: false);
    }

    private static ModelGuidance CreateMetricGuidance(
        bool isEmbeddedBase,
        string englishHeadline,
        string spanishHeadline,
        string englishBestFor,
        string spanishBestFor) => new(
        EnglishHeadline: isEmbeddedBase ? "Included base model" : englishHeadline,
        SpanishHeadline: isEmbeddedBase ? "Modelo base incluido" : spanishHeadline,
        EnglishBestFor: isEmbeddedBase
            ? "starting conversions without any optional model pack"
            : englishBestFor,
        SpanishBestFor: isEmbeddedBase
            ? "iniciar conversiones sin paquetes de modelos opcionales"
            : spanishBestFor,
        EnglishSpeed: "Medium",
        SpanishSpeed: "Media",
        EnglishQuality: isEmbeddedBase ? "Usable" : "Good",
        SpanishQuality: isEmbeddedBase ? "Utilizable" : "Buena",
        EnglishSize: isEmbeddedBase ? "Base" : "Small/Base",
        SpanishSize: isEmbeddedBase ? "Base" : "Pequeno/Base",
        IsRecommendedFirstOptionalModel: false,
        IsBaseModel: isEmbeddedBase,
        IsExperimental: false);

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant();
}
