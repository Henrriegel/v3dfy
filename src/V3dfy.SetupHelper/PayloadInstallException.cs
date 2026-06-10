namespace V3dfy.SetupHelper;

public sealed class PayloadInstallException : Exception
{
    public PayloadInstallException(string message)
        : base(message)
    {
    }

    public PayloadInstallException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
