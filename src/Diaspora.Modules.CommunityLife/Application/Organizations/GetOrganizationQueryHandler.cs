using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Extensibility.Views;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// One organization, by identifier or by slug.
/// <para>
/// <b>A page nobody may see answers not-found, not forbidden.</b> A draft an administrator has not
/// published yet is not a secret whose existence is worth confirming, and telling a stranger "this
/// exists but you cannot see it" is how somebody learns which bodies are being set up.
/// </para>
/// <para>
/// <b>The view is recorded through the tracker seam and never fails the read.</b> A directory that
/// returned an error because a counter would not move would be a directory that goes down when the
/// analytics do.
/// </para>
/// </summary>
[SideEffecting(Reason = "Records the view that this read caused, through the tracker seam.")]
public sealed class GetOrganizationQueryHandler
    : IQueryHandler<GetOrganizationQuery, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly OrganizationReadContext _read;
    private readonly ICommunityLifeViewTracker _views;
    private readonly IClock _clock;

    public GetOrganizationQueryHandler(
        IOrganizationRepository organizations,
        IOrganizationAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        OrganizationReadContext read,
        ICommunityLifeViewTracker views,
        IClock clock)
    {
        _organizations = organizations;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _views = views;
        _clock = clock;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        GetOrganizationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var organization = await LoadAsync(query, cancellationToken);

        if (organization is null || organization.IsDeleted)
        {
            return Result.Failure<OrganizationResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        if (!await CanReadAsync(organization, cancellationToken))
        {
            return Result.Failure<OrganizationResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        var response = await _read.ToResponseAsync(organization, cancellationToken);

        await RecordViewAsync(organization, cancellationToken);

        return Result.Success(response);
    }

    private async Task<Organization?> LoadAsync(
        GetOrganizationQuery query, CancellationToken cancellationToken)
    {
        if (query.OrganizationId is { } id && id != Guid.Empty)
        {
            return await _organizations.GetAsync(OrganizationId.From(id), cancellationToken);
        }

        return string.IsNullOrWhiteSpace(query.Slug)
            ? null
            : await _organizations.GetBySlugAsync(query.Slug, cancellationToken);
    }

    /// <summary>
    /// Whether the caller may see this page at all.
    /// <para>
    /// A published or active body is public. Anything else — a draft, an archived page, one a
    /// moderator has hidden — resolves only for somebody who could write to it, or for staff
    /// reviewing it: a moderator who cannot open a page they hid cannot decide whether to lift it.
    /// </para>
    /// </summary>
    private async Task<bool> CanReadAsync(
        Organization organization, CancellationToken cancellationToken)
    {
        if (organization is { Status: OrganizationStatus.Published or OrganizationStatus.Active }
            && organization.ModerationState is CommunityLifeModerationState.Clear
                or CommunityLifeModerationState.Flagged
            && organization.Visibility != OrganizationVisibility.Private)
        {
            return true;
        }

        var manager = await _authorization.RequireOrganizationManagementReadAsync(
            organization, cancellationToken);

        return manager.IsSuccess;
    }

    /// <summary>
    /// Records the visit, unless the caller is looking at their own page.
    /// <para>
    /// An administrator refreshing their own body forty times while editing it would otherwise make
    /// it the most-viewed organization in the city. Staff are excluded for the same reason.
    /// </para>
    /// <para>
    /// The uniqueness decision belongs to the tracker seam, which holds the deduplication window a
    /// deployment configures. This handler only asks and records what it is told.
    /// </para>
    /// </summary>
    private async Task RecordViewAsync(
        Organization organization, CancellationToken cancellationToken)
    {
        if (_authorization.IsStaff
            || organization.CreatedByUserId == _authorization.CallerId)
        {
            return;
        }

        var isUnique = _views.RecordView(
            organization.Id.Value, ViewerKey());

        var analytics = await _analytics.FindByOrganizationAsync(
            organization.Id, cancellationToken);

        if (analytics is null)
        {
            return;
        }

        analytics.RecordView(isUnique, _clock.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// What the tracker deduplicates on.
    /// <para>
    /// An anonymous reader is one bucket rather than none: without a key every anonymous visit
    /// would count as unique, and the unique-view figure on a public page would just be the total.
    /// </para>
    /// </summary>
    private string ViewerKey() =>
        _authorization.CallerId?.Value.ToString() ?? "anonymous";
}
