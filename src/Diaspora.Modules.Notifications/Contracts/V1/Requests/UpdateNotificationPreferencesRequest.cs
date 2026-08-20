namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>
/// Changes how a member is contacted.
/// <para>
/// <b>Every property is optional, and that is what makes the screen work.</b> A member toggling one
/// switch sends one field; nothing else is touched. A request that required the whole object would
/// mean two clients racing on one screen could each overwrite the other's change with a stale copy
/// of everything else.
/// </para>
/// <para>
/// <see cref="Categories"/> accepts the existing client's shape — a map of category key to a single
/// boolean, which is what the settings screen stores today — and
/// <see cref="CategoryChannels"/> is the finer form a later client can send. Both are applied; the
/// finer one wins where they overlap.
/// </para>
/// </summary>
public sealed record UpdateNotificationPreferencesRequest
{
    public bool? Push { get; init; }

    public bool? Email { get; init; }

    /// <summary>Category key to a single on/off switch. The existing client's shape.</summary>
    public IReadOnlyDictionary<string, bool>? Categories { get; init; }

    /// <summary>The per-channel form. Overrides <see cref="Categories"/> where both name a key.</summary>
    public IReadOnlyList<CategoryChannelPreferenceRequest>? CategoryChannels { get; init; }

    public bool? DoNotDisturb { get; init; }

    public bool? QuietHoursEnabled { get; init; }

    /// <summary>Local hour, 0–23.</summary>
    public int? QuietHoursStart { get; init; }

    /// <summary>Local hour, 0–23. Must differ from the start.</summary>
    public int? QuietHoursEnd { get; init; }

    /// <summary><c>Off</c>, <c>Daily</c>, <c>Weekly</c> or <c>Manual</c>.</summary>
    public string? DigestFrequency { get; init; }

    public int? DigestHour { get; init; }

    /// <summary>A day name. Only meaningful for a weekly digest.</summary>
    public string? DigestDayOfWeek { get; init; }

    /// <summary>A BCP-47 tag.</summary>
    public string? Language { get; init; }

    /// <summary>An IANA identifier.</summary>
    public string? TimeZoneId { get; init; }

    public bool? AllowsMarketing { get; init; }
}
