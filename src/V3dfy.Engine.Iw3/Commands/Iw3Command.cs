namespace V3dfy.Engine.Iw3.Commands;

public sealed record Iw3Command(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string FullCommandPreview,
    bool DryRun);
