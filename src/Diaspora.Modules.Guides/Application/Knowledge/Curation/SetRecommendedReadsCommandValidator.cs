using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.Curation;

/// <summary>Shape checks. Self-recommendation and duplicate collapsing belong to the aggregate.</summary>
public sealed class SetRecommendedReadsCommandValidator
    : AbstractValidator<SetRecommendedReadsCommand>
{
    public SetRecommendedReadsCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Reads)
            .Must(reads => reads.Count <= KnowledgeLimits.MaxRecommendedReads)
            .WithMessage("That is more next reads than one guide can recommend.");

        RuleForEach(command => command.Request.Reads).ChildRules(read =>
        {
            read.RuleFor(value => value.GuideId).NotEmpty();
            read.RuleFor(value => value.Reason).MaximumLength(KnowledgeLimits.MaxCuratorNote);
        });
    }
}
