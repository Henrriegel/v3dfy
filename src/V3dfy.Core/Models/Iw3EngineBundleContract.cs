namespace V3dfy.Core.Models;

public static class Iw3EngineBundleContract
{
    public const string EngineDirectoryRelativePath = "engine/iw3";
    public const string ManifestRelativePath = "engine/iw3/ENGINE_MANIFEST.json";
    public const string PythonExecutableRelativePath = "engine/iw3/python/python.exe";
    public const string ModelsDirectoryRelativePath = "engine/iw3/models";
    public const string CliCapabilitiesFileName = "IW3_CLI_CAPABILITIES.json";
    public const string CliCapabilitiesRelativePath = "engine/iw3/IW3_CLI_CAPABILITIES.json";
    public const string ModelCatalogFileName = "MODEL_CATALOG.json";
    public const string ModelCatalogRelativePath = "engine/iw3/models/MODEL_CATALOG.json";

    public static readonly IReadOnlyList<string> EngineEntryRelativePaths =
    [
        "iw3.py",
        "iw3/__main__.py",
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
