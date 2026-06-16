using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Core.Image;

public sealed record ImageParallaxExportRequest(
    string SourcePath,
    string OutputDirectory,
    string DepthIntensity,
    string MotionDirection,
    string ZoomAmplitude,
    string Duration,
    string Smoothing,
    string LayerBehavior,
    int FramesPerSecond,
    InternalToolPaths ExpectedToolPaths,
    EngineDependencyHealth DependencyHealth,
    LocalModelPlanSelection? SelectedLocalModel,
    CancellationToken CancellationToken = default);
