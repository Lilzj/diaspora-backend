using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;

// Both modules declare a `PollStatus`, and this file needs both namespaces — Community's for the
// content-type vocabulary, this module's for the aggregate. Aliased rather than fully qualified at
// the two use sites, so the ambiguity is resolved once and named where a reader meets it.
using ProgrammePollStatus = Diaspora.Modules.CommunityProgramme.Domain.Enums.PollStatus;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;

namespace Diaspora.Modules.CommunityProgramme.Infrastructure.Community;

/// <summary>
/// Makes a community poll reportable through B6.
/// <para>
/// The last member of <see cref="CommunityContentType"/> that was declared and claimed by nothing.
/// A report about a poll answered <c>community.content.type-unsupported</c>, which mattered because
/// a poll is the one piece of community content designed to *gather* answers from people — a
/// leading, harassing or fraudulent question reaches further than a post does.
/// </para>
/// <para>
/// <b>Which poll, and why it took a moment to find.</b> There are two things called a poll here.
/// <c>Community.Domain.Polls.Poll</c> hangs off a post and has no independent route — voting is
/// <c>POST /community/posts/{postId}/poll/votes</c>, keyed by the *post* — so reporting one of those
/// is reporting its post, which already works. This module's <see cref="CommunityPoll"/> is the one
/// the enum's own comment describes as *"addressable independently of the post that carries it"*:
/// <c>/communities/polls/{pollId}</c> reads it, and so this is where the content source belongs.
/// </para>
/// <para>
/// <b>Reportable, not commentable.</b> A poll gathers structured responses; the discussion belongs
/// on the post or the organization page that announced it. B6 refuses the comment and serves the
/// report from the same descriptor.
/// </para>
/// </summary>
public sealed class PollContentSource : ICommunityContentSource
{
    private readonly ICommunityPollRepository _polls;

    public PollContentSource(ICommunityPollRepository polls) => _polls = polls;

    public CommunityContentType ContentType => CommunityContentType.Poll;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        var poll = await _polls.GetAsync(CommunityPollId.From(contentId), cancellationToken);

        return poll is null ? null : Describe(poll);
    }

    /// <summary>
    /// Describes several polls at once.
    /// <para>
    /// One read per identifier. <see cref="ICommunityPollRepository"/> exposes no batched lookup,
    /// and adding one to persistence for a moderation queue that has not asked for it yet would be
    /// a change made on speculation. The call is bounded by B6's own page size, and this comment is
    /// where the decision is recorded so the next reader does not have to rediscover it.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        var described = new Dictionary<Guid, ContentDescriptor>();

        foreach (var id in contentIds.Where(id => id != Guid.Empty).Distinct())
        {
            var poll = await _polls.GetAsync(CommunityPollId.From(id), cancellationToken);

            if (poll is not null)
            {
                described[id] = Describe(poll);
            }
        }

        return described;
    }

    private static ContentDescriptor Describe(CommunityPoll poll) => new()
    {
        Reference = ContentReference.Create(CommunityContentType.Poll, poll.Id.Value),
        AuthorId = poll.CreatedByUserId.Value,

        // The question *is* the poll, and it is what a moderator needs to read to judge a report
        // about one. Unlike a private message, a poll is published to an audience already.
        Title = poll.Question,
        Exists = !poll.IsDeleted,
        Visibility = ToCommunityVisibility(poll),
        CountryCode = poll.CountryCode,
        AcceptsComments = false
    };

    /// <summary>
    /// Projects the poll's audience onto B6's, narrowing wherever the two do not line up.
    /// <para>
    /// A draft, scheduled or cancelled poll is private whatever its audience says: the status is the
    /// stronger statement, and a report about an unpublished poll must not make it visible anywhere
    /// its author has not put it.
    /// </para>
    /// </summary>
    private static VisibilityLevel ToCommunityVisibility(CommunityPoll poll)
    {
        if (poll.Status is ProgrammePollStatus.Draft or ProgrammePollStatus.Scheduled
            or ProgrammePollStatus.Cancelled)
        {
            return VisibilityLevel.Private;
        }

        return poll.Audience switch
        {
            PollAudience.Public => VisibilityLevel.Public,

            // Everything narrower than "anyone" maps to the members band rather than to public.
            // B6 has no neighbourhood or country audience of its own, and erring outward would let
            // a report about a poll addressed to one street surface in a national feed.
            PollAudience.OrganizationMembers => VisibilityLevel.Members,
            PollAudience.Neighborhood => VisibilityLevel.Members,
            PollAudience.Country => VisibilityLevel.Members,

            // `Invited` is a named list. Nothing wider than the people on it.
            _ => VisibilityLevel.Private
        };
    }
}
