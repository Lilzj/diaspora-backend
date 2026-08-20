using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.Follows;

/// <summary>
/// One member following one thing.
/// <para>
/// <b>The follow graph lives in B6 and not in Identity</b>, which is intentional deviation D-7
/// in the frontend contract report. Identity owns who a member is; following is not a fact about
/// identity but a subscription to content, and every surface that consumes it — the following
/// feed, the organization feed, hashtag subscriptions, follower-only visibility — is B6's.
/// Putting the edge in Identity would mean Identity growing a feature for every feed B6 adds.
/// </para>
/// <para>
/// A member, an organization and a hashtag are all followable through the same edge rather than
/// three tables, because the only query that matters is "what does this member follow", and
/// three tables makes that a three-way union on the hottest path in the module.
/// </para>
/// </summary>
public sealed class Follow : AggregateRoot<FollowId>, IAuditableEntity
{
    private Follow(
        FollowId id,
        UserId followerId,
        FollowTargetType targetType,
        Guid targetId,
        string edgeKey,
        DateTimeOffset followedAt) : base(id)
    {
        FollowerId = followerId;
        TargetType = targetType;
        TargetId = targetId;
        EdgeKey = edgeKey;
        FollowedAt = followedAt;
    }

    private Follow() { }

    public UserId FollowerId { get; private set; } = default!;

    public FollowTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    /// <summary>Follower and target in one column, so the edge is unique by index.</summary>
    public string EdgeKey { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the following feed shows this target's content.
    /// <para>
    /// Muting without unfollowing. The social convention exists because unfollowing is visible
    /// and socially costly, and a member who wants a quieter feed should not have to make a
    /// statement to get one.
    /// </para>
    /// </summary>
    public bool IsMuted { get; private set; }

    public DateTimeOffset FollowedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static Result<Follow> Create(
        UserId followerId, FollowTargetType targetType, Guid targetId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(followerId);

        if (targetType == FollowTargetType.Member && followerId.Value == targetId)
        {
            return Result.Failure<Follow>(CommunityErrors.FollowSelf);
        }

        var follow = new Follow(
            FollowId.New(),
            followerId,
            targetType,
            targetId,
            BuildKey(followerId, targetType, targetId),
            now);

        follow.RaiseDomainEvent(new FollowCreatedDomainEvent
        {
            FollowId = follow.Id,
            FollowerId = followerId,
            TargetType = targetType,
            TargetId = targetId
        });

        return Result.Success(follow);
    }

    public static string BuildKey(UserId followerId, FollowTargetType targetType, Guid targetId)
    {
        ArgumentNullException.ThrowIfNull(followerId);

        return $"{followerId.Value:N}:{(int)targetType}:{targetId:N}";
    }

    public void SetMuted(bool muted) => IsMuted = muted;

    public void RaiseRemoved() =>
        RaiseDomainEvent(new FollowRemovedDomainEvent
        {
            FollowerId = FollowerId, TargetType = TargetType, TargetId = TargetId
        });

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
