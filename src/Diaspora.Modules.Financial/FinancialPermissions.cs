namespace Diaspora.Modules.Financial;

/// <summary>
/// The permission keys the Financial Platform owns.
/// <para>
/// <b>Three segments throughout — <c>financial:resource:action</c> — which is the shape the
/// platform's own analyzer documents and the shape a new module should copy.</b> B13 and B14 between
/// them shipped 25 keys with a dotted sub-module segment and 7 ending <c>:view</c> rather than
/// <c>:read</c>; both are open items in the technical debt ledger awaiting a versioned authorisation
/// release. This module was written afterwards and simply does not join them.
/// </para>
/// <para>
/// <b>Nothing here is granted under restriction, and that is the opposite of B14's choice for a
/// deliberate reason.</b> Personalization controls are a privacy right that must survive suspension;
/// moving money is not. A restricted account can still read its wallet and its statement — somebody
/// under review must be able to see what they hold — and can do nothing that moves a balance.
/// </para>
/// </summary>
public static class FinancialPermissions
{
    // ── The member's own money ───────────────────────────────────────────────────────────────

    /// <summary>See your own wallet, statement and transaction history.</summary>
    public const string WalletRead = "financial:wallet:read";

    /// <summary>Start, authorise, cancel and refund your own payments.</summary>
    public const string PaymentsManage = "financial:payments:manage";

    /// <summary>Read your own payment history.</summary>
    public const string PaymentsRead = "financial:payments:read";

    /// <summary>Buy from the monetization catalogue.</summary>
    public const string PurchasesCreate = "financial:purchases:create";

    /// <summary>Read your own purchases and placements.</summary>
    public const string PurchasesRead = "financial:purchases:read";

    /// <summary>Start, change and cancel your own subscription.</summary>
    public const string SubscriptionsManage = "financial:subscriptions:manage";

    /// <summary>Read your own subscription and the plan catalogue.</summary>
    public const string SubscriptionsRead = "financial:subscriptions:read";

    /// <summary>Redeem a coupon.</summary>
    public const string PromotionsRedeem = "financial:promotions:redeem";

    /// <summary>Give — donations and community contributions.</summary>
    public const string GivingCreate = "financial:giving:create";

    /// <summary>Read your own giving history, and the public causes and funds.</summary>
    public const string GivingRead = "financial:giving:read";

    // ── Staff ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Read anybody's wallet and statement.</summary>
    public const string WalletReadAny = "financial:wallet:read:any";

    /// <summary>Freeze, unfreeze and close a wallet; provision an identity by hand.</summary>
    public const string WalletManage = "financial:wallet:manage";

    /// <summary>Refund anybody's payment.</summary>
    public const string PaymentsManageAny = "financial:payments:manage:any";

    /// <summary>Add, reprice and retire monetization products.</summary>
    public const string CatalogManage = "financial:catalog:manage";

    /// <summary>Publish a new version of a financial policy.</summary>
    public const string PoliciesManage = "financial:policies:manage";

    /// <summary>Read the policy catalogue, including inactive versions.</summary>
    public const string PoliciesRead = "financial:policies:read";

    /// <summary>Create, pause and exhaust promotions.</summary>
    public const string PromotionsManage = "financial:promotions:manage";

    /// <summary>Grant and revoke platform credit.</summary>
    public const string CreditManage = "financial:credit:manage";

    /// <summary>Create and close donation causes and community funds.</summary>
    public const string GivingManage = "financial:giving:manage";

    /// <summary>Read revenue, the dashboard and platform-wide financial reporting.</summary>
    public const string ReportingRead = "financial:reporting:read";

    /// <summary>Run a sweep or a rebuild by hand.</summary>
    public const string OperationsRun = "financial:operations:run";
}
