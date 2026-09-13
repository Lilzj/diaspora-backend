using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A reader said whether a guide helped — cast or changed through <c>POST /guides/{guideId}/vote</c>.
/// <para>
/// <b>Why this exists.</b> Until 13 Sep 2026 a vote only moved the guide's own counters, so the
/// platform's analytics never learned of it: a creator's "Helpful votes" read zero however many
/// readers marked their guides helpful. Analytics counts helpful votes from Community's reaction
/// stream, and the guide's own vote never reaches that stream — so this is not a second copy of it.
/// </para>
/// <para>
/// Published only when the vote changes something. The guide refuses a vote that repeats the
/// member's existing one, and refuses an author voting on their own guide, so neither produces a
/// message.
/// </para>
/// </summary>
public sealed record GuideVoteCastIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>
    /// The member who wrote the guide. Nullable, as on every guide contract, though the publisher
    /// holds the guide and always sets it.
    /// </summary>
    public Guid? AuthorUserId { get; init; }

    /// <summary>The member who voted. Never the author.</summary>
    public required Guid VoterUserId { get; init; }

    /// <summary>The vote as it now stands: <c>Helpful</c> or <c>NotHelpful</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The member's vote before this one, or null when this is their first.</summary>
    public string? PreviousKind { get; init; }

    public required DateTimeOffset CastAt { get; init; }
}
