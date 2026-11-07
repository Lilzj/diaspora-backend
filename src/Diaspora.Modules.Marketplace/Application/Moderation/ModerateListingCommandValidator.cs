using FluentValidation;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>
/// Every moderation action needs a reason, including lifting one (7 Nov 2026).
/// <para>
/// Listing moderation asked for one only to hide or suspend. A decision with no recorded reason is one nobody can review later, and since the
/// audit trail is stored, the reason given here is kept with the record of who acted.
/// </para>
/// </summary>
public sealed class ModerateListingCommandValidator : AbstractValidator<ModerateListingCommand>
{
    public const int MaxReasonLength = 1000;

    public ModerateListingCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A reason is required for every moderation action. It is kept with the decision.")
            .MaximumLength(MaxReasonLength)
            .When(command => command.Request is not null);
    }
}
