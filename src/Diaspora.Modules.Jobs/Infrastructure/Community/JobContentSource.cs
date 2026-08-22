using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Jobs.Infrastructure.Community;

/// <summary>
/// Makes a job posting reportable and moderatable through B6.
/// <para>
/// <b>Why this did not exist, and what it cost.</b> <see cref="CommunityContentType.Job"/> has been
/// in B6's enum since it shipped, and this module's own <c>.csproj</c> already documents
/// <c>ICommunityContentSource</c> as a deliberate dependency — *"CommunityContentType.Job has
/// existed since"*. The class was never written, so <c>ContentReferenceResolver</c> rejected the
/// type and <c>POST /community/reports</c> answered
/// <c>community.content.type-unsupported</c> for every job. <b>A job advert was the one thing on
/// this platform a member could not flag</b>, which matters more here than elsewhere: a fraudulent
/// advert asking for an application fee is a known diaspora harm, and the client's report modal had
/// to carry a sentence saying so.
/// </para>
/// <para>
/// <b>Reportable, not commentable.</b> <see cref="ContentDescriptor.AcceptsComments"/> is false for
/// every posting. A public comment thread under a job advert is a different product decision — it
/// exposes who is interested in a role to their current employer — and this class exists to close a
/// safety gap rather than to make one. B6 refuses the comment and serves the report from the same
/// descriptor, which is precisely the granularity the interface offers.
/// </para>
/// <para>
/// <b>Visibility is narrowed, never widened.</b> A draft or closed posting is described as private,
/// so a report about it cannot surface the posting anywhere a reader could not already see it.
/// </para>
/// </summary>
public sealed class JobContentSource : ICommunityContentSource
{
    private readonly IJobPostingRepository _postings;
    private readonly IClock _clock;

    public JobContentSource(IJobPostingRepository postings, IClock clock)
    {
        _postings = postings;
        _clock = clock;
    }

    public CommunityContentType ContentType => CommunityContentType.Job;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        var posting = await _postings.GetIncludingDeletedAsync(
            JobPostingId.From(contentId), cancellationToken);

        // Null rather than a "missing" descriptor: a report about a posting that has never existed
        // is a mistake to refuse, not a withdrawal to explain. B6 answers
        // `community.content.not-found`, which is the honest 404.
        return posting is null ? null : Describe(posting);
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

        // One batched read. B6 batches its own calls, so this is bounded by a page of the
        // moderation queue rather than by the whole board.
        var postings = await _postings.GetManyAsync(
            [.. contentIds.Where(id => id != Guid.Empty).Distinct().Select(JobPostingId.From)],
            cancellationToken);

        foreach (var posting in postings)
        {
            described[posting.Id.Value] = Describe(posting);
        }

        return described;
    }

    /// <summary>
    /// Applies a moderation outcome to the posting itself.
    /// <para>
    /// Implemented rather than left at the default, and for the reason the marketplace source gives:
    /// a moderator who upholds a report about a fraudulent advert and leaves the advert in search
    /// has moved the complaint rather than the problem. On a job board that means the next
    /// applicant meets the same fee request.
    /// </para>
    /// <para>
    /// <b>Archive flags rather than hides.</b> B9 models no archive transition a moderator may
    /// drive, and mapping it onto <c>Hide</c> would take a legitimate advert down on a decision that
    /// did not ask for that. The caller owns the unit of work, so this mutates and does not save.
    /// </para>
    /// </summary>
    public async Task ApplyModerationAsync(
        Guid contentId,
        ModerationActionType action,
        CancellationToken cancellationToken = default)
    {
        var posting = await _postings.GetAsync(JobPostingId.From(contentId), cancellationToken);

        if (posting is null)
        {
            return;
        }

        var now = _clock.UtcNow;

        switch (action)
        {
            case ModerationActionType.Hide:
            case ModerationActionType.Suspend:
            case ModerationActionType.Reject:
                posting.Hide("Hidden following a community report.", posting.CreatedByUserId, now);
                break;

            case ModerationActionType.Approve:
            case ModerationActionType.Restore:
                posting.Unhide(null, now);
                break;

            case ModerationActionType.Archive:
                // B9 has no archive transition a moderator may drive, and flagging is the weaker
                // action that keeps the posting visible while marking it for a second look. Better
                // than mapping archive onto Hide, which would take a legitimate advert down on a
                // decision that did not ask for that.
                posting.Flag(null, "Flagged following a community report.", now);
                break;

            default:
                // Pin and Unpin say nothing about a posting's lifecycle. B6 has already recorded
                // the decision; there is nothing for this module to do.
                break;
        }
    }

    private static ContentDescriptor Describe(JobPosting posting) => new()
    {
        Reference = ContentReference.Create(CommunityContentType.Job, posting.Id.Value),

        // The member who created the advert, not the employer profile. B6's model is about people:
        // a report is filed against whoever is answerable for the content.
        AuthorId = posting.CreatedByUserId.Value,
        Title = posting.Title.Value,
        Exists = !posting.IsDeleted,
        Visibility = ToCommunityVisibility(posting),
        CountryCode = posting.Location.CountryCode,
        AreaId = posting.Location.AreaId,

        // See the class note. A job posting is reportable and is not a comment thread.
        AcceptsComments = false
    };

    /// <summary>
    /// Projects B9's audience onto B6's, narrowing wherever the two do not line up.
    /// <para>
    /// B6 has no <c>Unlisted</c>, so a link-shared posting maps to its members-only band rather than
    /// to <c>Public</c>. A posting that is not publicly visible — draft, closed, filled — is private
    /// whatever its visibility field says, because the status is the stronger statement.
    /// </para>
    /// </summary>
    private static VisibilityLevel ToCommunityVisibility(JobPosting posting)
    {
        if (!JobStatusTransitions.IsPubliclyVisible(posting.Status))
        {
            return VisibilityLevel.Private;
        }

        return posting.Visibility switch
        {
            JobVisibility.Public => VisibilityLevel.Public,
            JobVisibility.Unlisted => VisibilityLevel.Members,
            _ => VisibilityLevel.Private
        };
    }
}
