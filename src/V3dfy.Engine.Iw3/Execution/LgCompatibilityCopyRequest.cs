using V3dfy.Core.Execution;
using V3dfy.Core.Processes;

namespace V3dfy.Engine.Iw3.Execution;

public sealed record LgCompatibilityCopyRequest(
    bool ShouldRun,
    bool Unsupported,
    string? FinalOutputPath,
    ProcessExecutionRequest? ProcessRequest,
    IReadOnlyList<ConversionExecutionLogEntry> Logs)
{
    public static LgCompatibilityCopyRequest Skipped() => new(
        ShouldRun: false,
        Unsupported: false,
        FinalOutputPath: null,
        ProcessRequest: null,
        Logs: []);

    public static LgCompatibilityCopyRequest UnsupportedLayout(
        string finalOutputPath,
        ConversionExecutionLogEntry log) => new(
        ShouldRun: false,
        Unsupported: true,
        FinalOutputPath: finalOutputPath,
        ProcessRequest: null,
        Logs: [log]);

    public static LgCompatibilityCopyRequest Ready(
        string finalOutputPath,
        ProcessExecutionRequest processRequest,
        ConversionExecutionLogEntry log) => new(
        ShouldRun: true,
        Unsupported: false,
        FinalOutputPath: finalOutputPath,
        ProcessRequest: processRequest,
        Logs: [log]);
}
