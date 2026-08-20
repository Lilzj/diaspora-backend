using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.Reactions;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Adds and withdraws reactions, and keeps every counter that depends on them correct.
/// <para>
/// One reaction touches four things: the instance row, the per-content summary, the counters on
/// the reacted-to aggregate, and — for a corroborating reaction on a post — the community
/// verification threshold. Doing that in one place is what stops a comment reaction updating
/// two of the four and a post reaction updating three.
/// </para>
/// <para>
/// <b>Corroboration is weighted at the moment it is given.</b> Whether the reactor was a
/// verified resident is resolved once, here, and stored on the reaction — so a member whose
/// verification later lapses does not retroactively un-confirm a safety post.
/// </para>
/// </summary>
public sealed class ReactionService : ICommunityReactionService
{
    private readonly IReactionRepository _reactions;
    private readonly ReactionCatalogLoader _catalog;
    private readonly ICommunityPostRepository _posts;
    private readonly Domain.Comments.ICommentRepository _comments;
    private readonly ContentReferenceResolver _references;
    private readonly ViewerContextResolver _viewers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ReactionService(
        IReactionRepository reactions,
        ReactionCatalogLoader catalog,
        ICommunityPostRepository posts,
        Domain.Comments.ICommentRepository comments,
        ContentReferenceResolver references,
        ViewerContextResolver viewers,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _reactions = reactions;
        _catalog = catalog;
        _posts = posts;
        _comments = comments;
        _references = references;
        _viewers = viewers;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> ReactAsync(
        ContentReference content,
        UserId userId,
        string reactionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(userId);

        var catalog = await _catalog.GetAsync(cancellationToken);
        var definition = catalog.Find(reactionKey);

        if (definition is null || !definition.Applies(content.Type))
        {
            return Result.Failure(CommunityErrors.ReactionUnknown(reactionKey ?? string.Empty));
        }

        var existing = await _reactions.FindAsync(
            content, userId, definition.Key, cancellationToken);

        if (existing is not null)
        {
            return Result.Failure(CommunityErrors.ReactionDuplicate);
        }

        var descriptor = await _references.DescribeAsync(content, cancellationToken);

        if (descriptor is null || !descriptor.Exists)
        {
            return Result.Failure(CommunityErrors.ContentNotFound);
        }

        // Confirming your own post is not corroboration, it is assertion. Refused rather than
        // silently discounted, so a member finds out instead of wondering why the count did not
        // move.
        if (definition.IsCorroborating && descriptor.AuthorId == userId.Value)
        {
            return Result.Failure(CommunityErrors.ReactionSelfCorroboration);
        }

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        var byVerifiedResident =
            descriptor.CountryCode is { } country
                ? viewer.VerifiedCountries.Contains(country, StringComparer.OrdinalIgnoreCase)
                : viewer.IsVerified;

        var reaction = Reaction.Create(
            content, userId, definition, byVerifiedResident, _clock.UtcNow);

        await _reactions.AddAsync(reaction, cancellationToken);

        await AdjustSummaryAsync(content, definition, 1, cancellationToken);
        await AdjustTargetAsync(content, definition, byVerifiedResident, 1, cancellationToken);

        reaction.AddDomainEvent(new ReactionAddedDomainEvent
        {
            ReactionId = reaction.Id,
            Content = content,
            UserId = userId,
            ReactionKey = definition.Key,
            Sentiment = definition.Sentiment,
            ContentAuthorId = descriptor.AuthorId
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> WithdrawAsync(
        ContentReference content,
        UserId userId,
        string reactionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(userId);

        var catalog = await _catalog.GetAsync(cancellationToken);
        var definition = catalog.Find(reactionKey);

        if (definition is null)
        {
            return Result.Failure(CommunityErrors.ReactionUnknown(reactionKey ?? string.Empty));
        }

        var existing = await _reactions.FindAsync(
            content, userId, definition.Key, cancellationToken);

        if (existing is null)
        {
            return Result.Failure(CommunityErrors.ReactionNotFound);
        }

        _reactions.Remove(existing);

        await AdjustSummaryAsync(content, definition, -1, cancellationToken);
        await AdjustTargetAsync(
            content, definition, existing.ByVerifiedResident, -1, cancellationToken);

        existing.AddDomainEvent(new ReactionRemovedDomainEvent
        {
            Content = content, UserId = userId, ReactionKey = definition.Key
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetCountsAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        var summary = await _reactions.GetSummaryAsync(content, cancellationToken);

        return summary is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : summary.Tallies.ToDictionary(
                tally => tally.ReactionKey, tally => tally.Count, StringComparer.Ordinal);
    }

    private async Task AdjustSummaryAsync(
        ContentReference content,
        ReactionDefinition definition,
        int delta,
        CancellationToken cancellationToken)
    {
        var summary = await _reactions.GetSummaryAsync(content, cancellationToken);

        if (summary is null)
        {
            if (delta <= 0)
            {
                return;
            }

            summary = ReactionSummary.For(content);
            await _reactions.AddSummaryAsync(summary, cancellationToken);
        }

        summary.Adjust(definition.Key, definition.Sentiment, definition.Weight, delta);
    }

    /// <summary>
    /// Moves the counters on whatever was reacted to.
    /// <para>
    /// Posts and comments are handled separately rather than through a shared interface,
    /// because they genuinely differ: a post tracks corroboration and disputes and re-evaluates
    /// a verification threshold, and a comment tracks a like count the client renders by name.
    /// A common abstraction over the two would have to carry both, and neither would use half of
    /// it.
    /// </para>
    /// </summary>
    private async Task AdjustTargetAsync(
        ContentReference content,
        ReactionDefinition definition,
        bool byVerifiedResident,
        int delta,
        CancellationToken cancellationToken)
    {
        switch (content.Type)
        {
            case CommunityContentType.Post:
            {
                var post = await _posts.GetAsync(
                    CommunityPostId.From(content.Id), cancellationToken);

                if (post is null)
                {
                    return;
                }

                post.ApplyEngagement(counters => counters.WithReactions(delta));

                if (definition.IsCorroborating)
                {
                    post.RecordCorroboration(byVerifiedResident, delta);
                }
                else if (definition.IsDisputing)
                {
                    post.RecordDispute(delta);
                }

                return;
            }

            case CommunityContentType.Comment:
            {
                var comment = await _comments.GetAsync(
                    CommentId.From(content.Id), cancellationToken);

                comment?.AdjustReactions(
                    delta,
                    string.Equals(definition.Key, "like", StringComparison.Ordinal));

                return;
            }

            default:
                // Another module owns the item. Its own counters are its business; the summary
                // row this module keeps is what its cards read.
                return;
        }
    }
}
