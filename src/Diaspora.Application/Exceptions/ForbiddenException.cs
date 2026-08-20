namespace Diaspora.Application.Exceptions;

public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You do not have permission to perform this action.") : base(message) { }
}
