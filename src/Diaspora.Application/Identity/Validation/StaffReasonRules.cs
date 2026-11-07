using Diaspora.Contracts.Moderation.V1;
using FluentValidation;

namespace Diaspora.Application.Identity.Validation;

/// <summary>
/// What a staff action on an account must say about why (the owner's rule, 7 Nov 2026).
/// <para>
/// A reason code from the platform's one list of moderation reasons, so the member is told a reason
/// in words they can read, and a note in the member of staff's own words of at least
/// <see cref="MinimumNoteLength"/> characters, so "x" is not a reason.
/// </para>
/// </summary>
public static class StaffReasonRules
{
    public const int MinimumNoteLength = 10;

    /// <summary>The note becomes the account's status reason, so it shares that limit.</summary>
    public const int MaximumNoteLength = 512;

    public static IRuleBuilderOptions<T, string> MustBeReasonCode<T>(
        this IRuleBuilderInitial<T, string> rule, Func<string, string> message)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(message);

        return rule
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(message(IdentityMessageKeys.ReasonCodeRequired))
            .Must(code => ModerationReasons.TryParse(code, out _))
            .WithMessage(message(IdentityMessageKeys.ReasonCodeUnknown));
    }

    public static IRuleBuilderOptions<T, string?> MustBeStaffNote<T>(
        this IRuleBuilderInitial<T, string?> rule,
        Func<string, string> message,
        string requiredKey = IdentityMessageKeys.ReasonRequired)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(message);

        return rule
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(message(requiredKey))
            .Must(note => note!.Trim().Length >= MinimumNoteLength)
            .WithMessage(message(IdentityMessageKeys.ReasonTooShort))
            .MaximumLength(MaximumNoteLength).WithMessage(message(IdentityMessageKeys.ReasonLength));
    }
}
