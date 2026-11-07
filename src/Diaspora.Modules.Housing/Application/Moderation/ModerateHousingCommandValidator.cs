using FluentValidation;

namespace Diaspora.Modules.Housing.Application.Moderation;

/// <summary>
/// Every moderation action needs a reason, including lifting one (7 Nov 2026).
/// <para>
/// Housing moderation never asked for one, and an archive's reason was lost. A decision with no recorded reason is one nobody can review later, and since the
/// audit trail is stored, the reason given here is kept with the record of who acted.
/// </para>
/// </summary>
public sealed class ModerateHousingCommandValidator : AbstractValidator<ModerateHousingCommand>
{
    public const int MaxReasonLength = 1000;

    public ModerateHousingCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A reason is required for every moderation action. It is kept with the decision.")
            .MaximumLength(MaxReasonLength)
            .When(command => command.Request is not null);
    }
}
