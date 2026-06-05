namespace V3dfy.Engine.Iw3.Commands;

public static class Iw3CliContract
{
    public const string PythonModuleSwitch = "-m";
    public const string ModuleName = "iw3";
    public const string InputSwitch = "-i";
    public const string OutputSwitch = "-o";
    public const string DepthModelSwitch = "--depth-model";

    public static IReadOnlyList<string> ConfirmedBaseArgumentTemplate { get; } =
    [
        PythonModuleSwitch,
        ModuleName,
        InputSwitch,
        "<input>",
        OutputSwitch,
        "<output>",
    ];

    public static IReadOnlyList<string> UnconfirmedPlanningOptions { get; } =
    [
        "selected model",
        "3D layout",
        "video codec",
        "quality preset",
        "3D intensity/depth",
        "scene detection",
        "normalization",
        "convergence/divergence",
    ];

    public static IReadOnlyList<string> CreateConfirmedBaseArguments(
        string inputPath,
        string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        return
        [
            PythonModuleSwitch,
            ModuleName,
            InputSwitch,
            inputPath,
            OutputSwitch,
            outputPath,
        ];
    }
}
