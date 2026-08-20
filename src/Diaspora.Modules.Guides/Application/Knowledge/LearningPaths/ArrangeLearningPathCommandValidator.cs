using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// Shape checks.
/// <para>
/// The rule that a step must point at a guide or a category is the aggregate's, not this — it is an
/// invariant of the domain rather than a property of an HTTP request, and a step that pointed at
/// nothing would break every progress indicator on the platform whatever created it.
/// </para>
/// </summary>
public sealed class ArrangeLearningPathCommandValidator
    : AbstractValidator<ArrangeLearningPathCommand>
{
    public ArrangeLearningPathCommandValidator()
    {
        RuleFor(command => command.CountryCode).NotEmpty().Length(2);
        RuleFor(command => command.Key).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Steps)
            .Must(steps => steps.Count <= KnowledgeLimits.MaxPathSteps)
            .WithMessage("That is more steps than one learning path can hold.");

        RuleForEach(command => command.Request.Steps).ChildRules(step =>
        {
            step.RuleFor(value => value.Title)
                .NotEmpty()
                .MaximumLength(KnowledgeLimits.MaxStepTitle);

            step.RuleFor(value => value.Description)
                .MaximumLength(KnowledgeLimits.MaxStepDescription);

            step.RuleFor(value => value.KnowledgeCategoryKey)
                .MaximumLength(KnowledgeLimits.MaxCategoryKey);
        });
    }
}
