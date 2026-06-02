namespace V3dfy.Core.Processes;

public sealed record ProcessOutputLine(
    ProcessOutputStream Stream,
    string Text,
    DateTimeOffset CapturedAt);
