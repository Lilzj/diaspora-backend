using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Integration;

/// <summary>
/// What other modules may ask B12 about an organization.
/// <para>
/// <b>Flat primitives, and nothing a consumer could write through.</b> The directory returns a
/// summary rather than the aggregate, so no other module can move an organization's lifecycle,
/// membership or trust mark — a body's state is changed by B12's own commands or not at all.
/// </para>
/// <para>
/// <b><c>CanActOnBehalfAsync</c> takes the role as a string, deliberately.</b> A consumer that had to
/// reference this module's role enum to ask a permission question would be coupled to B12's
/// vocabulary; an unrecognised name is refused rather than guessed at, so a typo denies rather than
/// silently grants.
/// </para>
/// </summary>
public sealed class OrganizationDirectory : IOrganizationDirectory
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;

    public OrganizationDirectory(
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships)
    {
        _organizations = organizations;
        _memberships = memberships;
    }

    public async Task<OrganizationSummary?> FindAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            return null;
        }

        var organization = await _organizations.GetAsync(
            OrganizationId.From(organizationId), cancellationToken);

        return organization is null ? null : OrganizationMapper.ToPublished(organization);
    }

    public async Task<OrganizationSummary?> FindBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var organization = await _organizations.GetBySlugAsync(slug, cancellationToken);

        // A deleted row resolves by slug so a stale link can say "this has gone", but the directory
        // is a data feed rather than a page and answers null.
        return organization is null || organization.IsDeleted
            ? null
            : OrganizationMapper.ToPublished(organization);
    }

    public async Task<IReadOnlyDictionary<Guid, OrganizationSummary>> FindManyAsync(
        IReadOnlyCollection<Guid> organizationIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationIds);

        if (organizationIds.Count == 0)
        {
            return new Dictionary<Guid, OrganizationSummary>();
        }

        var organizations = await _organizations.FindManyAsync(
            [.. organizationIds.Where(id => id != Guid.Empty).Distinct()
                .Select(OrganizationId.From)],
            cancellationToken);

        return organizations.ToDictionary(
            organization => organization.Id.Value, OrganizationMapper.ToPublished);
    }

    public async Task<bool> CanActOnBehalfAsync(
        Guid organizationId,
        Guid userId,
        string minimumRole,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            return false;
        }

        if (!Enum.TryParse<MembershipRole>(minimumRole, ignoreCase: true, out var role))
        {
            // An unrecognised role denies. See the class summary.
            return false;
        }

        var membership = await _memberships.FindCurrentAsync(
            OrganizationId.From(organizationId), UserId.From(userId), cancellationToken);

        return membership?.HasAtLeast(role) == true;
    }

    public async Task<IReadOnlyList<OrganizationSummary>> FindForMemberAsync(
        Guid userId, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var memberships = await _memberships.FindCurrentForMemberAsync(
            UserId.From(userId), Math.Clamp(limit, 1, 100), cancellationToken);

        if (memberships.Count == 0)
        {
            return [];
        }

        var organizations = await _organizations.FindManyAsync(
            [.. memberships.Select(membership => membership.OrganizationId).Distinct()],
            cancellationToken);

        return [.. organizations.Select(OrganizationMapper.ToPublished)];
    }

    public async Task<OrganizationMembershipSummary?> FindMembershipAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        var membership = await _memberships.FindCurrentAsync(
            OrganizationId.From(organizationId), UserId.From(userId), cancellationToken);

        return membership is null ? null : ToSummary(membership);
    }

    public async Task<IReadOnlyList<OrganizationMembershipSummary>> FindMembershipsForMemberAsync(
        Guid userId, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var memberships = await _memberships.FindCurrentForMemberAsync(
            UserId.From(userId), Math.Clamp(limit, 1, 100), cancellationToken);

        return [.. memberships.Select(ToSummary)];
    }

    private static OrganizationMembershipSummary ToSummary(OrganizationMembership membership) =>
        new()
        {
            MembershipId = membership.Id.Value,
            OrganizationId = membership.OrganizationId.Value,
            MemberUserId = membership.MemberUserId.Value,
            Role = membership.Role.ToString(),
            Status = membership.Status.ToString(),
            JoinedAt = membership.JoinedAt
        };
}
