using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.Modules.CommunityProgramme.Application.Mapping;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Memberships;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// COMMUNITY-LIFE-GAP-001 — community membership tiers and benefits.
//
// Three reads. The organization behind each is B12's and is read through its directory rather than
// copied; the tiers, the prices and the requirements are this module's.
//
// Two rules run through all three:
//
//   A private or invite-only programme is ABSENT for a caller who may not see it, not present with a
//   flag. The register is explicit that "a client-side filter would mean the private group had
//   already been sent to the device", and the visibility predicate therefore lives in the query.
//
//   Every requirement carries the server's own evaluation of whether the caller meets it, made on
//   this request. The client renders it; §19 forbids it computing one.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One organization's membership programme, with the caller's own standing against it.
/// <para>
/// <b><c>myStatus</c> is one of fourteen, and three of them are never stored.</b> A caller with no
/// membership row gets <c>NotAMember</c> when the programme has no requirements, <c>Eligible</c> when
/// they meet every requirement of at least one tier, and <c>Ineligible</c> when they do not — a
/// distinction the client renders as three different screens and which only the server can make.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record GetMembershipProgrammeQuery : IQuery<MembershipProgrammeResponse>
{
    public required Guid OrganizationId { get; init; }
}

public sealed class GetMembershipProgrammeQueryHandler
    : IQueryHandler<GetMembershipProgrammeQuery, MembershipProgrammeResponse>
{
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly MembershipEligibilityEvaluator _eligibility;

    public GetMembershipProgrammeQueryHandler(
        IMembershipProgrammeRepository programmes,
        IProgrammeMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        MembershipEligibilityEvaluator eligibility)
    {
        _programmes = programmes;
        _memberships = memberships;
        _access = access;
        _eligibility = eligibility;
    }

    public async Task<Result<MembershipProgrammeResponse>> Handle(
        GetMembershipProgrammeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var programme = await _programmes.FindByOrganizationAsync(
            query.OrganizationId, cancellationToken);

        if (programme is null)
        {
            return Result.Failure<MembershipProgrammeResponse>(
                CommunityProgrammeErrors.ProgrammeNotFound);
        }

        var organization = await _access.FindOrganizationAsync(
            query.OrganizationId, cancellationToken);

        if (organization is null)
        {
            return Result.Failure<MembershipProgrammeResponse>(
                CommunityProgrammeErrors.OrganizationNotFound);
        }

        // "Not visible to you" and "does not exist" answer alike, deliberately. A caller probing for
        // a private group learns nothing from a 404 they did not already know.
        if (programme.Visibility is ProgrammeVisibility.Private or ProgrammeVisibility.InviteOnly)
        {
            var memberOf = await _access.MemberOfAsync(cancellationToken);

            if (!memberOf.Contains(query.OrganizationId))
            {
                return Result.Failure<MembershipProgrammeResponse>(
                    CommunityProgrammeErrors.ProgrammeNotFound);
            }
        }

        var membership = _access.CallerId is { } caller
            ? await _memberships.FindCurrentAsync(query.OrganizationId, caller, cancellationToken)
            : null;

        var holdsInvitation = await HoldsInvitationAsync(query.OrganizationId, cancellationToken);

        bool IsMet(TierRequirement requirement) => _eligibility.IsMet(requirement, holdsInvitation);

        var status = ResolveStatus(programme, membership, IsMet);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            programme, organization, IsMet, status, membership?.Id.Value));
    }

    private async Task<bool> HoldsInvitationAsync(
        Guid organizationId, CancellationToken cancellationToken)
    {
        if (_access.CallerId is not { } caller)
        {
            return false;
        }

        // An invitation is B12's — its membership edge carries `Invited` as a status — so the
        // question is asked there rather than modelled a second time here.
        var membership = await _access.FindOrganizationMembershipAsync(
            organizationId, caller.Value, cancellationToken);

        return membership is not null;
    }

    /// <summary>
    /// The caller's own state against a programme.
    /// <para>
    /// A stored membership answers for itself. A caller with none gets one of the three states that
    /// are never stored, and which of the three depends on whether any tier's requirements are all
    /// met — which is the whole reason <c>Eligible</c> and <c>Ineligible</c> exist as separate
    /// values.
    /// </para>
    /// </summary>
    private static ProgrammeMembershipStatus ResolveStatus(
        MembershipProgramme programme,
        ProgrammeMembership? membership,
        Func<TierRequirement, bool> isMet)
    {
        if (membership is not null && !ProgrammeMembershipTransitions.AllowsReapplication(
            membership.Status))
        {
            return membership.Status;
        }

        if (programme.Tiers.Count == 0)
        {
            return ProgrammeMembershipStatus.NotAMember;
        }

        var anyTierOpen = programme.Tiers.Any(tier =>
            !tier.IsFull && tier.Requirements.All(isMet));

        return anyTierOpen
            ? ProgrammeMembershipStatus.Eligible
            : ProgrammeMembershipStatus.Ineligible;
    }
}

