using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Reactions;

/// <summary>
/// One member reacting one way to one piece of content.
/// <para>
/// The uniqueness rule is (member, content, kind) and it is enforced by a database index rather
/// than only by a handler check. A member double-tapping on a slow connection fires the same
/// request twice, and a check-then-insert loses that race about as often as you would expect.
/// </para>
/// <para>
/// Note what this permits: a member may hold <em>several</em> reactions on one item — helpful
/// and confirmed at once — which is deliberate. Forcing one reaction per member per item would
/// make confirming a safety post silently withdraw the applause, and there is no reading of that
/// which is what anybody meant.
/// </para>
/// <para>
/// The sentiment is copied onto the instance from the definition at the moment of reacting.
/// Denormalised, and for once not for speed: retuning a definition years later must not
/// retroactively rewrite what past reactions meant.
/// </para>
/// </summary>
public sealed class Reaction : AggregateRoot<ReactionId>, IAuditableEntity
{
    private Reaction(
        ReactionId id,
        ContentReference content,
        UserId userId,
        string reactionKey,
        ReactionSentiment sentiment,
        DateTimeOffset reactedAt) : base(id)
    {
        Content = content;
        UserId = userId;
        ReactionKey = reactionKey;
        Sentiment = sentiment;
        ReactedAt = reactedAt;
        UniquenessKey = BuildKey(userId, content, reactionKey);
    }

    private Reaction() { }

    public ContentReference Content { get; private set; } = default!;

    /// <summary>
    /// Member, content and kind in one column.
    /// <para>
    /// The rule this class exists to enforce is (member, content, kind), and it has to be a real
    /// unique index rather than a handler check — a double tap on a slow connection loses that
    /// race often. But <b>an index cannot span an owned type's columns and the aggregate's
    /// own</b>, which is the constraint B5 recorded when its collections needed the same thing.
    /// So the three are denormalised into one column and the index goes on that.
    /// </para>
    /// </summary>
    public string UniquenessKey { get; private set; } = string.Empty;

    public UserId UserId { get; private set; } = default!;

    /// <summary>The catalogue key. Not an enum — the catalogue is data.</summary>
    public string ReactionKey { get; private set; } = string.Empty;

    /// <summary>Copied from the definition at the moment of reacting, and never re-derived.</summary>
    public ReactionSentiment Sentiment { get; private set; }

    /// <summary>
    /// True when the reactor was a verified resident of the content's country at the time.
    /// <para>
    /// Captured here rather than looked up when the threshold is evaluated, because the
    /// verification a corroboration was worth is a fact about the moment it was given. A member
    /// whose verification later lapses does not retroactively un-corroborate a post, and one who
    /// gains verification does not retroactively strengthen one.
    /// </para>
    /// </summary>
    public bool ByVerifiedResident { get; private set; }

    public DateTimeOffset ReactedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static Reaction Create(
        ContentReference content,
        UserId userId,
        ReactionDefinition definition,
        bool byVerifiedResident,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(definition);

        return new Reaction(
            ReactionId.New(), content, userId, definition.Key, definition.Sentiment, now)
        {
            ByVerifiedResident = byVerifiedResident
        };
    }

    /// <summary>The single-column form the unique index is built on.</summary>
    public static string BuildKey(UserId userId, ContentReference content, string reactionKey)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(content);

        return $"{userId.Value:N}:{content.Key}:{reactionKey}";
    }

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
