using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Moderation;

/// <summary>Insists the note says something, and bounds how much.</summary>
public sealed class AddGuideModerationNoteCommandValidator
    : AbstractValidator<AddGuideModerationNoteCommand>
{
    public AddGuideModerationNoteCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Note)
            .NotEmpty()
            .MaximumLength(GuideLimits.MaxModerationNote)
            .When(command => command.Request is not null);
    }
}