/// <summary>An organization's tiers on their own, for a screen that only needs the prices.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListMembershipTiersQuery : IQuery<IReadOnlyList<MembershipTierResponse>>
{
    public required Guid OrganizationId { get; init; }
}

public sealed class ListMembershipTiersQueryHandler
    : IQueryHandler<ListMembershipTiersQuery, IReadOnlyList<MembershipTierResponse>>
{
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly CommunityProgrammeAccessService _access;
    private readonly MembershipEligibilityEvaluator _eligibility;

    public ListMembershipTiersQueryHandler(
        IMembershipProgrammeRepository programmes,
        CommunityProgrammeAccessService access,
        MembershipEligibilityEvaluator eligibility)
    {
        _programmes = programmes;
        _access = access;
        _eligibility = eligibility;
    }

    public async Task<Result<IReadOnlyList<MembershipTierResponse>>> Handle(
        ListMembershipTiersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var programme = await _programmes.FindByOrganizationAsync(
            query.OrganizationId, cancellationToken);

        if (programme is null)
        {
            return Result.Failure<IReadOnlyList<MembershipTierResponse>>(
                CommunityProgrammeErrors.ProgrammeNotFound);
        }

        if (programme.Visibility is ProgrammeVisibility.Private or ProgrammeVisibility.InviteOnly)
        {
            var memberOf = await _access.MemberOfAsync(cancellationToken);

            if (!memberOf.Contains(query.OrganizationId))
            {
                return Result.Failure<IReadOnlyList<MembershipTierResponse>>(
                    CommunityProgrammeErrors.ProgrammeNotFound);
            }
        }

        bool IsMet(TierRequirement requirement) => _eligibility.IsMet(requirement, false);

        return Result.Success<IReadOnlyList<MembershipTierResponse>>(
        [
            .. programme.Tiers.Select(tier =>
                CommunityProgrammeMapper.ToResponse(tier, programme.OrganizationId, IsMet))
        ]);
    }
}

