using FluentValidation;

namespace Diaspora.Modules.Jobs.Application.Moderation;

/// <summary>
/// Every moderation action needs a reason, including lifting one (7 Nov 2026).
/// <para>
/// Job moderation asked for a reason only to hide, and did not keep it. A decision with no recorded reason is one nobody can review later, and since the
/// audit trail is stored, the reason given here is kept with the record of who acted.
/// </para>
/// </summary>
public sealed class ModerateJobPostingCommandValidator : AbstractValidator<ModerateJobPostingCommand>
{
    public const int MaxReasonLength = 1000;

    public ModerateJobPostingCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A reason is required for every moderation action. It is kept with the decision.")
            .MaximumLength(MaxReasonLength)
            .When(command => command.Request is not null);
    }
}
