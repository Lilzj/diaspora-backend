using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Services;

/// <summary>
/// The one door to an organization-scoped act.
/// <para>
/// <b>Every write in this module comes through here, and that is what makes GAP-013 the keystone it
/// is.</b> The permission catalogue answers the first question — may this person run a programme at
/// all — and it cannot answer the second: whether they may run <em>this</em> one is a fact about a
/// role in one organization, and no token carries it. Nine of the register's gaps are gated on that
/// answer, so it is resolved in one place rather than nine.
/// </para>
/// <para>
/// <b>The role comes from B12, and this module does not keep a copy.</b> B12 owns the
/// <c>(member, organization, role)</c> edge and has since it shipped; asking its published directory
/// on every request means a member promoted in B12 is promoted here in the same instant, and there is
/// no second roll to drift.
/// </para>
/// <para>
/// <b>Staff bypass is deliberate and narrow, and there is none on a member roll.</b> A moderator may
/// read a poll's results or an announcement they are not addressed by, because moderation would
/// otherwise require belonging to everything they touch. Reading who belongs to a named organization
/// — a support group, a political association, a faith community — is a different act, and it should
/// require a deliberate support tool rather than fall out of a role check in a general-purpose
/// helper. B9, B10, B11 and B12 all draw the same line.
/// </para>
/// </summary>
public sealed class CommunityProgrammeAccessService
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Moderator", "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;
    private readonly IOrganizationDirectory _organizations;

    /// <summary>
    /// Per-request memoisation of the capability lookup.
    /// <para>
    /// A dashboard read asks about one organization up to a dozen times — once for the counts, once
    /// per section — and each ask is a database round trip through B12. The cache lives for one
    /// request, so a role changed mid-request is the one thing it cannot see, and a role changed
    /// mid-request is a role that was changed by another request.
    /// </para>
    /// </summary>
    private readonly Dictionary<Guid, OrganizationCapabilities> _resolved = [];

    public CommunityProgrammeAccessService(
        ICurrentUser currentUser, IOrganizationDirectory organizations)
    {
        _currentUser = currentUser;
        _organizations = organizations;
    }

    /// <summary>The caller, or null when the request is anonymous.</summary>
    public UserId? CallerId => _currentUser.UserId;

    public bool IsAuthenticated => _currentUser.UserId is not null;

    /// <summary>Whether the caller holds a platform staff role.</summary>
    public bool IsStaff => _currentUser.Role is { } role && StaffRoles.Contains(role);

    /// <summary>
    /// Admin or SuperAdmin — not a platform Moderator.
    /// <para>
    /// Narrower than <see cref="IsStaff"/> on purpose. Moderating content in a community one does not
    /// belong to is a staff act; opening a community into the directory, or making somebody the owner
    /// of "Nigerian Diaspora", is an administrative one, and a moderator reviewing reports should not
    /// be able to hand out communities as a side effect.
    /// </para>
    /// </summary>
    public bool IsPlatformAdministrator =>
        _currentUser.Role is { } role
        && (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase));

    /// <summary>The caller, or a failure. The first line of every write handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(CommunityProgrammeErrors.NotAuthenticated);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Capabilities
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// What the caller may do inside one organization.
    /// <para>
    /// <see cref="OrganizationCapabilities.None"/> for an anonymous caller, for a caller with no
    /// role, and for an organization that does not resolve. Failing closed is the only safe reading:
    /// a capability model whose failure mode is "grant" is not one.
    /// </para>
    /// </summary>
    public async Task<OrganizationCapabilities> CapabilitiesAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || _currentUser.UserId is not { } userId)
        {
            return OrganizationCapabilities.None(organizationId);
        }

        if (_resolved.TryGetValue(organizationId, out var cached))
        {
            return cached;
        }

        var membership = await _organizations.FindMembershipAsync(
            organizationId, userId.Value, cancellationToken);

        var capabilities = OrganizationCapabilityMap.For(organizationId, membership?.Role);

        _resolved[organizationId] = capabilities;

        return capabilities;
    }

    /// <summary>
    /// Refuses unless the caller holds a capability in an organization.
    /// <para>
    /// <b>403 rather than a filtered response.</b> The register is explicit: "refuse with 403 rather
    /// than filtering — a hidden button is a convenience, not a control". A caller who tampers with
    /// the client gains nothing, because the refusal is here and not in the rendering.
    /// </para>
    /// </summary>
    public async Task<Result> RequireCapabilityAsync(
        Guid organizationId,
        OrganizationCapability capability,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure(CommunityProgrammeErrors.NotAuthenticated);
        }

        var capabilities = await CapabilitiesAsync(organizationId, cancellationToken);

        return capabilities.Grants(capability)
            ? Result.Success()
            : Result.Failure(CommunityProgrammeErrors.CapabilityRequired(Describe(capability)));
    }

    /// <summary>Whether the caller holds a capability, without producing a failure.</summary>
    public async Task<bool> HoldsAsync(
        Guid organizationId,
        OrganizationCapability capability,
        CancellationToken cancellationToken = default) =>
        (await CapabilitiesAsync(organizationId, cancellationToken)).Grants(capability);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Membership of a body
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The organizations the caller currently belongs to, in any role.
    /// <para>
    /// Resolved once per request and reused. Three of this module's list reads need it — the poll
    /// list, the announcement feed and the programme discovery page — and each of them applies it
    /// inside the query rather than filtering the result, so a private row never enters the set.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Guid>> MemberOfAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return [];
        }

        if (_memberOf is not null)
        {
            return _memberOf;
        }

        var memberships = await _organizations.FindMembershipsForMemberAsync(
            userId.Value, limit: 100, cancellationToken);

        // Prime the capability cache from the same round trip. Without this a dashboard picker
        // resolves the caller's memberships and then asks B12 again, once per organization, for a
        // role it already has in hand.
        foreach (var membership in memberships)
        {
            _resolved[membership.OrganizationId] =
                OrganizationCapabilityMap.For(membership.OrganizationId, membership.Role);
        }

        _memberOf = [.. memberships.Select(membership => membership.OrganizationId)];

        return _memberOf;
    }

    /// <summary>The subset of the caller's organizations where they hold a given capability.</summary>
    public async Task<IReadOnlyList<Guid>> ManagedForAsync(
        OrganizationCapability capability, CancellationToken cancellationToken = default)
    {
        var memberOf = await MemberOfAsync(cancellationToken);

        if (memberOf.Count == 0)
        {
            return [];
        }

        var managed = new List<Guid>();

        foreach (var organizationId in memberOf)
        {
            if ((await CapabilitiesAsync(organizationId, cancellationToken)).Grants(capability))
            {
                managed.Add(organizationId);
            }
        }

        return managed;
    }

    /// <summary>One organization's card, read through B12. Null when it does not resolve.</summary>
    public Task<OrganizationSummary?> FindOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        _organizations.FindAsync(organizationId, cancellationToken);

    /// <summary>Several at once. The N+1 guard for any list whose rows each name an organization.</summary>
    public Task<IReadOnlyDictionary<Guid, OrganizationSummary>> FindOrganizationsAsync(
        IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken = default) =>
        _organizations.FindManyAsync(organizationIds, cancellationToken);

    /// <summary>The caller's own membership rows in B12, with their roles.</summary>
    public async Task<IReadOnlyList<OrganizationMembershipSummary>> MyMembershipsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return [];
        }

        return await _organizations.FindMembershipsForMemberAsync(
            userId.Value, limit: 100, cancellationToken);
    }

    /// <summary>One member's B12 standing in one organization.</summary>
    public Task<OrganizationMembershipSummary?> FindOrganizationMembershipAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
        _organizations.FindMembershipAsync(organizationId, userId, cancellationToken);

    private IReadOnlyList<Guid>? _memberOf;

    /// <summary>
    /// The capability's name as a member would ask for it.
    /// <para>
    /// Named rather than enumerated, because a refusal reading "you need ManageMembers" is a refusal
    /// nobody can act on — and the role that grants it differs between organizations.
    /// </para>
    /// </summary>
    private static string Describe(OrganizationCapability capability) => capability switch
    {
        OrganizationCapability.ManageMembers => "managing members",
        OrganizationCapability.ApproveMembers => "approving members",
        OrganizationCapability.ManageTiers => "editing membership tiers",
        OrganizationCapability.ManageEvents => "managing events",
        OrganizationCapability.ManageCampaigns => "managing fundraising",
        OrganizationCapability.ManageVolunteers => "managing volunteers",
        OrganizationCapability.ManagePolls => "managing polls",
        OrganizationCapability.PublishAnnouncements => "publishing announcements",
        OrganizationCapability.ViewAnalytics => "viewing analytics",
        OrganizationCapability.ViewFinancials => "viewing financial figures",
        _ => "that"
    };
}
