namespace V3dfy.Engine.Iw3.Commands;

public static class Iw3CliContract
{
    public const string PythonModuleSwitch = "-m";
    public const string ModuleName = "iw3";
    public const string InputSwitch = "-i";
    public const string InputLongSwitch = "--input";
    public const string OutputSwitch = "-o";
    public const string OutputLongSwitch = "--output";
    public const string DepthModelSwitch = "--depth-model";
    public const string DivergenceSwitch = "--divergence";
    public const string ConvergenceSwitch = "--convergence";
    public const string HalfSideBySideSwitch = "--half-sbs";
    public const string HalfTopBottomSwitch = "--half-tb";
    public const string TopBottomSwitch = "--tb";
    public const string AnaglyphSwitch = "--anaglyph";
    public const string CrossEyedSwitch = "--cross-eyed";

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
