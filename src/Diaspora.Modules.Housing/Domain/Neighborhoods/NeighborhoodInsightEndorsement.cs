using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Neighborhoods;

/// <summary>
/// One member attesting to one insight.
/// <para>
/// <b>A row rather than a counter, because uniqueness is the whole value.</b> An endorsement count
/// that a member could raise by pressing a button twice would rank the loudest contributor's
/// opinion above the one eleven residents actually agree with, and the profile ranks by that
/// number. The unique index on (insight, member) is what makes the count mean something.
/// </para>
/// <para>
/// <b>Hard-deleted, deliberately, unlike almost everything else in this module.</b> Withdrawing an
/// endorsement is a member saying they no longer agree, and a soft-deleted row that still names
/// them would be the platform keeping a record of an opinion they retracted.
/// </para>
/// </summary>
public sealed class NeighborhoodInsightEndorsement : Entity<Guid>, IAuditableEntity
{
    private NeighborhoodInsightEndorsement(
        Guid id,
        NeighborhoodInsightId insightId,
        UserId memberId,
        DateTimeOffset createdAt) : base(id)
    {
        InsightId = insightId;
        MemberId = memberId;
        CreatedAt = createdAt;
    }

    private NeighborhoodInsightEndorsement() { }

    public NeighborhoodInsightId InsightId { get; private set; } = default!;

    public UserId MemberId { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static NeighborhoodInsightEndorsement Create(
        Guid id, NeighborhoodInsightId insightId, UserId memberId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(insightId);
        ArgumentNullException.ThrowIfNull(memberId);

        return new NeighborhoodInsightEndorsement(id, insightId, memberId, createdAt);
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
