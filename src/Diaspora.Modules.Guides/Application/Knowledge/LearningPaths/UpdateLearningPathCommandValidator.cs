using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>Shape checks only.</summary>
public sealed class UpdateLearningPathCommandValidator
    : AbstractValidator<UpdateLearningPathCommand>
{
    public UpdateLearningPathCommandValidator()
    {
        RuleFor(command => command.CountryCode).NotEmpty().Length(2);
        RuleFor(command => command.Key).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Title)
            .NotEmpty()
            .MaximumLength(KnowledgeLimits.MaxPathTitle);

        RuleFor(command => command.Request.Description)
            .MaximumLength(KnowledgeLimits.MaxPathDescription);
    }
}
