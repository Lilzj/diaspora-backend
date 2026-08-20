using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// A moderation decision was made.
/// <para>
/// <see cref="Action"/> may be <c>suspend</c>, which Community records and publishes but does
/// not perform: suspending an account is Identity's, and a moderator deciding it is not the same
/// as Community doing it.
/// </para>
/// </summary>
public sealed record CommunityModerationDecidedIntegrationEvent : IntegrationEvent
{
    public required Guid DecisionId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    /// <summary>A <c>ModerationActionType</c> name, camelCased.</summary>
    public required string Action { get; init; }

    /// <summary><c>staff</c>, <c>automated</c>, <c>system</c> or <c>author</c>.</summary>
    public required string Source { get; init; }

    public Guid? ModeratorId { get; init; }

    public Guid? ContentAuthorId { get; init; }

    public string? Reason { get; init; }
}
