namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// When and in what language a member wants to hear from the platform.
/// <para>
/// The brief's "communication preferences", served from the same record as the channel switches. The
/// hours are the member's own wall clock, not UTC: "22:00" is a statement about when somebody sleeps.
/// </para>
/// </summary>
public sealed record CommunicationPreferencesResponse
{
    public required bool DoNotDisturb { get; init; }

    public required bool QuietHoursEnabled { get; init; }

    /// <summary>Local hour, 0–23, inclusive.</summary>
    public required int QuietHoursStart { get; init; }

    /// <summary>Local hour, 0–23, exclusive.</summary>
    public required int QuietHoursEnd { get; init; }

    /// <summary><c>Off</c>, <c>Daily</c>, <c>Weekly</c> or <c>Manual</c>.</summary>
    public required string DigestFrequency { get; init; }

    public required int DigestHour { get; init; }

    /// <summary>Only meaningful for a weekly digest.</summary>
    public required string DigestDayOfWeek { get; init; }

    /// <summary>A BCP-47 tag. What templates are rendered in.</summary>
    public required string Language { get; init; }

    /// <summary>An IANA identifier. What quiet hours and digests are expressed in.</summary>
    public required string TimeZoneId { get; init; }

    public required bool AllowsMarketing { get; init; }

    /// <summary>
    /// Always true, and stated rather than implied.
    /// <para>
    /// Critical messages — a suspension, a sign-in from a new country — override every setting on
    /// this record. A settings screen that did not say so would be misrepresenting what the switches
    /// above it do.
    /// </para>
    /// </summary>
    public required bool CriticalOverrideEnabled { get; init; }
}
