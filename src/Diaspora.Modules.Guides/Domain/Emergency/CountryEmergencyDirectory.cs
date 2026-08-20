using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Emergency;

/// <summary>
/// Every number somebody in one country should be able to find in ten seconds.
/// <para>
/// <b>One aggregate per country rather than one per number, because the invariant that matters is
/// about the set.</b> A country has one police number, and a directory showing two is worse than one
/// showing none — somebody dials the wrong one during the minute it matters. Editing as a unit is
/// also how it is actually edited: a person checks the whole page against an official source and
/// saves it.
/// </para>
/// <para>
/// <b>Never soft-deleted, and this is the only aggregate in the phase that is not.</b> Everything
/// else here archives, because an out-of-date notice is history worth keeping. An out-of-date
/// emergency number is not history — it is a number that rings somewhere else now, and the platform
/// having a hidden row holding it is a liability with no upside. A country that should not show
/// emergency information has an empty directory, which is a state the read path already renders
/// correctly.
/// </para>
/// </summary>
public sealed class CountryEmergencyDirectory
    : AggregateRoot<CountryEmergencyDirectoryId>, IAuditableEntity
{
    private readonly List<EmergencyContact> _contacts = [];
    private readonly List<EmergencyContactLocalization> _localizations = [];

    private CountryEmergencyDirectory(
        CountryEmergencyDirectoryId id, string countryCode, DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        CreatedAt = createdAt;
    }

    private CountryEmergencyDirectory() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Unique across the table.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>
    /// A line the editor wants read before any number — "dial 999 from a landline, 112 from a
    /// mobile". Rendered above the list, verbatim.
    /// </summary>
    public string? Guidance { get; private set; }

    /// <summary>The numbers, in the order the editor put them.</summary>
    public IReadOnlyList<EmergencyContact> Contacts => _contacts.AsReadOnly();

    /// <summary>Translations of the labels and notes. Never of the numbers.</summary>
    public IReadOnlyList<EmergencyContactLocalization> Localizations => _localizations.AsReadOnly();

    /// <summary>When somebody last checked the whole page against an official source.</summary>
    public DateTimeOffset? LastReviewedAt { get; private set; }

    public UserId? LastReviewedBy { get; private set; }

    /// <summary>The optimistic concurrency token, bumped on every mutation.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Construction and editing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<CountryEmergencyDirectory> Create(
        CountryEmergencyDirectoryId id,
        string? countryCode,
        DateTimeOffset now,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var country = IntelligenceNormalization.Country(countryCode);

        if (country is null)
        {
            return Result.Failure<CountryEmergencyDirectory>(IntelligenceErrors.CountryInvalid);
        }

        var directory = new CountryEmergencyDirectory(id, country, now);

        directory.RaiseChanged(IntelligenceChangeKinds.Created, createdBy);

        return Result.Success(directory);
    }

    /// <summary>Sets the line rendered above the numbers, or clears it.</summary>
    public Result SetGuidance(string? guidance, UserId? changedBy)
    {
        Guidance = IntelligenceNormalization.Text(guidance, IntelligenceLimits.MaxEmergencyNote);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the whole contact set.
    /// <para>
    /// Replace rather than add-and-remove, because the caller is an editing screen that already holds
    /// the intended list and a differ here would be a second implementation of the one that screen
    /// has. <b>A repeated kind is refused rather than collapsed</b> — unlike the localization
    /// collections, where a duplicate row is a spreadsheet artefact. Two police numbers is a
    /// contradiction the editor must resolve, and the platform silently keeping the first would pick
    /// the answer by list order.
    /// </para>
    /// </summary>
    public Result SetContacts(
        IEnumerable<(string? Kind, string? Label, string? Number, string? AlternateNumber,
            string? Note, bool IsAvailableAllHours)>? contacts,
        UserId? changedBy)
    {
        var requested = contacts?.ToList() ?? [];

        if (requested.Count > IntelligenceLimits.MaxEmergencyContacts)
        {
            return Result.Failure(IntelligenceErrors.EmergencyContactLimit);
        }

        var resolved = new List<EmergencyContact>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (kind, label, number, alternate, note, allHours) in requested)
        {
            var contact = EmergencyContact.TryCreate(
                kind, label, number, alternate, note, allHours, resolved.Count);

            if (contact is null)
            {
                return Result.Failure(IntelligenceErrors.EmergencyContactInvalid);
            }

            if (!seen.Add(contact.Kind))
            {
                return Result.Failure(IntelligenceErrors.DuplicateEmergencyKind);
            }

            resolved.Add(contact);
        }

        _contacts.Clear();
        _contacts.AddRange(resolved);

        // A translation of a kind nobody lists any more is a row that can never be read and would
        // outlive the contact it describes. Dropping them here is the only place that can see both
        // collections at once.
        _localizations.RemoveAll(localization => !seen.Contains(localization.Kind));

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the translation set.
    /// <para>
    /// A translation naming a kind the country does not list is refused rather than dropped: it is
    /// almost always a typo in the kind, and silently discarding it would leave the translator
    /// looking at a page that never changes no matter how many times they save it.
    /// </para>
    /// </summary>
    public Result SetLocalizations(
        IEnumerable<(string? Locale, string? Kind, string? Label, string? Note)>? localizations,
        UserId? changedBy)
    {
        var requested = localizations?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxLocalizations * 4)
        {
            return Result.Failure(IntelligenceErrors.LocalizationLimit);
        }

        var kinds = _contacts
            .Select(contact => contact.Kind)
            .ToHashSet(StringComparer.Ordinal);

        var resolved = new List<EmergencyContactLocalization>(requested.Count);
        var seen = new HashSet<(string, string)>();

        foreach (var (locale, kind, label, note) in requested)
        {
            var localization = EmergencyContactLocalization.TryCreate(locale, kind, label, note);

            if (localization is null)
            {
                return Result.Failure(IntelligenceErrors.LocaleInvalid);
            }

            if (!kinds.Contains(localization.Kind))
            {
                return Result.Failure(IntelligenceErrors.EmergencyContactInvalid);
            }

            if (seen.Add((localization.Locale, localization.Kind)))
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

    /// <summary>
    /// The label and note for one contact in one locale, with the usual exact→language→default
    /// fallback.
    /// </summary>
    public (string Label, string? Note) Localized(EmergencyContact contact, string? locale)
    {
        ArgumentNullException.ThrowIfNull(contact);

        var normalized = IntelligenceNormalization.Locale(locale);

        if (normalized is null)
        {
            return (contact.Label, contact.Note);
        }

        var exact = Find(contact.Kind, normalized);

        if (exact is not null)
        {
            return (exact.Label, exact.Note ?? contact.Note);
        }

        var byLanguage = Find(contact.Kind, normalized.Split('-')[0]);

        return byLanguage is null
            ? (contact.Label, contact.Note)
            : (byLanguage.Label, byLanguage.Note ?? contact.Note);
    }

    /// <summary>Records that somebody checked the whole page against an official source.</summary>
    public Result MarkReviewed(DateTimeOffset now, UserId? reviewedBy)
    {
        LastReviewedAt = now;
        LastReviewedBy = reviewedBy;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, reviewedBy);

        return Result.Success();
    }

    public Result EnsureRevision(int? expectedRevision) =>
        expectedRevision is null || expectedRevision == Revision
            ? Result.Success()
            : Result.Failure(IntelligenceErrors.RevisionConflict);

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

    private EmergencyContactLocalization? Find(string kind, string locale) =>
        _localizations.FirstOrDefault(localization =>
            string.Equals(localization.Kind, kind, StringComparison.Ordinal)
            && string.Equals(localization.Locale, locale, StringComparison.Ordinal));

    private void Bump() => Revision++;

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(new EmergencyDirectoryChangedDomainEvent
        {
            CountryEmergencyDirectoryId = Id,
            CountryCode = CountryCode,
            Change = change,
            ContactCount = _contacts.Count,
            ChangedBy = changedBy
        });
}
