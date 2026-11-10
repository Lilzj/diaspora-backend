using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Records a reader's verdict on whether the guide helped.
/// <para>
/// One vote per member per guide; casting again with the other answer changes the vote rather
/// than adding one. Deliberately not a star scale — a guide either answered the reader's
/// question or it did not, and five points on knowledge invites rating the topic instead of the
/// writing.
/// </para>
/// <para>
/// <c>[Transactional]</c> because the vote writes three aggregates: the vote row, the guide's
/// rollup and the analytics row. A vote counted on the card but missing from the author's
/// analytics is two screens disagreeing about the same fact, which is the failure nobody
/// reports and everybody notices.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.Vote)]
[Transactional(Reason = "The vote row, the guide's rollup and the analytics row must commit together.")]
[Auditable]
public sealed record CastGuideVoteCommand : ICommand<GuideEngagementResponse>
{
    public required Guid GuideId { get; init; }

    public required CastGuideVoteRequest Request { get; init; }
}
