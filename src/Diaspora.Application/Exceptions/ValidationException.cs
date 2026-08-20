using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Exceptions;

public sealed class ValidationException : Exception
{
    public IReadOnlyList<Error> Errors { get; }

    public ValidationException(IReadOnlyList<Error> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
