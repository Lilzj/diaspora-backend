using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Publishing;

/// <summary>
/// Checks the only two things a publish request carries.
/// <para>
/// Whether the guide is <em>publishable</em> is not asked here — that is the aggregate's
/// <c>IsPublishable</c>, which needs the loaded document to answer.
/// </para>
/// </summary>
public sealed class PublishGuideCommandValidator : AbstractValidator<PublishGuideCommand>
{
    public PublishGuideCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.RevisionNotes)
                .MaximumLength(GuideLimits.MaxRevisionNotes);

            RuleFor(command => command.Request.BaseRevision)
                .GreaterThanOrEqualTo(0)
                .When(command => command.Request.BaseRevision is not null);
        });
    }
}
