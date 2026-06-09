namespace V3dfy.Core.Preview;

public sealed record PreviewTimeRange(TimeSpan From, TimeSpan To)
{
    public TimeSpan Duration => To - From;
}
