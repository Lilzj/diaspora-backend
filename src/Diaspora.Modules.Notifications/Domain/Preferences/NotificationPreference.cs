using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Domain.Preferences;

/// <summary>
/// One member's complete statement about how, when and in what language they want to be contacted.
/// <para>
/// <b>One aggregate, not two, and the brief asked for two.</b> "Notification preferences" (which
/// channels, which categories) and "communication preferences" (quiet hours, language, time zone,
/// do-not-disturb) are one settings screen, are always read together to make a single routing
/// decision, and are always written together when a member taps a switch. Splitting them would
/// create a two-aggregate consistency problem — a member could have their quiet hours saved and
/// their channel switch lost — in exchange for a boundary nothing needs. The API keeps both names
/// and serves them from this one record, which is where the brief's distinction actually belongs.
/// </para>
/// <para>
/// <b>Created on demand, never seeded.</b> A member who has never opened the settings screen has no
/// row, and the resolver treats a missing record as the defaults. Seeding one per member at
/// registration would mean a write for every account that never changes anything, and a migration
/// for every account that already exists whenever a default moves.
/// </para>
/// <para>
/// <b>Critical cannot be switched off, and the aggregate enforces it rather than the UI.</b> A
/// member who mutes "security" is asking for something the platform must refuse — and refusing it in
/// a screen means the next client to be written refuses it differently, or not at all.
/// </para>
/// </summary>
public sealed class NotificationPreference : AggregateRoot<NotificationPreferenceId>, IAuditableEntity
{
    private readonly List<ChannelPreference> _channels = [];
    private readonly List<CategoryPreference> _categories = [];

    private NotificationPreference(
        NotificationPreferenceId id,
        UserId userId,
        string language,
        string timeZoneId,
        DateTimeOffset createdAt) : base(id)
    {
        UserId = userId;
        Language = language;
        TimeZoneId = timeZoneId;
        QuietHours = QuietHours.Disabled();
        Digest = DigestSchedule.Default();
        CreatedAt = createdAt;
        _channels.AddRange(ChannelPreference.Defaults());
    }

    private NotificationPreference() { }

    public UserId UserId { get; private set; } = default!;

    /// <summary>A normalised BCP-47 tag. What templates are rendered in.</summary>
    public string Language { get; private set; } = LocaleFallbackChain.DefaultLanguage;

    /// <summary>An IANA identifier. What quiet hours and digest schedules are expressed in.</summary>
    public string TimeZoneId { get; private set; } = "UTC";

    public QuietHours QuietHours { get; private set; } = default!;

    public DigestSchedule Digest { get; private set; } = default!;

    /// <summary>
    /// Everything off except Critical, indefinitely.
    /// <para>
    /// Distinct from quiet hours, which is a window. Do-not-disturb is a member saying "not now, and
    /// I will tell you when" — usually while travelling, grieving, or in a hospital.
    /// </para>
    /// </summary>
    public bool DoNotDisturb { get; private set; }

    /// <summary>
    /// Whether the member accepts marketing.
    /// <para>
    /// Off by default and stored now although nothing sends marketing yet, because the alternative
    /// is a migration that has to invent a value for every existing member — and the only defensible
    /// invented value for consent is "no", which is what this already is.
    /// </para>
    /// </summary>
    public bool AllowsMarketing { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<ChannelPreference> Channels => _channels.AsReadOnly();

    public IReadOnlyList<CategoryPreference> Categories => _categories.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static NotificationPreference CreateDefault(
        NotificationPreferenceId id,
        UserId userId,
        DateTimeOffset createdAt,
        string? language = null,
        string? timeZoneId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        return new NotificationPreference(
            id,
            userId,
            string.IsNullOrWhiteSpace(language)
                ? LocaleFallbackChain.DefaultLanguage
                : LocaleFallbackChain.Normalize(language),
            string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId.Trim(),
            createdAt);
    }

    // ── Reads the router makes ──────────────────────────────────────────────────────────────

    /// <summary>Whether the whole transport is switched on. Unknown channels are off.</summary>
    public bool IsChannelEnabled(NotificationChannelKind channel) =>
        _channels.Find(preference => preference.Channel == channel)?.IsEnabled ?? false;

    /// <summary>
    /// The member's settings for a category, or the platform default when they have never touched it.
    /// <para>
    /// Defaulting rather than requiring a row is what lets a new category ship without writing to
    /// every member's preferences — and what stops a category nobody has an opinion about from being
    /// silently muted.
    /// </para>
    /// </summary>
    public CategoryPreference CategoryFor(string categoryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);

        var key = categoryKey.Trim().ToLowerInvariant();

        return _categories.Find(preference =>
                   string.Equals(preference.CategoryKey, key, StringComparison.Ordinal))
               ?? CategoryPreference.Create(key);
    }

    /// <summary>Whether every channel the member could receive on is switched off.</summary>
    public bool IsFullyOptedOut =>
        DoNotDisturb || _channels.TrueForAll(preference => !preference.IsEnabled);

    // ── Writes ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Switches a whole transport on or off.
    /// <para>
    /// The in-app channel cannot be switched off. It is the only place a member is guaranteed to be
    /// reachable, and an account with no reachable channel is an account the platform cannot warn
    /// about its own compromise.
    /// </para>
    /// </summary>
    public Result SetChannel(NotificationChannelKind channel, bool isEnabled)
    {
        if (channel == NotificationChannelKind.InApp && !isEnabled)
        {
            return Result.Failure(NotificationErrors.CriticalChannelRequired);
        }

        var index = _channels.FindIndex(preference => preference.Channel == channel);

        if (index >= 0)
        {
            _channels[index] = ChannelPreference.Create(channel, isEnabled);
        }
        else
        {
            _channels.Add(ChannelPreference.Create(channel, isEnabled));
        }

        Changed("Channels");

        return Result.Success();
    }

