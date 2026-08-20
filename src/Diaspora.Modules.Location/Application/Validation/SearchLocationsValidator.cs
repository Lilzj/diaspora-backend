using Diaspora.Modules.Location.Application.Search;
using Diaspora.Modules.Location.Domain.Search;
using FluentValidation;

namespace Diaspora.Modules.Location.Application.Validation;

/// <summary>
/// Shape validation for place-name search.
/// <para>
/// The minimum length is enforced here as well as in the domain, and that duplication is
/// deliberate: the validator turns it into a proper problem document with a field name, which
/// is what a debounced input needs to render an inline hint rather than an error banner.
/// </para>
/// </summary>
public sealed class SearchLocationsValidator : AbstractValidator<SearchLocationsQuery>
{
    public SearchLocationsValidator()
    {
        RuleFor(query => query.Term)
            .NotEmpty()
            .MinimumLength(LocationTextQuery.MinTermLength)
            .WithMessage(
                $"Enter at least {LocationTextQuery.MinTermLength} characters to search for a place.")
            .MaximumLength(LocationTextQuery.MaxTermLength);
    }
}
