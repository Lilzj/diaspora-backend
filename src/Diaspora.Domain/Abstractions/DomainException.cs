namespace Diaspora.Domain.Abstractions;

public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message, string code = "Domain.Error") : base(message)
    {
        Code = code;
    }

    public DomainException(string message, string code, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }
}
