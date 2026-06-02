namespace V3dfy.Core.Models;

public sealed record InternalToolPaths(
    string FfmpegExecutable,
    string FfprobeExecutable,
    string PythonExecutable,
    string Iw3EngineDirectory,
    string ModelsDirectory);
