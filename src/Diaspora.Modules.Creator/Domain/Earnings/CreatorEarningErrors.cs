using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Earnings;

/// <summary>
/// What <see cref="CreatorEarning"/> refuses, declared once.
/// <para>
/// Every one of these is a programming error rather than something a member can cause: earnings are
/// recorded by consumers from platform events, not submitted. They exist so a consumer that gets the
/// arithmetic or the ordering wrong fails loudly at the aggregate instead of writing a row that a
/// revenue dashboard would later present as fact.
/// </para>
/// </summary>
public static class CreatorEarningErrors
{
    public static readonly Error SourceReferenceRequired = Error.Validation(
        "Creator.Earning.SourceReferenceRequired",
        "An earning needs the reference of the event that produced it.");

    public static readonly Error CurrencyRequired = Error.Validation(
        "Creator.Earning.CurrencyRequired", "An earning needs a currency.");

    public static readonly Error AmountNegative = Error.Validation(
        "Creator.Earning.AmountNegative", "An earning cannot be negative.");

    /// <summary>
    /// A share larger than what was paid.
    /// <para>
    /// Only a misconfigured rate produces this — a share above 100% — and it is refused rather than
    /// clamped, because clamping would quietly pay the configured amount while the dashboard showed
    /// the paid one.
    /// </para>
    /// </summary>
    public static readonly Error ShareExceedsGross = Error.Validation(
        "Creator.Earning.ShareExceedsGross",
        "A creator's share cannot be more than the amount that was paid.");

    public static readonly Error NotPending = Error.Conflict(
        "Creator.Earning.NotPending", "Only a pending earning can be made available.");

    public static readonly Error NotAvailable = Error.Conflict(
        "Creator.Earning.NotAvailable", "Only an available earning can be paid out.");

    public static readonly Error NotProcessing = Error.Conflict(
        "Creator.Earning.NotProcessing", "Only an earning on a payout can be marked paid.");

    public static readonly Error NotATerminalReversal = Error.Validation(
        "Creator.Earning.NotATerminalReversal",
        "A reversal must be Reversed, Refunded or Cancelled.");
}
