using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;

namespace V3dfy.Engine.Iw3.Execution;

public class LocalIw3ProcessRequestBuilder
{
    private readonly ConversionExecutionRequestValidator _requestValidator;
    private readonly Iw3CommandBuilder _commandBuilder;

    public LocalIw3ProcessRequestBuilder(
        ConversionExecutionRequestValidator? requestValidator = null,
        Iw3CommandBuilder? commandBuilder = null)
    {
        _requestValidator = requestValidator ?? new ConversionExecutionRequestValidator();
        _commandBuilder = commandBuilder ?? new Iw3CommandBuilder();
    }

    public virtual ProcessExecutionRequest Build(ConversionExecutionRequest request)
    {
        var validationResult = _requestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw CreateInvalidRequestException(validationResult);
        }

        var command = _commandBuilder.Build(
            CreateConversionRequest(request),
            request.ExpectedToolPaths,
            CreateCompleteHealthStatus(),
            request.SelectedLocalModel,
            requireVerifiedDepthModel: true);

        return new(
            ExecutablePath: command.ExecutablePath,
            Arguments: command.Arguments,
            WorkingDirectory: request.ExpectedToolPaths.NunifRootDirectory,
            EnvironmentVariables: Iw3BundledRuntimeEnvironment.Create(request.ExpectedToolPaths),
            AllowedRootDirectory: request.ExpectedToolPaths.Iw3EngineDirectory);
    }

    private static ConversionRequest CreateConversionRequest(
        ConversionExecutionRequest request) => new(
        InputPath: request.SourcePath,
        OutputPath: request.OutputPath,
        OutputContainer: request.OutputContainer,
        ThreeDOutputFormat: request.ThreeDOutputFormat,
        AiQualityPreset: request.QualityPreset,
        ThreeDIntensity: request.Intensity);

    private static EngineHealthStatus CreateCompleteHealthStatus() => new(
        Ffmpeg: ToolHealthStatus.Found,
        Ffprobe: ToolHealthStatus.Found,
        Python: ToolHealthStatus.Found,
        Iw3EngineDirectory: ToolHealthStatus.Found,
        ModelsDirectory: ToolHealthStatus.Found);

    private static ArgumentException CreateInvalidRequestException(
        ConversionExecutionRequestValidationResult validationResult) => new(
        "Cannot build local iw3 process request from an invalid conversion " +
        "execution request. " +
        string.Join(
            " ",
            validationResult.Issues.Select(issue => issue.EnglishMessage)),
        "request");
}
