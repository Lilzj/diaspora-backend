using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Integrations.Domain.Calendar;

/// <summary>
/// How a member wants calendar export to behave.
/// <para>
/// <b>Kept apart from the connection so a disconnect does not lose them.</b> Somebody who
/// disconnects Google for a week and reconnects should not have to pick their default calendar,
/// their reminder times and their timezone behaviour again — and folding these onto the connection
/// row would delete all three the moment the grant went.
/// </para>
/// <para>
/// <b><see cref="AutoExportRsvps"/> and <see cref="TwoWaySyncEnabled"/> are two settings, not two
/// values of one.</b> The frontend's own note is the reason: "a member who turns on automatic export
/// has not agreed to Diaspora reading their calendar." A single enum would have made consent to
/// export imply consent to import, which is the larger of the two by a long way — import brings
/// personal events into Diaspora's storage.
/// </para>
/// </summary>
public sealed class IntegrationCalendarSettings : AggregateRoot<IntegrationSettingsId>
{
    public const int CalendarIdLength = 256;
    public const int ProviderLength = 64;

    /// <summary>Joined minutes. Bounded so a client cannot post ten thousand reminders.</summary>
    public const int RemindersLength = 128;

    /// <summary>
    /// The most reminders a calendar entry may carry.
    /// <para>
    /// Providers cap this themselves — Google at five — and a platform that accepted more would
    /// produce a write that fails at the provider for a reason the member cannot see.
    /// </para>
    /// </summary>
    public const int MaxReminders = 5;

    /// <summary>Two days. Beyond that a "reminder" is a separate calendar entry.</summary>
    public const int MaxReminderMinutes = 2 * 24 * 60;

