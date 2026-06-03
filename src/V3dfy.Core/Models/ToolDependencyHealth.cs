namespace V3dfy.Core.Models;

public sealed record ToolDependencyHealth(
    ToolHealthStatus Status,
    ToolHealthDetailKind DetailKind,
    string ExpectedPath);
