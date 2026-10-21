using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Errors;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Extensibility.Scopes;
using Diaspora.Modules.Automation.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Automation.Application.Services;

/// <summary>
/// What the server decided this member may do with automation.
/// <para>
/// Every field is a boolean the server put here. There is deliberately no method on this type that
/// takes a role, an account state or a subscription tier and returns one — the same shape the
/// frontend's <c>lib/automation/access.ts</c> holds itself to, and for the same reason: it makes §49
/// a property of the code rather than a review item.
/// </para>
/// </summary>
public sealed record AutomationAccess
{
    public required bool CanViewAutomations { get; init; }

    public required bool CanCreatePersonal { get; init; }

    public required bool CanCreateForCommunity { get; init; }

    public required bool CanCreateForOrganization { get; init; }

    public required bool CanCreateForCreator { get; init; }

    public required bool CanCreateForBusiness { get; init; }

    public required bool CanCreatePlatformWide { get; init; }

    public required bool CanNotifyOtherMembers { get; init; }

    public required bool CanUseFinancialTriggers { get; init; }

    public required bool CanUseLocationTriggers { get; init; }

    public required bool CanViewOthersRuns { get; init; }

    public required bool CanManageOthersAutomations { get; init; }

    /// <summary>Rendered before a member hits it, not after.</summary>
    public required int? MaxActiveAutomations { get; init; }

    /// <summary>How many they already have, so the client can say "43 of 50".</summary>
    public required int ActiveAutomations { get; init; }

    /// <summary><c>server</c> always. The client's <c>unavailable</c> is its own failure mode.</summary>
    public required string Provenance { get; init; }

    public required IReadOnlyList<string> GrantedCapabilities { get; init; }

    /// <summary>
    /// Whether an action that reaches other people would reach anyone in the scope that was asked
    /// about. False for a community that is not open, or one that is suspended or closed.
    /// </summary>
    public required bool CanReachMembers { get; init; }

    /// <summary>
    /// Why it would reach nobody, in the scope owner's own words, or null when it would.
    /// <para>
    /// <b>Served so a builder can say this before the rule is turned on.</b> The dispatcher has
    /// always read it and recorded it as the reason an action did nothing, which means the member
    /// found out after the fact, from a run record — the sentence existed and reached nobody.
    /// </para>
    /// </summary>
    public string? ReachRefusal { get; init; }

    /// <summary>Nothing allowed. The safe default whenever nothing has said otherwise.</summary>
    public static AutomationAccess None() => new()
    {
        CanViewAutomations = false,
        CanCreatePersonal = false,
        CanCreateForCommunity = false,
        CanCreateForOrganization = false,
        CanCreateForCreator = false,
        CanCreateForBusiness = false,
        CanCreatePlatformWide = false,
        CanNotifyOtherMembers = false,
        CanUseFinancialTriggers = false,
        CanUseLocationTriggers = false,
        CanViewOthersRuns = false,
        CanManageOthersAutomations = false,
        MaxActiveAutomations = null,
        ActiveAutomations = 0,
        Provenance = "unavailable",
        GrantedCapabilities = [],

        // Nothing is reachable when nothing has been granted, and there is no scope to explain.
        CanReachMembers = false,
        ReachRefusal = null
    };
}

/// <summary>
/// Decides what a member may do, and refuses what they may not.
/// <para>
/// <b>The capability set is advisory to the client and binding on the server.</b> Every write calls
/// <see cref="AuthorizeScopeAsync"/> again rather than trusting a set the client cached — a capability
/// set the write path believed would be a permission the client could hold past its revocation.
/// </para>
/// <para>
/// <b>Platform-wide is never granted.</b> There is no path in this class that sets
/// <c>CanCreatePlatformWide</c> to true, which is §66's security requirement expressed as an absence
/// rather than as a check: a normal member cannot create platform-wide automation because nothing here
/// can produce the grant, not because something here refuses it.
/// </para>
/// </summary>
public sealed class AutomationAccessService
{
    private readonly AutomationScopeAuthorityResolver _scopes;
    private readonly IAutomationRepository _automations;
    private readonly ICurrentUser _currentUser;
    private readonly IOptions<AutomationOptions> _options;

    public AutomationAccessService(
        AutomationScopeAuthorityResolver scopes,
        IAutomationRepository automations,
        ICurrentUser currentUser,
        IOptions<AutomationOptions> options)
    {
        _scopes = scopes;
        _automations = automations;
        _currentUser = currentUser;
        _options = options;
    }

    /// <summary>The signed-in member, or a refusal.</summary>
    public Result<UserId> RequireMember() =>
        _currentUser.UserId is { } id
            ? Result<UserId>.Success(id)
            : Result<UserId>.Failure(AutomationErrors.NotAuthenticated());