    private IntegrationCalendarSettings(IntegrationSettingsId id, Guid ownerId, DateTimeOffset now)
        : base(id)
    {
        OwnerId = ownerId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    private IntegrationCalendarSettings() { }

    public Guid OwnerId { get; private set; }

    /// <summary>Whether calendar export is switched on at all.</summary>
    public bool Enabled { get; private set; }

    /// <summary>The calendar new entries go into. Null means "ask every time".</summary>
    public string? DefaultCalendarId { get; private set; }

    public string? DefaultProvider { get; private set; }

    public bool AutoExportRsvps { get; private set; }

    public bool TwoWaySyncEnabled { get; private set; }

    /// <summary><c>manual</c>, <c>hourly</c> or <c>daily</c>.</summary>
    public string SyncFrequency { get; private set; } = "manual";

    private string? _defaultReminderMinutes;

    public IReadOnlyList<int> DefaultReminderMinutes =>
        string.IsNullOrWhiteSpace(_defaultReminderMinutes)
            ? []
            : [.. _defaultReminderMinutes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var minutes) ? minutes : -1)
                .Where(minutes => minutes >= 0)];

    /// <summary>
    /// <c>eventTimeZone</c> or <c>deviceTimeZone</c>.
    /// <para>
    /// Defaults to the first, and the default is the whole point: converting an event into whichever
    /// zone the member's phone is in changes what the entry claims about the world. The setting
    /// exists because somebody who has permanently relocated may genuinely want the other.
    /// </para>
    /// </summary>
    public string TimeZoneBehaviour { get; private set; } = TimeZoneBehaviours.EventTimeZone;

    /// <summary><c>skip</c>, <c>update</c> or <c>duplicate</c>.</summary>
    public string ConflictBehaviour { get; private set; } = ConflictBehaviours.Update;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// A member's settings before they have changed any.
    /// <para>
    /// Created rather than returned as a transient default, so that "I never turned this on" and
    /// "I turned it off" are the same answer for the client and a different row for support.
    /// </para>
    /// </summary>
    public static IntegrationCalendarSettings CreateDefault(
        IntegrationSettingsId id, Guid ownerId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentOutOfRangeException.ThrowIfEqual(ownerId, Guid.Empty);

        return new IntegrationCalendarSettings(id, ownerId, now)
        {
            Enabled = false,
            AutoExportRsvps = false,
            TwoWaySyncEnabled = false,
            SyncFrequency = SyncFrequencies.Manual,
            TimeZoneBehaviour = TimeZoneBehaviours.EventTimeZone,
            ConflictBehaviour = ConflictBehaviours.Update
        };
    }

    /// <summary>
    /// Applies a partial update. Null means "leave it alone".
    /// <para>
    /// <c>PUT</c> with <c>Partial&lt;CalendarSyncSettings&gt;</c> is the client's contract, so a
    /// missing field is an absence of instruction rather than an instruction to clear. Treating it
    /// as the latter would mean a client that sends only <c>enabled</c> silently wipes the member's
    /// default calendar.
    /// </para>
    /// </summary>
    public void Apply(
        bool? enabled,
        string? defaultCalendarId,
        string? defaultProvider,
        bool? autoExportRsvps,
        bool? twoWaySyncEnabled,
        string? syncFrequency,
        IReadOnlyList<int>? defaultReminderMinutes,
        string? timeZoneBehaviour,
        string? conflictBehaviour,
        DateTimeOffset now)
    {
        if (enabled is { } isEnabled)
        {
            Enabled = isEnabled;
        }

        if (defaultCalendarId is not null)
        {
            DefaultCalendarId = Blank(defaultCalendarId) ? null : Clip(defaultCalendarId, CalendarIdLength);
        }

        if (defaultProvider is not null)
        {
            DefaultProvider = Blank(defaultProvider) ? null : Clip(defaultProvider, ProviderLength);
        }

        if (autoExportRsvps is { } autoExport)
        {
            AutoExportRsvps = autoExport;
        }

        if (twoWaySyncEnabled is { } twoWay)
        {
            TwoWaySyncEnabled = twoWay;
        }

        if (syncFrequency is not null && SyncFrequencies.IsKnown(syncFrequency))
        {
            SyncFrequency = syncFrequency.Trim().ToLowerInvariant();
        }

        if (defaultReminderMinutes is not null)
        {
            _defaultReminderMinutes = string.Join(
                ',',
                defaultReminderMinutes
                    .Where(minutes => minutes is >= 0 and <= MaxReminderMinutes)
                    .Distinct()
                    .Order()
                    .Take(MaxReminders));
        }

        if (timeZoneBehaviour is not null && TimeZoneBehaviours.IsKnown(timeZoneBehaviour))
        {
            TimeZoneBehaviour = timeZoneBehaviour.Trim();
        }

        if (conflictBehaviour is not null && ConflictBehaviours.IsKnown(conflictBehaviour))
        {
            ConflictBehaviour = conflictBehaviour.Trim().ToLowerInvariant();
        }

        UpdatedAt = now;
    }

    /// <summary>
    /// Forgets the default calendar when the connection behind it goes.
    /// <para>
    /// A default pointing at a calendar the member can no longer reach produces a write that fails
    /// for a reason the settings screen cannot explain — so a disconnect clears the pointer and
    /// keeps everything else.
    /// </para>
    /// </summary>
    public void ForgetProvider(string provider, DateTimeOffset now)
    {
        if (!string.Equals(DefaultProvider, provider, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DefaultProvider = null;
        DefaultCalendarId = null;
        UpdatedAt = now;
    }

    private static bool Blank(string value) => string.IsNullOrWhiteSpace(value);

    private static string Clip(string value, int length)
    {
        var trimmed = value.Trim();

        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }

    /// <summary>The accepted <c>syncFrequency</c> values. The client's, verbatim.</summary>
    public static class SyncFrequencies
    {
        public const string Manual = "manual";
        public const string Hourly = "hourly";
        public const string Daily = "daily";

        public static bool IsKnown(string? value) =>
            value?.Trim().ToLowerInvariant() is Manual or Hourly or Daily;
    }

    /// <summary>The accepted <c>timeZoneBehaviour</c> values. The client's, verbatim and cased.</summary>
    public static class TimeZoneBehaviours
    {
        public const string EventTimeZone = "eventTimeZone";
        public const string DeviceTimeZone = "deviceTimeZone";

        public static bool IsKnown(string? value) =>
            value?.Trim() is EventTimeZone or DeviceTimeZone;
    }

    /// <summary>The accepted <c>conflictBehaviour</c> values. The client's, verbatim.</summary>
    public static class ConflictBehaviours
    {
        public const string Skip = "skip";
        public const string Update = "update";
        public const string Duplicate = "duplicate";

        public static bool IsKnown(string? value) =>
            value?.Trim().ToLowerInvariant() is Skip or Update or Duplicate;
    }
}
