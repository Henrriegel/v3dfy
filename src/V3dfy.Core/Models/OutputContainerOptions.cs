namespace V3dfy.Core.Models;

public static class OutputContainerOptions
{
    public static IReadOnlyList<OutputContainer> All { get; } =
    [
        OutputContainer.MP4,
        OutputContainer.MKV,
    ];
}
