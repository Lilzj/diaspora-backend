using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Community;

/// <summary>
/// Makes an organization page reportable and moderatable through B6.
/// <para>
/// <b>The companion to <c>CommunityEventContentSource</c>, and it was missing.</b> B12 registered a
/// source for its events and never wrote one for its organizations, so
/// <see cref="CommunityContentType.Organization"/> sat in B6's enum unclaimed and
/// <c>POST /community/reports</c> answered <c>community.content.type-unsupported</c> for every
/// association, church, savings group and hometown union on the platform.
/// </para>
/// <para>
/// <b>Why that matters more than it sounds.</b> An organization page is where money is collected —
/// membership dues, campaign donations, event tickets. A page impersonating a real association is
/// the highest-value fraud this product can carry, and until now the only route to raising it was
/// to report one of the organization's posts, which moves the complaint rather than the target.
/// </para>
/// <para>
/// <b>Reportable, not commentable.</b> Discussion about an organization belongs on its posts and its
/// events, both of which already carry threads. A second thread on the page itself would split the
/// conversation and give a page's own moderators a surface they have no tools for.
/// </para>
/// </summary>
public sealed class OrganizationContentSource : ICommunityContentSource
{
    private readonly IOrganizationRepository _organizations;
    private readonly IClock _clock;

    public OrganizationContentSource(IOrganizationRepository organizations, IClock clock)
    {
        _organizations = organizations;
        _clock = clock;
    }

    public CommunityContentType ContentType => CommunityContentType.Organization;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        var organization = await _organizations.GetIncludingDeletedAsync(
            OrganizationId.From(contentId), cancellationToken);

        return organization is null ? null : Describe(organization);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        var described = new Dictionary<Guid, ContentDescriptor>();

        if (contentIds.Count == 0)
        {
            return described;
        }

        var organizations = await _organizations.FindManyAsync(
            [.. contentIds.Where(id => id != Guid.Empty).Distinct().Select(OrganizationId.From)],
            cancellationToken);

        foreach (var organization in organizations)
        {
            described[organization.Id.Value] = Describe(organization);
        }

        return described;
    }

    /// <summary>
    /// Applies a moderation outcome to the organization page itself.
    /// <para>
    /// Hiding the page is the whole point of upholding a report about an impersonation, so this is
    /// implemented rather than left at the interface default. Pin, Unpin and Archive deliberately do
    /// nothing: none of them says anything about an organization's lifecycle, and an association's
    /// page should not go dark on a decision that did not ask for that.
    /// </para>
    /// <para>
    /// The caller owns the unit of work — this mutates the aggregate and does not save.
    /// </para>
    /// </summary>
    public async Task ApplyModerationAsync(
        Guid contentId,
        ModerationActionType action,
        CancellationToken cancellationToken = default)
    {
        var organization = await _organizations.GetAsync(
            OrganizationId.From(contentId), cancellationToken);

        if (organization is null)
        {
            return;
        }

        var now = _clock.UtcNow;

        switch (action)
        {
            case ModerationActionType.Hide:
            case ModerationActionType.Reject:
                organization.Hide("Hidden following a community report.", null, now);
                break;

            case ModerationActionType.Suspend:
                organization.Suspend("Suspended following a community report.", null, now);
                break;

            case ModerationActionType.Approve:
            case ModerationActionType.Restore:
                organization.Clear(null, now);
                break;

            default:
                // Pin, Unpin and Archive say nothing about an organization's lifecycle. B6 has
                // recorded the decision; there is nothing for B12 to do.
                break;
        }
    }

    private static ContentDescriptor Describe(Organization organization) => new()
    {
        Reference = ContentReference.Create(
            CommunityContentType.Organization, organization.Id.Value),

        // The member who created the page. B6's model is about people, and an organization is
        // answerable through whoever registered it.
        AuthorId = organization.CreatedByUserId.Value,
        Title = organization.Name.Value,
        Exists = !organization.IsDeleted,
        Visibility = ToCommunityVisibility(organization),
        CountryCode = organization.Location.CountryCode,

        // See the class note. Discussion belongs on the organization's posts and events.
        AcceptsComments = false
    };

    /// <summary>
    /// Projects B12's audience onto B6's, narrowing wherever the two do not line up.
    /// <para>
    /// A page that has not been published — draft or awaiting review — is private whatever its
    /// visibility says, because the status is the stronger statement. B6 has no <c>Unlisted</c>, so
    /// a link-shared page maps to members-only rather than to public.
    /// </para>
    /// </summary>
    private static VisibilityLevel ToCommunityVisibility(Organization organization)
    {
        if (organization.Status is OrganizationStatus.Draft or OrganizationStatus.Submitted)
        {
            return VisibilityLevel.Private;
        }

        return organization.Visibility switch
        {
            OrganizationVisibility.Public => VisibilityLevel.Public,
            OrganizationVisibility.MembersOnly => VisibilityLevel.Members,
            OrganizationVisibility.Unlisted => VisibilityLevel.Members,
            _ => VisibilityLevel.Private
        };
    }
}
