namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>One assembled summary.</summary>
public sealed record NotificationDigestResponse
{
    public required Guid Id { get; init; }

    /// <summary><c>Daily</c>, <c>Weekly</c> or <c>Manual</c>.</summary>
    public required string Frequency { get; init; }

    /// <summary><c>Generated</c>, <c>Sent</c>, <c>Opened</c> or <c>Empty</c>.</summary>
    public required string Status { get; init; }

    public string? CategoryKey { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset WindowStart { get; init; }

    public required DateTimeOffset WindowEnd { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public DateTimeOffset? OpenedAt { get; init; }

    /// <summary>
    /// How many messages the window held.
    /// <para>
    /// Not the same as the number of entries: a window holding two hundred reactions shows the first
    /// fifty and says so.
    /// </para>
    /// </summary>
    public required int TotalCount { get; init; }

    public required IReadOnlyList<DigestEntryResponse> Entries { get; init; }
}
