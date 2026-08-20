using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Holidays;

/// <summary>
/// A day the offices are shut, and what that means for somebody with an appointment.
/// <para>
/// <b>One row per observance per year, not a recurring rule.</b> A rule would have to encode "the
/// first Monday after the full moon following the vernal equinox", every national exception to it,
/// and every year a government moves a holiday to make a long weekend — and it would still be wrong
/// exactly in the year somebody needs the answer. Rows are entered by an editor from a published
/// calendar, which is how the information arrives in the first place.
/// </para>
/// <para>
/// <b>The closure flags are the reason this aggregate exists.</b> "Independence Day, 12 March" is a
/// fact about a country. "The passport office is shut on 12 March" is the fact that changes what a
/// member does this week, and the two are not the same: several religious holidays close nothing,
/// and several missions close on their home country's holidays rather than the local ones.
/// </para>
/// <para>
/// <b>There is no draft state.</b> A holiday is either in the calendar or it is not; an editor
/// transcribing a published government calendar has nothing to draft. Archiving is the soft delete,
/// which is how a holiday entered in error disappears without erasing the audit trail of who entered
/// it.
/// </para>
/// </summary>
public sealed class PublicHoliday : AggregateRoot<PublicHolidayId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<PublicHolidayLocalization> _localizations = [];

    private PublicHoliday(
        PublicHolidayId id,
        string countryCode,
        string name,
        HolidayKind kind,
        DateTimeOffset observedOn,
        DateTimeOffset observedUntil,
        DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        Name = name;
        Kind = kind;
        ObservedOn = observedOn;
        ObservedUntil = observedUntil;
        Year = observedOn.Year;
        CreatedAt = createdAt;
    }

    private PublicHoliday() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public HolidayKind Kind { get; private set; }

    /// <summary>
    /// The area a regional holiday applies to, or null for nationwide.
    /// <para>
    /// Free text for the reason <c>OfficialNotice.Region</c> gives: B4 owns the geographic hierarchy
    /// and a calendar saying "Rodrigues only" names something no hierarchy node matches exactly.
    /// </para>
    /// </summary>
    public string? Region { get; private set; }

    /// <summary>
    /// The day it falls on.
    /// <para>
    /// Stored as an instant at midnight UTC rather than a date type, because every other timestamp on
    /// this platform is a <c>DateTimeOffset</c> and a single date column would be the one thing a
    /// consumer has to remember not to convert. Comparisons are date comparisons; the time is never
    /// meaningful.
    /// </para>
    /// </summary>
    public DateTimeOffset ObservedOn { get; private set; }

    /// <summary>The last day of a multi-day observance, or the same day as a single one.</summary>
    public DateTimeOffset ObservedUntil { get; private set; }

    /// <summary>
    /// The calendar year, denormalised.
    /// <para>
    /// Stored rather than derived because "this country's holidays for 2026" is the query the
    /// calendar screen makes, and a provider cannot use an index on <see cref="ObservedOn"/> through
    /// a year function. One integer against a per-country listing that runs on every dashboard.
    /// </para>
    /// </summary>
    public int Year { get; private set; }

    /// <summary>Whether government offices are shut. The fact that changes somebody's week.</summary>
    public bool ClosesGovernmentOffices { get; private set; }

    /// <summary>Whether missions are shut, which is frequently a different set of days.</summary>
    public bool ClosesEmbassies { get; private set; }

    /// <summary>Human translations of the name and description.</summary>
    public IReadOnlyList<PublicHolidayLocalization> Localizations => _localizations.AsReadOnly();

    /// <summary>The optimistic concurrency token, bumped on every mutation.</summary>
    public int Revision { get; private set; }

    /// <summary>Whether the entry is still in the calendar. The inverse of archived.</summary>
    public bool IsActive => !IsDeleted;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Construction and editing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<PublicHoliday> Create(
        PublicHolidayId id,
        string? countryCode,
        string? name,
        HolidayKind kind,
        DateTimeOffset observedOn,
        DateTimeOffset? observedUntil,
        DateTimeOffset now,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var country = IntelligenceNormalization.Country(countryCode);

        if (country is null)
        {
            return Result.Failure<PublicHoliday>(IntelligenceErrors.CountryInvalid);
        }

        var trimmedName = IntelligenceNormalization.Text(name, IntelligenceLimits.MaxHolidayName);

        if (trimmedName is null)
        {
            return Result.Failure<PublicHoliday>(IntelligenceErrors.HolidayNameInvalid);
        }

        var span = ResolveSpan(observedOn, observedUntil);

        if (span is null)
        {
            return Result.Failure<PublicHoliday>(IntelligenceErrors.HolidayDateInvalid);
        }

        var (from, until) = span.Value;

        var holiday = new PublicHoliday(
            id,
            country,
            trimmedName,
            Enum.IsDefined(kind) ? kind : HolidayKind.National,
            from,
            until,
            now);

        holiday.RaiseChanged(IntelligenceChangeKinds.Published, createdBy);

        return Result.Success(holiday);
    }

    /// <summary>Rewrites the entry. The country cannot change; a holiday elsewhere is another row.</summary>
    public Result Update(
        string? name,
        string? description,
        HolidayKind kind,
        DateTimeOffset observedOn,
        DateTimeOffset? observedUntil,
        string? region,
        bool closesGovernmentOffices,
        bool closesEmbassies,
        UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.HolidayArchived);
        }

        var trimmedName = IntelligenceNormalization.Text(name, IntelligenceLimits.MaxHolidayName);

        if (trimmedName is null)
        {
            return Result.Failure(IntelligenceErrors.HolidayNameInvalid);
        }

        var span = ResolveSpan(observedOn, observedUntil);

        if (span is null)
        {
            return Result.Failure(IntelligenceErrors.HolidayDateInvalid);
        }

        if (!Enum.IsDefined(kind))
        {
            return Result.Failure(IntelligenceErrors.HolidayDateInvalid);
        }

        var (from, until) = span.Value;

        Name = trimmedName;
        Description = IntelligenceNormalization.Text(
            description, IntelligenceLimits.MaxHolidayDescription);
        Kind = kind;
        ObservedOn = from;
        ObservedUntil = until;
        Year = from.Year;
        Region = IntelligenceNormalization.Text(region, IntelligenceLimits.MaxRegion);
        ClosesGovernmentOffices = closesGovernmentOffices;
        ClosesEmbassies = closesEmbassies;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the translation set.</summary>
    public Result SetLocalizations(
        IEnumerable<(string? Locale, string? Name, string? Description)>? localizations,
        UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.HolidayArchived);
        }

        var requested = localizations?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxLocalizations)
        {
            return Result.Failure(IntelligenceErrors.LocalizationLimit);
        }

        var resolved = new List<PublicHolidayLocalization>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (locale, name, description) in requested)
        {
            var localization = PublicHolidayLocalization.TryCreate(locale, name, description);

            if (localization is null)
            {
                return Result.Failure(IntelligenceErrors.LocaleInvalid);
            }

            if (seen.Add(localization.Locale))
            {
                resolved.Add(localization);
            }
        }

        _localizations.Clear();
        _localizations.AddRange(resolved);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>The name and description for a locale, with the usual fallback.</summary>
    public (string Name, string? Description) Localized(string? locale)
    {
        var normalized = IntelligenceNormalization.Locale(locale);

        if (normalized is null)
        {
            return (Name, Description);
        }

        var exact = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, normalized, StringComparison.Ordinal));

        if (exact is not null)
        {
            return (exact.Name, exact.Description ?? Description);
        }

        var language = normalized.Split('-')[0];

        var byLanguage = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, language, StringComparison.Ordinal));

        return byLanguage is null
            ? (Name, Description)
            : (byLanguage.Name, byLanguage.Description ?? Description);
    }

    public Result EnsureRevision(int? expectedRevision) =>
        expectedRevision is null || expectedRevision == Revision
            ? Result.Success()
            : Result.Failure(IntelligenceErrors.RevisionConflict);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete and auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <inheritdoc cref="Intelligence.NoticeCategory.MarkDeleted" />
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Archived, Actor(userId));
    }

    /// <summary>Returns an archived entry to the calendar, exactly as it was.</summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Restored, null);
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

    /// <summary>
    /// Normalizes an observance to whole days in UTC, or refuses it.
    /// <para>
    /// Truncating to midnight is what makes two entries for the same day comparable, and it is done
    /// here rather than at the edge because an entry created through a seeder and one created through
    /// the API must land on the same instant — otherwise "is there a holiday on the twelfth" depends
    /// on which path wrote the row.
    /// </para>
    /// <para>
    /// An absent end date means a single day. An end date before the start is refused, because unlike
    /// a bad map pin there is no useful remainder to keep.
    /// </para>
    /// </summary>
    private static (DateTimeOffset From, DateTimeOffset Until)? ResolveSpan(
        DateTimeOffset observedOn, DateTimeOffset? observedUntil)
    {
        var from = new DateTimeOffset(observedOn.UtcDateTime.Date, TimeSpan.Zero);

        if (observedUntil is null)
        {
            return (from, from);
        }

        var until = new DateTimeOffset(observedUntil.Value.UtcDateTime.Date, TimeSpan.Zero);

        return until < from ? null : (from, until);
    }

    private void Bump() => Revision++;

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(new PublicHolidayChangedDomainEvent
        {
            PublicHolidayId = Id,
            CountryCode = CountryCode,
            Name = Name,
            Kind = Kind,
            ObservedOn = ObservedOn,
            ObservedUntil = ObservedUntil,
            Change = change,
            ClosesGovernmentOffices = ClosesGovernmentOffices,
            ClosesEmbassies = ClosesEmbassies,
            ChangedBy = changedBy
        });

    private static UserId? Actor(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? UserId.From(parsed) : null;
}
