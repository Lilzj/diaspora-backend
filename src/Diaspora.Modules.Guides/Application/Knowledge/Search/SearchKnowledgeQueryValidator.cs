using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.Search;

/// <summary>
/// Shape checks on a search.
/// <para>
/// Deliberately permissive. A search endpoint that rejects a request is a search endpoint that shows
/// somebody an error page instead of results, and almost every wrong-looking parameter has a
/// sensible interpretation: an unknown sort falls back, an unknown difficulty is ignored, a
/// malformed country searches globally. What is refused is only what cannot be interpreted at all —
/// a phrase longer than the column that would count it.
/// </para>
/// </summary>
public sealed class SearchKnowledgeQueryValidator : AbstractValidator<SearchKnowledgeQuery>
{
    public SearchKnowledgeQueryValidator()
    {
        RuleFor(query => query.Request).NotNull();

        RuleFor(query => query.Request.Q)
            .MaximumLength(KnowledgeLimits.MaxSearchTerm);

        RuleFor(query => query.Request.MaxReadingTime)
            .GreaterThan(0)
            .When(query => query.Request.MaxReadingTime is not null);

        RuleFor(query => query.Request.Limit)
            .InclusiveBetween(1, KnowledgeLimits.MaxPageSize)
            .When(query => query.Request.Limit is not null);
    }
}
