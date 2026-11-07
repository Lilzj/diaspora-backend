using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Community;

/// <summary>
/// Makes an organization page reportable through B6.
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
/// <para>
/// <b>Describes only; it no longer moderates (7 Nov 2026).</b> Its moderation hook hid or suspended
/// the page on a Community decision with nobody recorded as the moderator. Staff now act on an
/// organisation only through its Trust case, carried out by <c>OrganizationModerationTarget</c>,
/// which also names the organisation's Owner member rather than its creator.
/// </para>
/// </summary>
public sealed class OrganizationContentSource : ICommunityContentSource
{
    private readonly IOrganizationRepository _organizations;

    public OrganizationContentSource(IOrganizationRepository organizations) =>
        _organizations = organizations;

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
