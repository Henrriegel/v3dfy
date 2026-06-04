namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionStartGateResult(
    bool CanStart,
    ConversionExecutionBlocker Blocker,
    string EnglishStatus,
    string SpanishStatus,
    string EnglishDetail,
    string SpanishDetail)
{
    public string EnglishLogMessage => BuildLogMessage(EnglishStatus, EnglishDetail);

    public string SpanishLogMessage => BuildLogMessage(SpanishStatus, SpanishDetail);

    public static ConversionExecutionStartGateResult Ready() => new(
        CanStart: true,
        Blocker: ConversionExecutionBlocker.None,
        EnglishStatus: "Conversion execution can start.",
        SpanishStatus: "La ejecuci\u00f3n de conversi\u00f3n puede iniciar.",
        EnglishDetail: string.Empty,
        SpanishDetail: string.Empty);

    public static ConversionExecutionStartGateResult Blocked(
        ConversionExecutionBlocker blocker,
        string englishStatus,
        string spanishStatus,
        string englishDetail,
        string spanishDetail)
    {
        if (blocker == ConversionExecutionBlocker.None)
        {
            throw new ArgumentException(
                "A blocked conversion start result requires a blocker.",
                nameof(blocker));
        }

        return new(
            CanStart: false,
            Blocker: blocker,
            EnglishStatus: englishStatus,
            SpanishStatus: spanishStatus,
            EnglishDetail: englishDetail,
            SpanishDetail: spanishDetail);
    }

    private static string BuildLogMessage(string status, string detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? status
            : $"{status} {detail}";
}
