using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Deliberately the laxest validator in the module.
/// <para>
/// An auto-save that refused a half-typed section would fight the writer every few seconds, so
/// only the bounds that protect storage are checked — how many sections, how many blocks, how
/// long a block may be. Everything about whether the document is <em>good</em> is the real save's
/// question.
/// </para>
/// </summary>
public sealed class SaveGuideDraftCommandValidator : AbstractValidator<SaveGuideDraftCommand>
{
    public SaveGuideDraftCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Sections)
                .NotNull()
                .Must(sections => sections.Count <= GuideLimits.MaxSections)
                .WithMessage($"A guide may hold at most {GuideLimits.MaxSections} sections.");

            RuleForEach(command => command.Request.Sections)
                .ChildRules(section =>
                {
                    section.RuleFor(input => input.SectionId).NotEmpty();

                    section.RuleFor(input => input.Blocks)
                        .Must(blocks => blocks.Count <= GuideLimits.MaxBlocksPerSection)
                        .WithMessage(
                            $"A section may hold at most {GuideLimits.MaxBlocksPerSection} blocks.");

                    section.RuleForEach(input => input.Blocks)
                        .ChildRules(block => block.RuleFor(input => input.Content)
                            .MaximumLength(GuideLimits.MaxBlockLength));
                });
        });
    }
}
