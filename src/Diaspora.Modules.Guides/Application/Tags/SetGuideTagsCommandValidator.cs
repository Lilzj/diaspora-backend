using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Tags;

/// <summary>
/// Bounds the tag set. The <em>shape</em> of each tag is not checked here — the aggregate
/// normalises free text to a slug and deduplicates, so a validator insisting on slugs would
/// reject input the domain was built to accept.
/// </summary>
public sealed class SetGuideTagsCommandValidator : AbstractValidator<SetGuideTagsCommand>
{
    public SetGuideTagsCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Tags)
                .NotNull()
                .Must(tags => tags.Count <= GuideLimits.MaxTags)
                .WithMessage($"A guide may carry at most {GuideLimits.MaxTags} tags.");

            RuleForEach(command => command.Request.Tags)
                .NotEmpty()
                .MaximumLength(GuideLimits.MaxTagLength);

            RuleFor(command => command.Request.BaseRevision)
                .GreaterThanOrEqualTo(0)
                .When(command => command.Request.BaseRevision is not null);
        });
    }
}