/// <summary>
/// The membership discovery page.
/// <para>
/// <b>The text and place filters are B12's, and this module does not reimplement them.</b> A search
/// for "Nigerian association in Manchester" is a question about organizations, which B12's directory
/// already answers with its own indexes and its own visibility rules. This read narrows that answer
/// to the bodies that run a programme, applies the price band and the eligibility filter, and pages
/// the result — which is the part B12 cannot do because it does not know what a tier is.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record DiscoverMembershipsQuery
    : IQuery<CommunityPageResponse<MembershipProgrammeResponse>>
{
    public string? Query { get; init; }

    public string? Category { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>
    /// A B4 administrative area.
    /// <para>
    /// The register names this filter <c>neighborhood</c> and B12 already carries the area on its
    /// organization summary, so it is servable. Its sibling <c>maxDistanceKm</c> is not: the summary
    /// carries no coordinate, and a proximity filter would mean B18 growing one — see the Wave 5
    /// report's documented limits.
    /// </para>
    /// </summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary><c>any</c>, <c>free</c> or <c>paid</c>.</summary>
    public string? PriceBand { get; init; }

    /// <summary>Only programmes the caller currently satisfies the requirements of.</summary>
    public bool EligibleOnly { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

public sealed class DiscoverMembershipsQueryHandler
    : IQueryHandler<DiscoverMembershipsQuery, CommunityPageResponse<MembershipProgrammeResponse>>
{
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly MembershipEligibilityEvaluator _eligibility;

    public DiscoverMembershipsQueryHandler(
        IMembershipProgrammeRepository programmes,
        IProgrammeMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        MembershipEligibilityEvaluator eligibility)
    {
        _programmes = programmes;
        _memberships = memberships;
        _access = access;
        _eligibility = eligibility;
    }

    public async Task<Result<CommunityPageResponse<MembershipProgrammeResponse>>> Handle(
        DiscoverMembershipsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memberOf = await _access.MemberOfAsync(cancellationToken);

        var page = await _programmes.SearchAsync(
            new ProgrammeQueryFilter
            {
                VisibleOrganizationIds = memberOf,
                PriceBand = query.PriceBand,
                OpenOnly = true,
                Cursor = query.Cursor,
                Limit = CommunityProgrammeLimits.PageSize(query.Take)
            },
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result.Success(Empty());
        }

        var organizations = await _access.FindOrganizationsAsync(
            [.. page.Items.Select(programme => programme.OrganizationId)], cancellationToken);

        var caller = _access.CallerId;

        var mine = caller is null
            ? []
            : (await _memberships.FindForMemberAsync(caller, 200, cancellationToken))
                .ToDictionary(membership => membership.OrganizationId);

        var rows = new List<MembershipProgrammeResponse>();

        foreach (var programme in page.Items)
        {
            if (!organizations.TryGetValue(programme.OrganizationId, out var organization))
            {
                // The body is no longer discoverable. Its programme goes with it: rendering a
                // membership of an organization nobody can open is a dead end on a screen.
                continue;
            }

            if (!MatchesOrganization(organization, query))
            {
                continue;
            }

            bool IsMet(TierRequirement requirement) => _eligibility.IsMet(requirement, false);

            var eligible = programme.Tiers.Any(tier =>
                !tier.IsFull && tier.Requirements.All(IsMet));

            if (query.EligibleOnly && !eligible)
            {
                continue;
            }

            mine.TryGetValue(programme.OrganizationId, out var membership);

            var status = membership is not null
                && !ProgrammeMembershipTransitions.AllowsReapplication(membership.Status)
                    ? membership.Status
                    : eligible
                        ? ProgrammeMembershipStatus.Eligible
                        : ProgrammeMembershipStatus.Ineligible;

            rows.Add(CommunityProgrammeMapper.ToResponse(
                programme, organization, IsMet, status, membership?.Id.Value));
        }

        return Result.Success(new CommunityPageResponse<MembershipProgrammeResponse>
        {
            Items = rows,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }

    /// <summary>
    /// The B12-side filters, applied to the summary rather than in the programme query.
    /// <para>
    /// This module stores no organization name, category or place — the register's own rule that
    /// there is one organization system — so the only place those can be matched is on the summary
    /// B12 hands back. The trade is that a heavily filtered discovery page reads more rows than it
    /// serves; the page is bounded by the cursor, so the cost is bounded with it.
    /// </para>
    /// </summary>
    private static bool MatchesOrganization(
        OrganizationSummary organization, DiscoverMembershipsQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Query)
            && !organization.Name.Contains(query.Query.Trim(), StringComparison.OrdinalIgnoreCase)
            && !(organization.Summary?.Contains(
                query.Query.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Category)
            && !string.Equals(query.Category, "All", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                organization.OrganizationTypeKey, query.Category, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode)
            && !string.Equals(
                organization.CountryCode, query.CountryCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.CityName)
            && !string.Equals(
                organization.CityName, query.CityName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // A catalogue neighbourhood id no longer narrows (14 Oct 2026): organizations stopped
        // carrying one when the platform began retiring its place catalogue, so matching on it
        // would hide every body created since. The town name above is the narrowing that remains.
        return true;
    }

    private static CommunityPageResponse<MembershipProgrammeResponse> Empty() =>
        new() { Items = [], HasMore = false };
}
