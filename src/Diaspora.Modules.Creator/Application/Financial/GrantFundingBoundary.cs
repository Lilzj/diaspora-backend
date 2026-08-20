namespace Diaspora.Modules.Creator.Application.Financial;

/// <summary>
/// How an approved grant becomes money, written down.
/// <para>
/// <b>This type holds no behaviour and that is the point.</b> CREATOR-GAP-005 asks for "a documented
/// rule for how an approved grant becomes money — a B15 payout, a wallet credit, or an off-platform
/// transfer", and §10 of the Wave 6 brief requires the chosen path to be named rather than implied.
/// The Creator Platform's answer is that it does not move money at all, so there is nothing here to
/// execute; what there is, is the boundary stated where somebody looking for the code will find it.
/// </para>
///
/// <para><b>The path, end to end.</b></para>
/// <list type="number">
///   <item>
///     A reviewer approves an application through <c>DecideGrantApplicationCommand</c>. The awarded
///     amount is the reviewer's figure, bounded by the grant's own maximum, and is stored on the
///     application as minor units plus a currency — the platform's shape for money everywhere.
///   </item>
///   <item>
///     The aggregate raises <c>GrantApplicationDecidedDomainEvent</c>, which the relay publishes as
///     <c>GrantApplicationDecidedIntegrationEvent</c> carrying the application, the grant, the
///     applicant, the awarded amount and the currency — and none of the applicant's answers.
///   </item>
///   <item>
///     <b>Finance executes the movement through B15's existing credit path:</b>
///     <c>POST /api/v1/financial/operations/credits</c>, which dispatches B15's
///     <c>GrantCreditCommand</c>. That command is <c>[Transactional]</c>, writes a
///     <c>CreditGrant</c> and posts the ledger entries behind it, and requires
///     <c>financial:credit:manage</c> — a permission restricted to Admin and SuperAdmin.
///   </item>
///   <item>
///     The grant lands as <c>CreditKind.Promotional</c> in the recipient's <c>Promotional</c>
///     wallet, which is B15's own answer to "credit is not money and the platform must never confuse
///     the two": granted rather than deposited, expirable, and not withdrawable.
///   </item>
///   <item>
///     Finance records B15's reference back onto the application through
///     <c>GrantApplication.Activate</c>, which moves it to <c>Active</c> and starts its reporting
///     obligations. Until that happens the application reads <c>Approved</c> with a null
///     <c>fundingReference</c> — which is what makes "approved but not yet funded" a state the
///     platform can see rather than an assumption.
///   </item>
/// </list>
///
/// <para><b>Why operator-mediated rather than automatic.</b></para>
/// <para>
/// An approved grant is a decision to give somebody money on the strength of what they wrote. B15's
/// own credit path is deliberately staff-gated for exactly that class of movement, and closing the
/// loop automatically would have meant either the Creator Platform holding
/// <c>financial:credit:manage</c> — a business module able to mint credit — or B15 growing a
/// consumer that pays out on another module's say-so. Both are the shape §18 of the brief forbids:
/// "Do not directly write WalletBalance, LedgerEntry, Payment, Transaction from Creator Platform."
/// </para>
/// <para>
/// <b>What would change if that becomes automatic.</b> A B15 consumer of
/// <c>GrantApplicationDecidedIntegrationEvent</c> that granted credit, and an
/// <c>Activate</c> call driven by <c>CreditGrantedIntegrationEvent</c> coming back. The contract
/// already carries everything such a consumer would need, which is why the payload was shaped this
/// way; the decision not to write it is B15's to make, not this phase's.
/// </para>
///
/// <para><b>Alternatives considered and rejected.</b></para>
/// <list type="bullet">
///   <item>
///     <b>A payout.</b> <c>Withdrawal</c> moves money out to a destination the member registered,
///     and a grant is money coming <em>in</em>. Using the payout path would have made a grant a
///     withdrawal against a balance that did not exist.
///   </item>
///   <item>
///     <b>A wallet credit written from this module.</b> A second writer of the ledger, which the
///     Financial Platform's central claim — "there is no code path anywhere on the platform that
///     sets a balance" — exists to prevent.
///   </item>
///   <item>
///     <b>Off-platform transfer.</b> Legitimate for a programme that pays by bank transfer, and the
///     contract supports it: the event carries the amount, and <c>Activate</c> records whatever
///     reference the transfer produced. Nothing in this module assumes the money moved through B15,
///     which is why <c>FundingReference</c> is a string rather than a B15 identifier.
///   </item>
/// </list>
/// </summary>
public static class GrantFundingBoundary
{
    /// <summary>
    /// The B15 route finance uses to execute an approved grant.
    /// <para>
    /// A constant so the report, the tests and this documentation cannot drift from each other.
    /// Nothing in this module calls it.
    /// </para>
    /// </summary>
    public const string ExecutionRoute = "/api/v1/financial/operations/credits";

    /// <summary>The permission that route requires. Admin and SuperAdmin only.</summary>
    public const string ExecutionPermission = "financial:credit:manage";

    /// <summary>The credit kind an executed grant lands as.</summary>
    public const string CreditKind = "Promotional";
}
