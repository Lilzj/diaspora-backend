using Diaspora.Contracts.Moderation.V1;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// What one member of staff may grant as promotional credit, alone (7 Nov 2026).
/// <para>
/// <b>Why this exists.</b> <c>POST /financial/operations/credit</c> had no self-grant guard and no
/// cap: one staff account could put any amount of credit in any wallet, its own included. Reward
/// points already refused both (RWD-10); this is the same rule for credit, which is closer to money.
/// </para>
/// <para>
/// <b>Constants, not a published policy.</b> The owner's due-process numbers are constants
/// (<see cref="ModerationPolicy"/>), so changing one is a reviewed change rather than a configuration
/// edit. Both limits are in the grant currency's <em>major</em> unit, whatever the currency: 100 means
/// £100, $100 or ₦100. That is deliberately simple and has a known edge: 100 of a currency worth very
/// little (the naira, the shilling) is a small grant, so a large goodwill gesture in such a currency
/// needs several grants or a code change. The cap errs towards granting too little, which a second
/// grant fixes; the opposite cannot be undone by anyone but the member spending it.
/// </para>
/// </summary>
public static class CreditGrantPolicy
{
    /// <summary>
    /// The largest single grant, with or without a second Admin, in the currency's major unit.
    /// Above it the grant is refused outright.
    /// </summary>
    public const decimal MaximumMajorUnits = 100m;

    /// <summary>
    /// The largest grant one Admin may make alone, in the currency's major unit. Above it, up to
    /// <see cref="MaximumMajorUnits"/>, the grant goes through a grant request a second Admin decides.
    /// </summary>
    public const decimal ApprovalThresholdMajorUnits = 50m;

    /// <summary>The shortest reason a grant may carry. The member reads it on their ledger.</summary>
    public const int MinimumReasonLength = ModerationPolicy.MinimumNoteLength;

    /// <summary>Nobody grants, asks for or approves credit for their own account.</summary>
    public static Error? RefuseSelf(Guid staffUserId, Guid memberUserId) =>
        staffUserId == memberUserId
            ? Error.Forbidden(
                FinancialErrorCodes.CreditGrantToSelf,
                "Staff cannot grant credit to their own account; another Admin has to.")
            : null;

    /// <summary>A grant says why, in words a member can read.</summary>
    public static Error? RefuseReason(string? reason) =>
        reason is null || reason.Trim().Length < MinimumReasonLength
            ? Error.Validation(
                FinancialErrorCodes.CreditGrantReasonRequired,
                $"A grant needs a reason of at least {MinimumReasonLength} characters: "
                + "why is this member being given credit? They read it.")
            : null;

    /// <summary>
    /// Whether the amount may be granted at all: more than nothing, and no more than
    /// <see cref="MaximumMajorUnits"/>. Says nothing about the threshold; <see cref="NeedsApproval"/> does.
    /// </summary>
    public static Error? RefuseAmount(decimal amount)
    {
        if (amount <= 0m)
        {
            return Error.Validation(
                FinancialErrorCodes.AmountInvalid, "A grant must be for more than nothing.");
        }

        return amount > MaximumMajorUnits
            ? Error.Conflict(
                FinancialErrorCodes.CreditGrantAboveMaximum,
                $"The largest single credit grant is {MaximumMajorUnits:0} in the grant's currency.")
            : null;
    }

    /// <summary>Whether a grant of this size needs a second Admin.</summary>
    public static bool NeedsApproval(decimal amount) => amount > ApprovalThresholdMajorUnits;
}
