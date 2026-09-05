using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Financial.Authorization;

/// <summary>
/// Declares the permissions the Financial Platform owns.
/// <para>
/// <b>Nothing here is granted while an account is restricted, except reading.</b> B14 made the
/// opposite call for personalization on the grounds that a privacy control is a right rather than a
/// privilege; moving money is a privilege, and an account under review is under review precisely
/// because somebody is unsure about its money. Reading a balance survives, because somebody under
/// review must still be able to see what they hold.
/// </para>
/// <para>
/// <b>Exactly one permission opens at the onboarding tier, and it is deliberately the smallest set
/// that is honest.</b> A financial identity is opened the moment somebody registers — so a welcome
/// bonus or a referral credit can land before verification — and a member who has been granted money
/// must be able to see it. That is <c>financial:wallet:read</c> and nothing else: the other four
/// read permissions describe payments, purchases, subscriptions and giving, and open one rung later
/// at <see cref="AccessTier.Limited"/> — the membership rung, where browsing a catalogue and reading
/// your own (usually empty) history belong. Nothing at either tier moves money.
/// </para>
/// <para>
/// <b>Everything that moves money is <see cref="AccessTier.Verified"/>, without exception.</b> Trust
/// exists so that the platform knows who somebody is before that happens; opening a payment surface
/// below verification would make the verification ladder decorative on the one surface where it
/// matters most.
/// </para>
/// <para>
/// <b>Every definition is constructed inline.</b> The contributor analyzers resolve a catalogue name
/// through the semantic model at the <c>new</c> site; a definition built inside a private helper
/// that takes the name as a parameter resolves to nothing and every key in the file is silently
/// exempt from three naming rules. B14 did that for thirteen keys and nobody knew for two phases.
/// PSR-1 made it an architecture rule, and this file is written to satisfy it from the start.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class FinancialPermissionContributor : IPermissionContributor
{
    public string Module => FinancialModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        // ── The member's own money ───────────────────────────────────────────────────────────

        yield return new PermissionDefinition(
            FinancialPermissions.WalletRead, Module, "See your balance and statement.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            FinancialPermissions.PaymentsRead, Module, "See your payment history.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            FinancialPermissions.PaymentsManage, Module, "Pay for something.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            FinancialPermissions.PurchasesCreate, Module, "Buy a premium feature.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            FinancialPermissions.PurchasesRead, Module, "See what you have bought.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            FinancialPermissions.SubscriptionsManage, Module, "Start or cancel a subscription.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            FinancialPermissions.SubscriptionsRead, Module, "See your plan and what it includes.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            FinancialPermissions.PromotionsRedeem, Module, "Use a coupon.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            FinancialPermissions.GivingCreate, Module, "Donate or contribute to a fund.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            FinancialPermissions.GivingRead, Module, "See causes, funds and your own giving.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        // ── Staff ────────────────────────────────────────────────────────────────────────────

        yield return new PermissionDefinition(
            FinancialPermissions.WalletReadAny, Module, "Read anybody's wallet and statement.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.WalletManage, Module, "Freeze, close or provision a wallet.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminOnly()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.PaymentsManageAny, Module, "Refund anybody's payment.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminOnly()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.CatalogManage, Module, "Add, reprice or retire a product.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminOnly()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.PoliciesRead, Module, "Read the financial policy catalogue.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.PoliciesManage, Module, "Change what the platform charges.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminOnly()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.PromotionsManage, Module, "Create and retire promotions.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminOnly()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.CreditManage, Module, "Grant or revoke platform credit.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminOnly()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.GivingManage, Module, "Create and close causes and funds.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.AdvertisingRead,
            Module,
            "See the advertising campaigns waiting to be reviewed.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.AdvertisingManage,
            Module,
            "Approve or reject an advertising campaign.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.ReportingRead, Module, "Read revenue and the financial dashboard.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            FinancialPermissions.OperationsRun, Module, "Run a financial sweep by hand.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminOnly()
        };
    }

    /// <summary>
    /// The two roles that read financial data.
    /// <para>
    /// A fresh set per definition rather than a shared static one, for the reason B6 and B7 both
    /// recorded: a mutable value shared between two definitions is one edit away from being two
    /// definitions that disagree.
    /// </para>
    /// </summary>
    private static HashSet<string> StaffRoles() =>
        new(StringComparer.Ordinal) { "Admin", "Moderator" };

    /// <summary>
    /// The one role that moves money on somebody else's behalf.
    /// <para>
    /// Deliberately narrower than <see cref="StaffRoles"/>. Refunding a stranger's payment, granting
    /// credit and changing what the platform charges are not moderation actions, and a moderator who
    /// could do them would be a moderator whose account is worth stealing.
    /// </para>
    /// </summary>
    private static HashSet<string> AdminOnly() => new(StringComparer.Ordinal) { "Admin" };
}
