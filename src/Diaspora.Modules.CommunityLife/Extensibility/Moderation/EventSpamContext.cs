namespace Diaspora.Modules.CommunityLife.Extensibility.Moderation;

/// <summary>
/// What a spam-signal provider is given about an event being published.
/// <para>
/// Flat primitives, so a provider in another assembly does not reference this module's domain and
/// cannot mutate what it is assessing.
/// </para>
/// </summary>
public sealed record EventSpamContext
{
    public required Guid EventId { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public string? Body { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public string? VenueName { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    /// <summary>How many live events the organizer already holds. The volume signal.</summary>
    public required int OrganizerLiveEventCount { get; init; }

    /// <summary>Whether the organizing organization carries a Trust badge.</summary>
    public required bool IsOrganizerVerified { get; init; }

    /// <summary>Whether the event carries any media at all. A signal on its own.</summary>
    public required bool HasMedia { get; init; }

    /// <summary>External URLs the content carries. What a link-reputation provider reads.</summary>
    public IReadOnlyList<string> ExternalUrls { get; init; } = [];
}
