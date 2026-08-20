namespace Diaspora.Modules.Financial;

/// <summary>
/// The feature gates the Financial Platform owns.
/// <para>
/// <b>Every member-facing gate opens at <c>Verified</c>, and that is the whole tier decision of this
/// phase.</b> The platform's handbook records the general rule — a control opens at the same rung as
/// the thing it controls — and the thing being controlled here is money leaving somebody's hands.
/// Trust exists precisely so that the platform knows who somebody is before that happens; opening a
/// payment surface below verification would make the verification ladder decorative on the one
/// surface where it matters most.
/// </para>
/// <para>
/// <b>The exception is reading.</b> <see cref="Wallet"/> covers seeing a balance and a statement, and
/// it opens at <c>Onboarding</c> alongside every other read of your own data — because a member who
/// was granted credit during onboarding must be able to see it, and because a control that opens
/// later than the processing it governs is not a control.
/// </para>
/// </summary>
public static class FinancialFeatures
{
    /// <summary>See your own wallet, balance and statement. Open at the onboarding tier.</summary>
    public const string Wallet = "financial.wallet";

    /// <summary>Pay for something. Verified only.</summary>
    public const string Payments = "financial.payments";

    /// <summary>Buy a monetization product. Verified only.</summary>
    public const string Purchases = "financial.purchases";

    /// <summary>Hold a subscription. Verified only.</summary>
    public const string Subscriptions = "financial.subscriptions";

    /// <summary>Give to a cause or a fund. Verified only.</summary>
    public const string Giving = "financial.giving";

    /// <summary>Operate the platform's finances. Staff only.</summary>
    public const string Operations = "financial.operations";
}
