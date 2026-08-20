using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.Curation;

/// <summary>Shape checks. Existence and publication of the guide belong to the handler.</summary>
public sealed class FeatureKnowledgeGuideCommandValidator
    : AbstractValidator<FeatureKnowledgeGuideCommand>
{
    public FeatureKnowledgeGuideCommandValidator()
    {
        RuleFor(command => command.CountryCode).NotEmpty().Length(2);
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.GuideId).NotEmpty();

        RuleFor(command => command.Request.KnowledgeCategoryKey)
            .MaximumLength(KnowledgeLimits.MaxCategoryKey);

        RuleFor(command => command.Request.Note)
            .MaximumLength(KnowledgeLimits.MaxCuratorNote);

        RuleFor(command => command.Request.Position)
            .GreaterThanOrEqualTo(0)
            .When(command => command.Request.Position is not null);
    }
}
