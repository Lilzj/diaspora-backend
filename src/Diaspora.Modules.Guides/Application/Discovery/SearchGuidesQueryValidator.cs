using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Rejects a discovery request whose shape is wrong, before a search round trip.
/// <para>
/// Shape only, and deliberately lenient about vocabulary: an unknown sort key falls back to
/// newest and an unknown category simply matches nothing, because a browse screen that errors is
/// worse than one that shows a different order or an empty list. What is rejected here is what
/// no reasonable client sends — a two-thousand-character search term, a negative page size, a
/// country code that is not a country code.
/// </para>
/// </summary>
public sealed class SearchGuidesQueryValidator : AbstractValidator<SearchGuidesQuery>
{
    /// <summary>The longest term worth running. Past this it is a paste, not a search.</summary>
    private const int MaxTextLength = 200;

    public SearchGuidesQueryValidator()
    {
        RuleFor(query => query.Request).NotNull();

        When(query => query.Request is not null, () =>
        {
            RuleFor(query => query.Request.Query).MaximumLength(MaxTextLength);

            RuleFor(query => query.Request.Q).MaximumLength(MaxTextLength);

            RuleFor(query => query.Request.Country)
                .Length(2)
                .When(query => !string.IsNullOrWhiteSpace(query.Request.Country));

            RuleFor(query => query.Request.Language)
                .Length(2)
                .When(query => !string.IsNullOrWhiteSpace(query.Request.Language));

            RuleFor(query => query.Request.Category).MaximumLength(GuideLimits.MaxCategoryKey);

            RuleFor(query => query.Request.Tag).MaximumLength(GuideLimits.MaxTagLength);

            RuleFor(query => query.Request.Region).MaximumLength(GuideLimits.MaxLocationName);

            RuleFor(query => query.Request.City).MaximumLength(GuideLimits.MaxLocationName);

            RuleFor(query => query.Request.Limit)
                .InclusiveBetween(1, GuideLimits.MaxPageSize)
                .When(query => query.Request.Limit is not null);
        });
    }
}