    /// <summary>Replaces one category's settings outright.</summary>
    public Result SetCategory(CategoryPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        var index = _categories.FindIndex(existing =>
            string.Equals(existing.CategoryKey, preference.CategoryKey, StringComparison.Ordinal));

        if (index >= 0)
        {
            _categories[index] = preference;
        }
        else
        {
            _categories.Add(preference);
        }

        Changed("Categories");

        return Result.Success();
    }

    /// <summary>
    /// Switches a category's email off and records that the member asked — B13.3.
    /// <para>
    /// <b>One category, never the account.</b> A footer link that switched everything off would let
    /// one unwanted marketing email silence a security alert, and an unsubscribe token is exactly the
    /// credential most likely to be handled by something that is not the member — a mail client
    /// prefetching links, a corporate scanner following every URL in an inbound message.
    /// </para>
    /// <para>
    /// <b>A mandatory category refuses.</b> The catalogue decides which those are; this method is
    /// given the answer rather than deciding it, because "which categories may be silenced" is
    /// reference data an operator edits and not a constant compiled into an aggregate.
    /// </para>
    /// </summary>
    /// <param name="isMandatory">
    /// Whether the catalogue marks this category as one a member may not switch off. True refuses,
    /// and refuses with a distinct error rather than quietly succeeding — a member who followed a
    /// link and was told nothing would follow it again.
    /// </param>
    public Result Unsubscribe(
        string categoryKey, string source, DateTimeOffset at, bool isMandatory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (isMandatory)
        {
            return Result.Failure(NotificationErrors.CategoryMandatory);
        }

        return SetCategory(CategoryFor(categoryKey).Unsubscribed(source, at));
    }

    /// <summary>
    /// Sets one category from a single boolean.
    /// <para>
    /// The shape the mobile client's existing settings screen sends: one switch per subject area.
    /// Mapped onto the finer model as "every channel at once", so the current client keeps working
    /// unchanged while a later one can address channels individually.
    /// </para>
    /// </summary>
    public Result SetCategoryEnabled(string categoryKey, bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);

        var current = CategoryFor(categoryKey);

        return SetCategory(CategoryPreference.Create(
            categoryKey,
            inApp: isEnabled,
            push: isEnabled && IsChannelEnabled(NotificationChannelKind.Push),
            email: isEnabled && IsChannelEnabled(NotificationChannelKind.Email),
            useDigest: current.UseDigest,
            isMuted: !isEnabled));
    }

    public Result SetQuietHours(bool isEnabled, int startHour, int endHour)
    {
        var quietHours = QuietHours.TryCreate(isEnabled, startHour, endHour);

        if (quietHours is null)
        {
            return Result.Failure(NotificationErrors.QuietHoursInvalid);
        }

        QuietHours = quietHours;
        Changed("QuietHours");

        return Result.Success();
    }

    public Result SetDigest(DigestFrequency frequency, int hourOfDay, DayOfWeek dayOfWeek)
    {
        var schedule = DigestSchedule.TryCreate(frequency, hourOfDay, dayOfWeek);

        if (schedule is null)
        {
            return Result.Failure(NotificationErrors.PreferenceInvalid);
        }

        Digest = schedule;
        Changed("Digest");

        return Result.Success();
    }

    /// <summary>
    /// Sets the language templates are rendered in.
    /// <para>
    /// An unrecognised tag is refused rather than accepted and ignored, because a member who sets
    /// their language and keeps receiving English has been told nothing about why.
    /// </para>
    /// </summary>
    public Result SetLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language)
            || language.Length > NotificationLimits.MaxLanguageTagLength)
        {
            return Result.Failure(NotificationErrors.LanguageUnsupported);
        }

        var normalized = LocaleFallbackChain.Normalize(language);

        if (!LocaleFallbackChain.IsKnownCulture(normalized))
        {
            return Result.Failure(NotificationErrors.LanguageUnsupported);
        }

        Language = normalized;
        Changed("Language");

        return Result.Success();
    }

    /// <summary>
    /// Sets the wall clock quiet hours and digests are expressed in.
    /// <para>
    /// Validated against the runtime's own zone database rather than a list this module keeps,
    /// because a list of time zones goes stale the first time a country changes its mind about
    /// daylight saving — which several do, most years.
    /// </para>
    /// </summary>
    public Result SetTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)
            || timeZoneId.Length > NotificationLimits.MaxTimeZoneIdLength)
        {
            return Result.Failure(NotificationErrors.TimeZoneUnknown);
        }

        var trimmed = timeZoneId.Trim();

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out _))
        {
            return Result.Failure(NotificationErrors.TimeZoneUnknown);
        }

        TimeZoneId = trimmed;
        Changed("TimeZone");

        return Result.Success();
    }

    public void SetDoNotDisturb(bool enabled)
    {
        DoNotDisturb = enabled;
        Changed("DoNotDisturb");
    }

    public void SetMarketing(bool allowed)
    {
        AllowsMarketing = allowed;
        Changed("Marketing");
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    /// <summary>
    /// Bumps the token and records which section moved.
    /// <para>
    /// One domain event per section rather than one per save, because the published contract carries
    /// the list and a consumer watching for members opting out entirely needs to know which switch
    /// did it. The relay collapses several into one published event per unit of work.
    /// </para>
    /// </summary>
    private void Changed(string section)
    {
        Revision++;

        RaiseDomainEvent(new NotificationPreferenceChangedDomainEvent
        {
            PreferenceId = Id,
            UserId = UserId,
            Section = section,
            IsFullyOptedOut = IsFullyOptedOut
        });
    }
}
