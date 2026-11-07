using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;

// The kernel's legacy identifier bag also declares a GuideId. The module-local identifier is the
// authoritative one for B8; the alias resolves the collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Infrastructure.Community;

/// <summary>
/// Makes guides commentable, reactable, reportable and bookmarkable, through B6.
/// <para>
/// <b>This one class is the whole of what B8 pays to get Community's social surface.</b> B6's own
/// content-type documentation names this case explicitly — "Guides (B8) becomes commentable by
/// registering a content source, not by growing its own comment table" — so
/// <see cref="CommunityContentType.Guide"/> is claimed rather than invented. Registering it means
/// comments, reports and bookmarks on a guide all work, and B8 wrote no social code at all.
/// </para>
/// <para>
/// <b>Visibility is narrowed, never widened.</b> B6 takes the descriptor's audience as a ceiling,
/// so an unlisted or private guide must not be described as public — that would let a comment on
/// it surface in a feed with no business showing it. A guide that is not published is not
/// visible to anyone but its author whatever its declared visibility says, so drafts describe
/// themselves as private.
/// </para>
/// </summary>
public sealed class GuideContentSource : ICommunityContentSource
{
    private readonly IGuideRepository _guides;

    public GuideContentSource(IGuideRepository guides) => _guides = guides;

    public CommunityContentType ContentType => CommunityContentType.Guide;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var guide = await _guides.GetForReadAsync(GuideId.From(contentId), cancellationToken);

        return guide is null ? null : Describe(guide);
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

        // One read per identifier, because the repository publishes no batch read for the detail
        // graph. Community batches its calls, so this is bounded by a page rather than by the
        // table — and a bulk read is a repository change rather than a shape change here.
        foreach (var contentId in contentIds.Distinct())
        {
            var guide = await _guides.GetForReadAsync(GuideId.From(contentId), cancellationToken);

            if (guide is not null)
            {
                described[guide.Id.Value] = Describe(guide);
            }
        }

        return described;
    }

    private static ContentDescriptor Describe(Guide guide) => new()
    {
        Reference = ContentReference.Create(CommunityContentType.Guide, guide.Id.Value),
        AuthorId = guide.AuthorId.Value,
        Title = guide.Title.Value,
        Exists = !guide.IsDeleted,
        Visibility = ToCommunityVisibility(guide),
        CountryCode = guide.Location.CountryCode,
        AreaId = guide.Location.AreaId,

        // A hidden or archived guide stops taking comments; a published one takes them. A draft
        // has no readers to comment, and a discussion on a document that has not been released
        // would be a discussion about something nobody else has seen.
        AcceptsComments = guide.Status == GuideStatus.Published
            && guide.ModerationState != GuideModerationState.Hidden
    };

    /// <summary>
    /// Projects B8's audience onto B6's, narrowing wherever the two do not line up.
    /// <para>
    /// B6's vocabulary has no <c>Unlisted</c>, so a link-shared guide maps to its members-only
    /// band rather than to <c>Public</c>. Erring outward here would surface an unreleased guide's
    /// comments in a public feed, which is a privacy failure rather than a rendering bug — and an
    /// unpublished guide is private regardless of what its visibility field says.
    /// </para>
    /// </summary>
    private static VisibilityLevel ToCommunityVisibility(Guide guide)
    {
        if (guide.Status != GuideStatus.Published)
        {
            return VisibilityLevel.Private;
        }

        return guide.Visibility switch
        {
            GuideVisibility.Public => VisibilityLevel.Public,
            GuideVisibility.Unlisted => VisibilityLevel.Members,
            _ => VisibilityLevel.Private
        };
    }
}
