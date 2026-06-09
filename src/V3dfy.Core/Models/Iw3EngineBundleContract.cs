namespace V3dfy.Core.Models;

public static class Iw3EngineBundleContract
{
    public const string EngineDirectoryRelativePath = "engine/iw3";
    public const string ManifestRelativePath = "engine/iw3/ENGINE_MANIFEST.json";
    public const string PythonExecutableRelativePath = "engine/iw3/python/python.exe";
    public const string PythonPathFileRelativePath = "engine/iw3/python/python312._pth";
    public const string NunifDirectoryName = "nunif";
    public const string Iw3PackageDirectoryName = "iw3";
    public const string PretrainedModelsDirectoryName = "pretrained_models";
    public const string NunifRootDirectoryRelativePath = "engine/iw3/nunif";
    public const string Iw3PackageDirectoryRelativePath = "engine/iw3/nunif/iw3";
    public const string Iw3PackageMainRelativePath = "engine/iw3/nunif/iw3/__main__.py";
    public const string ModelsDirectoryRelativePath = "engine/iw3/nunif/iw3/pretrained_models";
    public const string Iw3DefaultStereoRuntimeDependencyFileName = "iw3_row_flow_v3_20250627.pth";
    public const string Iw3DefaultStereoRuntimeDependencyRelativePath =
        "engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/iw3_row_flow_v3_20250627.pth";
    public const string CliCapabilitiesFileName = "IW3_CLI_CAPABILITIES.json";
    public const string CliCapabilitiesRelativePath = "engine/iw3/IW3_CLI_CAPABILITIES.json";
    public const string ModelCatalogFileName = "MODEL_CATALOG.json";
    public const string ModelCatalogRelativePath = "engine/iw3/nunif/iw3/pretrained_models/MODEL_CATALOG.json";

    public static readonly IReadOnlyList<string> EngineEntryRelativePaths =
    [
        "nunif/iw3/__main__.py",
    ];

    public static readonly IReadOnlyList<string> RequiredRuntimeDependencyRelativePaths =
    [
        Iw3DefaultStereoRuntimeDependencyRelativePath,
    ];

    public static readonly IReadOnlyList<string> PlaceholderOrContractFileNames =
    [
        "README.md",
        "ENGINE_MANIFEST.json",
        "ENGINE_BUNDLE_CONTRACT.md",
        CliCapabilitiesFileName,
        ModelCatalogFileName,
    ];

    public static readonly IReadOnlyList<string> SupportedModelExtensions =
    [
        ".pth",
        ".pt",
        ".onnx",
        ".safetensors",
        ".ckpt",
        ".bin",
    ];
}
