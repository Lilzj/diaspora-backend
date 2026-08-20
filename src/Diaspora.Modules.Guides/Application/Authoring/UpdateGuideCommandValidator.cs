using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Bounds the editor's save before it reaches the aggregate.
/// <para>
/// The section and block ceilings are checked here as well as in the domain, deliberately: a
/// scripted client posting forty thousand blocks should be refused before the request body is
/// projected into drafts, and the aggregate should still refuse it if it ever arrives another way.
/// </para>
/// </summary>
public sealed class UpdateGuideCommandValidator : AbstractValidator<UpdateGuideCommand>
{
    public UpdateGuideCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Title)
                .NotEmpty()
                .MinimumLength(GuideLimits.MinTitle)
                .MaximumLength(GuideLimits.MaxTitle);

            RuleFor(command => command.Request.CountryCode)
                .NotEmpty()
                .Length(2);

            RuleFor(command => command.Request.Summary)
                .MaximumLength(GuideLimits.MaxSummary);

            RuleFor(command => command.Request.MetaTitle)
                .MaximumLength(GuideLimits.MaxSeoTitle);

            RuleFor(command => command.Request.MetaDescription)
                .MaximumLength(GuideLimits.MaxSeoDescription);

            RuleFor(command => command.Request.CanonicalUrl)
                .MaximumLength(GuideLimits.MaxSeoUrl);

            RuleFor(command => command.Request.Keywords)
                .MaximumLength(GuideLimits.MaxSeoKeywords);

            RuleFor(command => command.Request.EstimatedCompletionMinutes)
                .GreaterThan(0)
                .When(command => command.Request.EstimatedCompletionMinutes is not null);

            RuleFor(command => command.Request.BaseRevision)
                .GreaterThanOrEqualTo(0)
                .When(command => command.Request.BaseRevision is not null);

            RuleFor(command => command.Request.Sections)
                .Must(sections => sections is null || sections.Count <= GuideLimits.MaxSections)
                .WithMessage($"A guide may hold at most {GuideLimits.MaxSections} sections.");

            RuleForEach(command => command.Request.Sections)
                .ChildRules(section =>
                {
                    section.RuleFor(input => input.SectionId).NotEmpty();

                    section.RuleFor(input => input.Title)
                        .NotEmpty()
                        .MaximumLength(GuideLimits.MaxSectionTitle);

                    section.RuleFor(input => input.Blocks)
                        .Must(blocks => blocks.Count <= GuideLimits.MaxBlocksPerSection)
                        .WithMessage(
                            $"A section may hold at most {GuideLimits.MaxBlocksPerSection} blocks.");

                    section.RuleForEach(input => input.Blocks)
                        .ChildRules(block =>
                        {
                            block.RuleFor(input => input.BlockId).NotEmpty();
                            block.RuleFor(input => input.Kind).NotEmpty();

                            block.RuleFor(input => input.Content)
                                .MaximumLength(GuideLimits.MaxBlockLength);
                        });
                })
                .When(command => command.Request.Sections is not null);
        });
    }
}
