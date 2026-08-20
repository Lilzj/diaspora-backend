using Diaspora.SharedKernel.Results;
using FluentValidation;
using MediatR;

namespace Diaspora.Application.Behaviors;

/// <summary>
/// Behavior 3: Validation.
/// Automatically runs FluentValidation validators before the handler executes.
/// Returns standardized validation errors via the Result pattern.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var errors = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => Error.Validation(f.PropertyName, f.ErrorMessage))
            .Distinct()
            .ToList();

        if (errors.Count != 0)
        {
            throw new Exceptions.ValidationException(errors);
        }

        return await next();
    }
}
