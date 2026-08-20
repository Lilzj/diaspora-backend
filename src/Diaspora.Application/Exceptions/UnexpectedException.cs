namespace Diaspora.Application.Exceptions;

public sealed class UnexpectedException : Exception
{
    public UnexpectedException(string message = "An unexpected error occurred.", Exception? innerException = null)
        : base(message, innerException) { }
}
