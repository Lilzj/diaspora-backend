namespace Diaspora.Application.Exceptions;

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Authentication is required.") : base(message) { }
}
