namespace Diaspora.Application.Exceptions;

public sealed class InfrastructureException : Exception
{
    public string ServiceName { get; }

    public InfrastructureException(string serviceName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ServiceName = serviceName;
    }
}
