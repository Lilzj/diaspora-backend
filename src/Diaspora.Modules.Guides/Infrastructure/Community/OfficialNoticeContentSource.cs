using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;

namespace Diaspora.Modules.Guides.Infrastructure.Community;

/// <summary>
/// Makes official notices reactable and commentable, through Community.
/// <para>
/// <b>Added 13 Oct 2026.</b> A notice a platform admin published had no way to be reacted to or
/// discussed, unlike every update in the app. Following <see cref="GuideContentSource"/>, this one
/// class is the whole cost: Community's reactions and comments work on any content type a module
/// describes. Reports on a notice itself are refused in Community's report handler — it is the
/// platform's own publication — while comments under it remain reportable.
/// </para>
/// <para>
/// <b>The author is the staff member who published it</b> (<c>VerifiedBy</c>, which the console's
/// publish step sets), so they are the one told about comments. A notice with neither recorded —
/// none exist today — falls back to an empty id, which matches no member.
/// </para>
/// </summary>
public sealed class OfficialNoticeContentSource : ICommunityContentSource
{
    private readonly IOfficialNoticeRepository _notices;

    public OfficialNoticeContentSource(IOfficialNoticeRepository notices) => _notices = notices;

    public CommunityContentType ContentType => CommunityContentType.Notice;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var notice = await _notices.GetAsync(OfficialNoticeId.From(contentId), cancellationToken);

        return notice is null ? null : Describe(notice);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        var described = new Dictionary<Guid, ContentDescriptor>();

        foreach (var contentId in contentIds.Distinct())
        {
            var notice = await _notices.GetAsync(OfficialNoticeId.From(contentId), cancellationToken);

            if (notice is not null)
            {
                described[contentId] = Describe(notice);
            }
        }

        return described;
    }

    private static ContentDescriptor Describe(OfficialNotice notice)
    {
        var isPublic = notice.Status is NoticeStatus.Published or NoticeStatus.Superseded;

        return new ContentDescriptor
        {
            Reference = ContentReference.Create(CommunityContentType.Notice, notice.Id.Value),
            AuthorId = notice.VerifiedBy?.Value ?? notice.LastReviewedBy?.Value ?? Guid.Empty,
            Title = notice.Title,
            Exists = !notice.IsDeleted,

            // A draft is not in front of members, so nothing about it is either.
            Visibility = isPublic ? VisibilityLevel.Public : VisibilityLevel.Private,
            CountryCode = notice.CountryCode,

            // Discussion while it is the live notice. A superseded one stays readable with its
            // thread, but the conversation has moved to the notice that replaced it.
            AcceptsComments = notice.Status == NoticeStatus.Published && !notice.IsDeleted
        };
    }
}
