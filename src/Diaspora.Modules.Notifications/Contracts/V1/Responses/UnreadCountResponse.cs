namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// The badge.
/// <para>
/// Answered from the maintained counter rather than a count query — one indexed row read on what is
/// the most frequently requested number on the platform.
/// </para>
/// </summary>
public sealed record UnreadCountResponse
{
    public required int Total { get; init; }

    /// <summary>Per category, keyed on the catalogue key. Drives per-tab dots.</summary>
    public required IReadOnlyDictionary<string, int> ByCategory { get; init; }

    /// <summary>Whether anything has arrived since the member last opened the inbox screen.</summary>
    public required bool HasNew { get; init; }

    public DateTimeOffset? LastNotificationAt { get; init; }
}
