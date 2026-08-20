using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Embassies;

/// <summary>
/// One diplomatic mission in the embassy directory.
/// <para>
/// <b>Managed content, and emphatically not a Provider.</b> B7's provider is a member's business: it
/// registers itself, an account owns it, customers review it, and staff can suspend it. Every one of
/// those is wrong for a consulate. Modelling missions as providers would put a claim-this-listing
/// button on an embassy, invite five-star ratings of a passport office, and give the platform a
/// suspension workflow for a sovereign institution. The architecture suite fails the build if this
/// aggregate ever grows an owner, a rating or a review count.
/// </para>
/// <para>
/// <b>Two country codes, and both are load-bearing.</b> <see cref="CountryCode"/> is where the
/// mission physically is; <see cref="ServesCountryCode"/> is whose nationals it looks after. The
/// Nigerian High Commission in London belongs on the United Kingdom's dashboard as a mission present
/// there and on Nigeria's as somewhere its citizens abroad are served, and neither code can be
/// derived from the other. Every directory query in this module takes one or the other explicitly.
/// </para>
/// <para>
/// Verification is Trust's decision projected inbound, exactly as it is for an issuing authority.
/// Archiving is the soft delete; there is one gone state.
/// </para>
/// </summary>
public sealed class Embassy : AggregateRoot<EmbassyId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<EmbassyOfficeHours> _officeHours = [];
    private readonly List<EmbassyService> _services = [];
    private readonly List<EmbassyLocalization> _localizations = [];

    private Embassy(
        EmbassyId id,
        string countryCode,
        string servesCountryCode,
        string name,
        DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        ServesCountryCode = servesCountryCode;
        Name = name;
        CreatedAt = createdAt;
    }

    private Embassy() { }

    // ── Placement ───────────────────────────────────────────────────────────────────────────

    /// <summary>Upper-case ISO 3166-1 alpha-2 — where the mission is located.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Upper-case ISO 3166-1 alpha-2 — whose nationals it serves.</summary>
    public string ServesCountryCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The issuing authority record for this mission, when one exists.
    /// <para>
    /// Optional, because the two exist for different reasons and most missions need only one of
    /// them. A mission that issues notices — a consulate announcing new passport procedures — needs
    /// an authority record so those notices have a verifiable source. A mission that only appears in
    /// the directory does not, and forcing one would double the register for no gain.
    /// </para>
    /// </summary>
    public IssuingAuthorityId? AuthorityId { get; private set; }

    // ── Contact ─────────────────────────────────────────────────────────────────────────────

    public string? City { get; private set; }

    public string? Address { get; private set; }

    /// <summary>
    /// Optional coordinates for a map pin.
    /// <para>
    /// Kept here rather than resolved through B4 because B4 owns the geographic <em>hierarchy</em> —
    /// countries, regions, cities — and a mission is a point inside one, not a node of it.
    /// Registering embassies as places in B4 would put editorial content into the platform's
    /// reference data.
    /// </para>
    /// </summary>
    public double? Latitude { get; private set; }

    /// <inheritdoc cref="Latitude" />
    public double? Longitude { get; private set; }

    public string? Telephone { get; private set; }

    /// <summary>
    /// The out-of-hours line. The single most important field on this record.
    /// <para>
    /// It is kept separate from <see cref="Telephone"/> because somebody who needs it is not going to
    /// read a paragraph to find it, and a client renders it as its own control for exactly that
    /// reason.
    /// </para>
    /// </summary>
    public string? EmergencyTelephone { get; private set; }

    public string? Email { get; private set; }

    public string? Website { get; private set; }

    /// <summary>Where a member books, when the mission takes appointments online.</summary>
    public string? AppointmentUrl { get; private set; }

    /// <summary>Whether the mission requires an appointment for anything at all.</summary>
    public bool RequiresAppointment { get; private set; }

    // ── Headline capabilities ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The four the brief names, as flags rather than rows.
    /// <para>
    /// Every client filters on these — "which mission near me renews passports" is the directory's
    /// commonest question — and a filter over a child collection is a join every consumer pays for on
    /// every read. Everything beyond these four is a keyed row in <see cref="Services"/>.
    /// </para>
    /// </summary>
    public bool OffersPassportServices { get; private set; }

    /// <inheritdoc cref="OffersPassportServices" />
    public bool OffersVisaServices { get; private set; }

    /// <inheritdoc cref="OffersPassportServices" />
    public bool OffersConsularServices { get; private set; }

    /// <inheritdoc cref="OffersPassportServices" />
    public bool OffersEmergencyAssistance { get; private set; }

    // ── Media and provenance ────────────────────────────────────────────────────────────────

    /// <summary>A B5 media reference for the building photograph. A reference, never a URL.</summary>
    public Guid? MediaId { get; private set; }

    public bool IsVerified { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <inheritdoc cref="IssuingAuthority.VerificationCaseId" />
    public Guid? VerificationCaseId { get; private set; }

    // ── Collections ─────────────────────────────────────────────────────────────────────────

    /// <summary>Opening hours, one row per day the mission publishes.</summary>
    public IReadOnlyList<EmbassyOfficeHours> OfficeHours => _officeHours.AsReadOnly();

    /// <summary>Everything the mission does beyond the four headline capabilities.</summary>
    public IReadOnlyList<EmbassyService> Services => _services.AsReadOnly();

    /// <summary>Human translations of the name, description and address.</summary>
    public IReadOnlyList<EmbassyLocalization> Localizations => _localizations.AsReadOnly();

    // ── Concurrency and audit ───────────────────────────────────────────────────────────────

    public int Revision { get; private set; }

    /// <summary>Whether the mission still appears in the directory. The inverse of archived.</summary>
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

    public static Result<Embassy> Create(
        EmbassyId id,
        string? countryCode,
        string? servesCountryCode,
        string? name,
        DateTimeOffset now,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var located = IntelligenceNormalization.Country(countryCode);
        var serves = IntelligenceNormalization.Country(servesCountryCode);

        if (located is null || serves is null)
        {
            return Result.Failure<Embassy>(IntelligenceErrors.CountryInvalid);
        }

        var trimmedName = IntelligenceNormalization.Text(name, IntelligenceLimits.MaxEmbassyName);

        if (trimmedName is null)
        {
            return Result.Failure<Embassy>(IntelligenceErrors.EmbassyNameInvalid);
        }

        var embassy = new Embassy(id, located, serves, trimmedName, now);

        embassy.RaiseChanged(IntelligenceChangeKinds.Created, createdBy);

        return Result.Success(embassy);
    }

    /// <summary>
    /// Rewrites the directory entry.
    /// <para>
    /// Neither country code can change. A mission that moved to a different country is a different
    /// mission, and a mission that started serving a different nationality is a diplomatic event
    /// rather than a typo — both are a new record and an archived old one, which is also the only
    /// version of the story that leaves the old entry findable.
    /// </para>
    /// </summary>
    public Result UpdateProfile(
        string? name,
        string? city,
        string? address,
        double? latitude,
        double? longitude,
        UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.EmbassyArchived);
        }

        var trimmedName = IntelligenceNormalization.Text(name, IntelligenceLimits.MaxEmbassyName);

        if (trimmedName is null)
        {
            return Result.Failure(IntelligenceErrors.EmbassyNameInvalid);
        }

        // Out-of-range coordinates are dropped rather than refused: an entry with a bad pin is still
        // a useful entry, and refusing the whole save would lose the address somebody just typed.
        var validCoordinates =
            latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

        Name = trimmedName;
        City = IntelligenceNormalization.Text(city, IntelligenceLimits.MaxRegion);
        Address = IntelligenceNormalization.Text(address, IntelligenceLimits.MaxAddress);
        Latitude = validCoordinates ? latitude : null;
        Longitude = validCoordinates ? longitude : null;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Rewrites how somebody reaches the mission.</summary>
    public Result UpdateContact(
        string? telephone,
        string? emergencyTelephone,
        string? email,
        string? website,
        string? appointmentUrl,
        bool requiresAppointment,
        UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.EmbassyArchived);
        }

        var site = NormalizeOptionalUrl(website, out var siteValid);
        var booking = NormalizeOptionalUrl(appointmentUrl, out var bookingValid);

        if (!siteValid || !bookingValid)
        {
            return Result.Failure(IntelligenceErrors.SourceUrlInvalid);
        }

        Telephone = IntelligenceNormalization.Phone(telephone);
        EmergencyTelephone = IntelligenceNormalization.Phone(emergencyTelephone);
        Email = IntelligenceNormalization.Email(email);
        Website = site;
        AppointmentUrl = booking;
        RequiresAppointment = requiresAppointment;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Sets the four headline capabilities every directory filter reads.</summary>
    public Result UpdateCapabilities(
        bool passports, bool visas, bool consular, bool emergencyAssistance, UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.EmbassyArchived);
        }

        OffersPassportServices = passports;
        OffersVisaServices = visas;
        OffersConsularServices = consular;
        OffersEmergencyAssistance = emergencyAssistance;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Links the mission to its issuing authority record, or clears the link.</summary>
    public Result LinkAuthority(IssuingAuthorityId? authorityId, UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.EmbassyArchived);
        }

        AuthorityId = authorityId;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Attaches or clears the building photograph. A B5 reference, never a URL.</summary>
    public Result SetMedia(Guid? mediaId, UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.EmbassyArchived);
        }

        MediaId = mediaId == Guid.Empty ? null : mediaId;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Collections
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the opening hours.
    /// <para>
    /// A repeated day keeps its first row rather than failing. A mission publishing two lines for
    /// Wednesday is transcribing a page that says "09:00–12:00" and "visa collection 14:00–15:00",
    /// and the honest fix is one row saying both — but refusing the save teaches the editor nothing
    /// they can act on at three in the afternoon.
    /// </para>
    /// </summary>
    public Result SetOfficeHours(
        IEnumerable<(DayOfWeek Day, string? Hours, bool IsClosed)>? hours, UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.EmbassyArchived);
        }

        var resolved = new List<EmbassyOfficeHours>(7);
        var seen = new HashSet<DayOfWeek>();

        foreach (var (day, text, isClosed) in hours ?? [])
        {
            var row = EmbassyOfficeHours.TryCreate(day, text, isClosed);

            if (row is not null && seen.Add(day))
            {
                resolved.Add(row);
            }
        }

        _officeHours.Clear();
        _officeHours.AddRange([.. resolved.OrderBy(row => row.Day)]);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the service list.</summary>
    public Result SetServices(
        IEnumerable<(string? Key, string? Name, string? Description, bool RequiresAppointment)>? services,
        UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.EmbassyArchived);
        }

        var requested = services?.ToList() ?? [];

        if (requested.Count > IntelligenceLimits.MaxEmbassyServices)
        {
            return Result.Failure(IntelligenceErrors.EmbassyServiceLimit);
        }

        var resolved = new List<EmbassyService>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, name, description, requiresAppointment) in requested)
        {
            var service = EmbassyService.TryCreate(
                key, name, description, requiresAppointment, resolved.Count);

            if (service is null)
            {
                return Result.Failure(IntelligenceErrors.EmbassyNameInvalid);
            }

            if (seen.Add(service.Key))
            {
                resolved.Add(service);
            }
        }

        _services.Clear();
        _services.AddRange(resolved);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the translation set.</summary>
    public Result SetLocalizations(
        IEnumerable<(string? Locale, string? Name, string? Description, string? Address)>? localizations,
        UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.EmbassyArchived);
        }

        var requested = localizations?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxLocalizations)
        {
            return Result.Failure(IntelligenceErrors.LocalizationLimit);
        }

        var resolved = new List<EmbassyLocalization>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (locale, name, description, address) in requested)
        {
            var localization = EmbassyLocalization.TryCreate(locale, name, description, address);

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

    /// <summary>The mission's name, description and address for a locale, with the usual fallback.</summary>
    public (string Name, string? Description, string? Address) Localized(string? locale)
    {
        var normalized = IntelligenceNormalization.Locale(locale);

        if (normalized is null)
        {
            return (Name, null, Address);
        }

        var exact = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, normalized, StringComparison.Ordinal));

        if (exact is not null)
        {
            return (exact.Name, exact.Description, exact.Address ?? Address);
        }

        var language = normalized.Split('-')[0];

        var byLanguage = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, language, StringComparison.Ordinal));

        return byLanguage is null
            ? (Name, null, Address)
            : (byLanguage.Name, byLanguage.Description, byLanguage.Address ?? Address);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Verification, soft delete and auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <inheritdoc cref="IssuingAuthority.RecordVerificationCase" />
    public void RecordVerificationCase(Guid caseId)
    {
        if (caseId != Guid.Empty)
        {
            VerificationCaseId = caseId;
        }
    }

    /// <inheritdoc cref="IssuingAuthority.ApplyVerification" />
    public void ApplyVerification(bool verified, DateTimeOffset now)
    {
        if (IsVerified == verified)
        {
            return;
        }

        IsVerified = verified;
        VerifiedAt = verified ? now : null;

        Bump();
        RaiseChanged(
            verified ? IntelligenceChangeKinds.Verified : IntelligenceChangeKinds.Rejected,
            changedBy: null);
    }

    public Result EnsureRevision(int? expectedRevision) =>
        expectedRevision is null || expectedRevision == Revision
            ? Result.Success()
            : Result.Failure(IntelligenceErrors.RevisionConflict);

    /// <inheritdoc cref="Intelligence.NoticeCategory.MarkDeleted" />
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Archived, Actor(userId));
    }

    /// <summary>Returns an archived mission to the directory, verification intact.</summary>
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
    /// Normalizes an optional URL, distinguishing "not supplied" from "supplied and unusable".
    /// <para>
    /// Both produce a null string, and only one of them is an error — which is exactly the
    /// distinction a single nullable return cannot carry, and the reason this takes an out parameter
    /// rather than returning one.
    /// </para>
    /// </summary>
    private static string? NormalizeOptionalUrl(string? value, out bool valid)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            valid = true;
            return null;
        }

        var normalized = IntelligenceNormalization.Url(value);

        valid = normalized is not null;

        return normalized;
    }

    private void Bump() => Revision++;

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(new EmbassyChangedDomainEvent
        {
            EmbassyId = Id,
            CountryCode = CountryCode,
            ServesCountryCode = ServesCountryCode,
            Name = Name,
            Change = change,
            IsVerified = IsVerified,
            ChangedBy = changedBy
        });

    private static UserId? Actor(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? UserId.From(parsed) : null;
}
