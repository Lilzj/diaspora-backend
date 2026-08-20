using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Versioning;

/// <summary>
/// Rejects a comparison that could not mean anything.
/// <para>
/// Version numbers are one-based and a version cannot be compared with itself — a diff of a
/// version against itself is always empty, so answering it would spend two snapshot reads to
/// tell the caller nothing. Whether the two versions <em>exist</em> is the handler's question,
/// because only it can ask the repository.
/// </para>
/// </summary>
public sealed class CompareGuideVersionsQueryValidator
    : AbstractValidator<CompareGuideVersionsQuery>
{
    public CompareGuideVersionsQueryValidator()
    {
        RuleFor(query => query.GuideId).NotEmpty();

        RuleFor(query => query.FromVersion).GreaterThan(0);

        RuleFor(query => query.ToVersion).GreaterThan(0);

        RuleFor(query => query.ToVersion)
            .NotEqual(query => query.FromVersion)
            .WithMessage("A version cannot be compared with itself.");
    }
}
