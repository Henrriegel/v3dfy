using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Preview;

namespace V3dfy.Engine.Iw3.Execution;

public sealed record Iw3PreviewGenerationRequest(
    PreviewConfigurationSnapshot Configuration,
    PreviewCachePaths CachePaths,
    InternalToolPaths ExpectedToolPaths,
    LocalModelPlanSelection SelectedLocalModel,
    Iw3CliCapabilitiesManifest? Iw3CliCapabilities = null,
    CancellationToken CancellationToken = default)
{
    public string SourcePath => Configuration.SourcePath;

    public OutputContainer OutputContainer => Configuration.OutputContainer;

    public AiQualityPreset QualityPreset => Configuration.QualityPreset;

    public ThreeDIntensity Intensity => Configuration.Intensity;

    public ThreeDOutputFormat ThreeDOutputFormat => Configuration.ThreeDOutputFormat;
}
