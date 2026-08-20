using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>Shape checks. Key grammar and country validity belong to the aggregate.</summary>
public sealed class CreateLearningPathCommandValidator
    : AbstractValidator<CreateLearningPathCommand>
{
    public CreateLearningPathCommandValidator()
    {
        RuleFor(command => command.CountryCode).NotEmpty().Length(2);
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Key)
            .NotEmpty()
            .MaximumLength(KnowledgeLimits.MaxPathKey);

        RuleFor(command => command.Request.Title)
            .NotEmpty()
            .MaximumLength(KnowledgeLimits.MaxPathTitle);

        RuleFor(command => command.Request.Description)
            .MaximumLength(KnowledgeLimits.MaxPathDescription);
    }
}
