using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Reactions;

/// <summary>
/// A kind of reaction the platform offers.
/// <para>
/// <b>The brief forbids a hardcoded Like, and this is how that is honoured.</b> Reaction kinds
/// are rows, not enum members: a deployment adds one by contributing a definition, and every
/// counter, every summary, every ranking weight and every API response picks it up with no code
/// change anywhere.
/// </para>
/// <para>
/// The definition carries far more than a label. <see cref="Sentiment"/> is what lets a ranking
/// strategy tell applause from corroboration from dissent — a ranker that summed all reactions
/// equally would push a heavily disputed post <em>up</em>. <see cref="Weight"/> is the ranking
/// multiplier. <see cref="AppliesTo"/> is a comma-joined list of content types, because a
/// confirm reaction makes sense on a safety post and no sense at all on a comment.
/// </para>
/// <para>
/// Storing definitions separately from instances is what the brief asks for in as many words,
/// and the operational reason is retirement: withdrawing a reaction kind must not delete the
/// three million rows that used it, and a definition row is what lets an instance keep
/// rendering after its kind is retired.
/// </para>
/// </summary>
public sealed class ReactionDefinition : AggregateRoot<ReactionDefinitionId>, IAuditableEntity
{
    public const int MaxKeyLength = 32;

    private ReactionDefinition(
        ReactionDefinitionId id,
        string key,
        string label,
        string? glyph,
        ReactionSentiment sentiment,
        double weight,
        string appliesTo,
        int position,
        bool isBuiltIn) : base(id)
    {
        Key = key;
        Label = label;
        Glyph = glyph;
        Sentiment = sentiment;
        Weight = weight;
        AppliesTo = appliesTo;
        Position = position;
        IsBuiltIn = isBuiltIn;
        IsEnabled = true;
    }

    private ReactionDefinition() { }

    /// <summary>
    /// The stable wire key, lower-cased. This is what a client sends, what a counter is filed
    /// under and what an event carries — so it is a contract, and renaming one orphans every
    /// reaction ever recorded under the old spelling.
    /// </summary>
    public string Key { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>An emoji or short glyph. Display only; the client may substitute its own.</summary>
    public string? Glyph { get; private set; }

    public ReactionSentiment Sentiment { get; private set; }

    /// <summary>Ranking multiplier. One is neutral; a confirmation is worth more than applause.</summary>
    public double Weight { get; private set; }

    /// <summary>
    /// Comma-joined <see cref="CommunityContentType"/> names, or empty for every type. A string
    /// rather than a collection because the set is short, read on every reaction, and would
    /// otherwise be a second table joined on a hot path.
    /// </summary>
    public string AppliesTo { get; private set; } = string.Empty;

    public int Position { get; private set; }

    /// <summary>True for the definitions B6 seeds. A deployment may disable but not delete one.</summary>
    public bool IsBuiltIn { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public bool IsCorroborating => Sentiment == ReactionSentiment.Corroborating;

    public bool IsDisputing => Sentiment == ReactionSentiment.Disputing;

    public static ReactionDefinition Create(
        string key,
        string label,
        string? glyph,
        ReactionSentiment sentiment,
        double weight,
        IReadOnlyCollection<CommunityContentType>? appliesTo = null,
        int position = 0,
        bool isBuiltIn = false)
    {
        var normalised = Normalise(key);

        return new ReactionDefinition(
            ReactionDefinitionId.New(),
            normalised,
            (label ?? normalised).Trim(),
            glyph?.Trim(),
            sentiment,
            weight,
            appliesTo is null || appliesTo.Count == 0
                ? string.Empty
                : string.Join(',', appliesTo.Select(type => type.ToString())),
            position,
            isBuiltIn);
    }

    /// <summary>Whether this kind is offered on a given kind of content.</summary>
    public bool Applies(CommunityContentType contentType) =>
        AppliesTo.Length == 0 ||
        AppliesTo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(contentType.ToString(), StringComparer.OrdinalIgnoreCase);

    public void SetEnabled(bool enabled) => IsEnabled = enabled;

    public void Retune(double weight, ReactionSentiment sentiment)
    {
        Weight = weight;
        Sentiment = sentiment;
    }

    public static string Normalise(string? key)
    {
        var trimmed = (key ?? string.Empty).Trim().ToLowerInvariant();

        return trimmed.Length > MaxKeyLength ? trimmed[..MaxKeyLength] : trimmed;
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