    /// <summary>
    /// What this member may do, for a scope.
    /// <para>
    /// The scope defaults to the member themselves. A caller asking about somebody else's scope gets
    /// the answer for that scope, which is <c>None</c> unless a module says otherwise — and never gets
    /// somebody else's <i>personal</i> capability set, because there is no read-another-member variant
    /// and the subject is always the token's.
    /// </para>
    /// </summary>
    public async Task<Result<AutomationAccess>> DescribeAsync(
        AutomationOwnerKind? ownerKind, Guid? ownerId, CancellationToken cancellationToken = default)
    {
        var member = RequireMember();

        if (member.IsFailure)
        {
            return Result<AutomationAccess>.Failure(member.Error);
        }

        var kind = ownerKind ?? AutomationOwnerKind.User;
        var id = ownerId ?? member.Value.Value;

        var grant = await _scopes.For(kind).EvaluateAsync(member.Value, kind, id, cancellationToken);

        var personal = await _scopes.For(AutomationOwnerKind.User)
            .EvaluateAsync(member.Value, AutomationOwnerKind.User, member.Value.Value, cancellationToken);

        var community = await _scopes.For(AutomationOwnerKind.Community)
            .EvaluateAsync(member.Value, AutomationOwnerKind.Community, id, cancellationToken);

        var organization = await _scopes.For(AutomationOwnerKind.Organization)
            .EvaluateAsync(member.Value, AutomationOwnerKind.Organization, id, cancellationToken);

        var creator = await _scopes.For(AutomationOwnerKind.Creator)
            .EvaluateAsync(member.Value, AutomationOwnerKind.Creator, member.Value.Value, cancellationToken);

        var capabilities = grant.Capabilities
            .Concat(personal.Capabilities)
            .Concat(community.Capabilities)
            .Concat(organization.Capabilities)
            .Concat(creator.Capabilities)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(capability => capability, StringComparer.Ordinal)
            .ToArray();

        var active = await _automations.CountActiveAsync(kind, id, cancellationToken);

        return Result<AutomationAccess>.Success(new AutomationAccess
        {
            CanViewAutomations = true,
            CanCreatePersonal = personal.CanOwn,
            CanCreateForCommunity = community.CanOwn,
            CanCreateForOrganization = organization.CanOwn,
            CanCreateForCreator = creator.CanOwn,
            CanCreateForBusiness = personal.CanOwn,

            // No path to true. §66, expressed as an absence.
            CanCreatePlatformWide = false,

            CanNotifyOtherMembers =
                capabilities.Contains(AutomationCapabilities.Community, StringComparer.Ordinal) ||
                capabilities.Contains(AutomationCapabilities.Organization, StringComparer.Ordinal) ||
                capabilities.Contains(AutomationCapabilities.Creator, StringComparer.Ordinal),

            CanUseFinancialTriggers =
                capabilities.Contains(AutomationCapabilities.Financial, StringComparer.Ordinal),

            // 21 Oct 2026: granted with the member's own scope, now that Location takes an opt-in,
            // foreground-only presence report. Read from the grant rather than written down, so the
            // answer cannot drift from the capability check that actually gates the trigger.
            CanUseLocationTriggers =
                capabilities.Contains(AutomationCapabilities.Location, StringComparer.Ordinal),

            CanViewOthersRuns = grant.CanManage,
            CanManageOthersAutomations = grant.CanManage,
            MaxActiveAutomations = _options.Value.MaximumActiveAutomationsPerOwner,
            ActiveAutomations = active,
            Provenance = "server",
            GrantedCapabilities = capabilities,

            // The grant for the scope that was *asked about*, not the union above: "can this rule
            // reach anyone" is a question about one community, and answering it from the union
            // would say yes because some other scope of this member's is open.
            CanReachMembers = grant.CanReachMembers,
            ReachRefusal = grant.CanReachMembers ? null : grant.ReachRefusal
        });
    }

    /// <summary>
    /// Whether this member may own automations in this scope, and what they may reach.
    /// <para>
    /// <b>A caller naming an owner they have no role on receives 403, not an empty list.</b> The
    /// register is explicit: an empty list is indistinguishable from "there are none" and teaches the
    /// client the wrong thing. That is the one place this module answers 403 rather than 404 — the
    /// caller already knows the community exists.
    /// </para>
    /// </summary>
    public async Task<Result<AutomationScopeGrant>> AuthorizeScopeAsync(
        UserId member,
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        if (ownerKind == AutomationOwnerKind.Platform)
        {
            // Refused before any authority is consulted, so no implementation can grant it by mistake.
            return Result<AutomationScopeGrant>.Failure(
                AutomationErrors.ScopeForbidden("platform"));
        }

        var grant = await _scopes.For(ownerKind)
            .EvaluateAsync(member, ownerKind, ownerId, cancellationToken);

        return grant.CanOwn
            ? Result<AutomationScopeGrant>.Success(grant)
            : Result<AutomationScopeGrant>.Failure(
                AutomationErrors.ScopeForbidden(ownerKind.ToString().ToLowerInvariant()));
    }

    /// <summary>
    /// Whether this member may read or change an automation that already exists.
    /// <para>
    /// Returns <see cref="AutomationErrors.NotFound"/> for both "does not exist" and "not yours",
    /// because a 403 on somebody else's automation id confirms that the id exists — which is how an
    /// enumeration tool is built out of an authorization check.
    /// </para>
    /// </summary>
    public async Task<Result<AutomationScopeGrant>> AuthorizeAutomationAsync(
        UserId member,
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        UserId createdBy,
        bool requireManage,
        CancellationToken cancellationToken = default)
    {
        var grant = await _scopes.For(ownerKind)
            .EvaluateAsync(member, ownerKind, ownerId, cancellationToken);

        if (grant.CanOwn && (!requireManage || grant.CanManage))
        {
            return Result<AutomationScopeGrant>.Success(grant);
        }

        // The creator keeps access to what they wrote even when the scope authority no longer grants
        // them the scope — otherwise a moderator who wrote a rule and stepped down cannot delete it,
        // and nobody else knows it is theirs. They keep read and delete; they do not regain the
        // capability to broadcast through it, because that is checked separately at dispatch.
        if (createdBy == member && !requireManage)
        {
            return Result<AutomationScopeGrant>.Success(
                grant with { CanOwn = true, CanReachMembers = false });
        }

        return Result<AutomationScopeGrant>.Failure(AutomationErrors.NotFound());
    }
}
