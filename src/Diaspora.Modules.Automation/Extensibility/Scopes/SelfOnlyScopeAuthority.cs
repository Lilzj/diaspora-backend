using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Automation.Extensibility.Scopes;

/// <summary>
/// The shipped answer: a member may automate their own affairs and nobody may broadcast.
/// <para>
/// <b>Registered rather than omitted, and it is not a stub.</b> A missing registration would produce a
/// resolution failure at the first request; a registered default that answers "you own your own scope
/// and hold no broadcast capability" produces an answer a client can branch on and a capability report
/// something true to say. PSR-1.2's rule — "a stub is worse than an absence" — is satisfied because
/// this pretends nothing exists that does not: the capability list it returns is empty, the audience
/// it estimates is one, and every scope it does not own is refused.
/// </para>
/// <para>
/// <b>It handles every owner kind, deliberately.</b> A module that ships a real authority for
/// communities registers <c>IAutomationScopeAuthority</c> before this module composes and the resolver
/// prefers the one that names the kind explicitly, so this remains the floor rather than a competitor.
/// The alternative — handling only <c>User</c> and letting an unhandled kind fall through to nothing —
/// would make "no implementation" mean "allowed", which is the wrong direction for a permission.
/// </para>
/// </summary>
public sealed class SelfOnlyScopeAuthority : IAutomationScopeAuthority
{
    /// <summary>
    /// The capabilities a member holds over their own affairs.
    /// <para>
    /// <b>The dividing line is whether the capability can reach another person.</b> These five gate
    /// triggers about the member's own money, their own listings, their own property, their own job
    /// postings and their own provider profile — automating one is a member arranging their own
    /// notifications, and refusing it would make the whole feature unusable for the surfaces it was
    /// built for.
    /// </para>
    /// <para>
    /// <b>The three that are withheld are exactly the three that appear on an action reaching other
    /// people</b> — <c>automations.community</c>, <c>automations.organization</c> and
    /// <c>automations.creator</c>. This authority cannot tell a moderator from a member or a verified
    /// creator from anybody else; that is B6, B12 and B18's knowledge, and a floor that guessed would
    /// be a second, divergent answer to a question those modules already answer. Until one of them
    /// implements the seam, nobody broadcasts.
    /// </para>
    /// <para>
    /// <c>automations.location</c> is granted since 21 Oct 2026, and it is on the personal side of
    /// the line: an area trigger tells the member about their own movements and reaches nobody. It
    /// was withheld while there was no backend for it; there is now an opt-in, foreground-only
    /// presence report that keeps no history (<c>POST /location/me/presence/ping</c>). Nothing fires
    /// for a member who has not switched that on, and that switch — not this capability — is the
    /// consent.
    /// </para>
    /// </summary>
    private static readonly string[] PersonalCapabilities =
    [
        AutomationCapabilities.Financial,
        AutomationCapabilities.Seller,
        AutomationCapabilities.Landlord,
        AutomationCapabilities.Employer,
        AutomationCapabilities.Provider,
        AutomationCapabilities.Location
    ];

    public IReadOnlyList<AutomationOwnerKind> Handles { get; } = Enum.GetValues<AutomationOwnerKind>();

    public Task<AutomationScopeGrant> EvaluateAsync(
        UserId member,
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        // The member's own scope. Owner kinds that describe a role somebody plays rather than a group
        // they administer — creator, seller, landlord, employer, provider — are still the member's own
        // record, so an automation owned by one of them is an automation about their own affairs. What
        // the member may *do* under that role is a capability, and this authority grants none.
        var isSelf = ownerId == member.Value && ownerKind is not (
            AutomationOwnerKind.Community or
            AutomationOwnerKind.Organization or
            AutomationOwnerKind.Platform);

        return Task.FromResult(
            isSelf
                ? new AutomationScopeGrant(
                    CanOwn: true,
                    CanManage: true,

                    // False, and it is defence in depth rather than the control. Every action that
                    // reaches another person also requires a capability this authority withholds, so
                    // the capability check refuses first; this makes the second check refuse too.
                    CanReachMembers: false,
                    Label: "You",
                    Capabilities: PersonalCapabilities)
                : AutomationScopeGrant.None());
    }

    public Task<AutomationAudienceEstimate> EstimateAudienceAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            ownerKind is AutomationOwnerKind.Community or AutomationOwnerKind.Organization
                or AutomationOwnerKind.Platform
                ? AutomationAudienceEstimate.Unavailable(
                    "This deployment cannot say how many people a "
                    + $"{ownerKind.ToString().ToLowerInvariant()} automation would reach.")
                : new AutomationAudienceEstimate(1, "Just you.", IsExact: true));

    /// <summary>
    /// Nobody.
    /// <para>
    /// This is not a broadcast authority and must not answer as though it were. A module that grants a
    /// reaching capability implements this seam too; one that granted the capability and left this
    /// empty would produce a run recorded as skipped with "this deployment could not work out who to
    /// notify", which is the honest failure and is far better than a broadcast that silently reached
    /// one person.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<UserId>> ListRecipientsAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserId>>([]);
}

/// <summary>
/// Picks the authority that owns a scope kind.
/// <para>
/// Prefers an implementation that names the kind explicitly over one that handles everything, so the
/// shipped floor never shadows a module's real answer regardless of registration order — which
/// matters because registration order here is assembly scan order, and depending on it is how a
/// permission comes to be decided by a file name.
/// </para>
/// </summary>
public sealed class AutomationScopeAuthorityResolver
{
    private readonly IReadOnlyList<IAutomationScopeAuthority> _authorities;

    public AutomationScopeAuthorityResolver(IEnumerable<IAutomationScopeAuthority> authorities)
    {
        ArgumentNullException.ThrowIfNull(authorities);
        _authorities = [.. authorities];
    }

    public IAutomationScopeAuthority For(AutomationOwnerKind ownerKind)
    {
        var everyKind = Enum.GetValues<AutomationOwnerKind>().Length;

        var specific = _authorities.FirstOrDefault(
            authority => authority.Handles.Count < everyKind && authority.Handles.Contains(ownerKind));

        return specific
            ?? _authorities.FirstOrDefault(authority => authority.Handles.Contains(ownerKind))
            ?? _authorities[^1];
    }
}
