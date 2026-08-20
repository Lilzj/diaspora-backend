using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// A body whose statements this platform treats as official — a ministry, an immigration department,
/// a police service, a mission.
/// <para>
/// <b>Managed content, never an account.</b> Nothing here authenticates as a government: embassy
/// authentication and government system integration are both explicitly out of scope, and pretending
/// otherwise would put a login screen in front of an institution that will never use it. What this
/// records is who an editorial team says a notice came from, and whether anybody has checked.
/// </para>
/// <para>
/// <b>Verification is Trust's decision, not this module's.</b> The flag here is a projection of an
/// outcome Trust returned — ADR-002's separation, and the same shape B8 uses for a guide's trust
/// mark. A module that could set its own verified flag would have a badge that means "we said so".
/// </para>
/// <para>
/// <b>Publishing a notice requires a verified authority.</b> That single rule is what the word
/// "official" on the surface is worth: an unverified body can be recorded, notices can be drafted
/// against it, and none of them can go live until somebody confirms the body is what it claims.
/// </para>
/// </summary>
public sealed class IssuingAuthority
    : AggregateRoot<IssuingAuthorityId>, IAuditableEntity, ISoftDeletable
{
    private IssuingAuthority(
        IssuingAuthorityId id,
        string countryCode,
        string name,
        AuthorityType authorityType,
        DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        Name = name;
        AuthorityType = authorityType;
        CreatedAt = createdAt;
    }

    private IssuingAuthority() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — the country this body issues for.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>The body's full official name, as it writes it.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>The acronym members and staff actually use. Rendered on cards where space is short.</summary>
    public string? ShortName { get; private set; }

    public AuthorityType AuthorityType { get; private set; }

    public string? Description { get; private set; }

    /// <summary>The body's own site. Absolute http or https, checked on the way in.</summary>
    public string? Website { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    /// <summary>
    /// Whether Trust has confirmed this body is what it claims. Set only by the inbound projection.
    /// </summary>
    public bool IsVerified { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>
    /// The Trust case a submission opened, kept so an approval has somewhere to land. B8 shipped
    /// without this field once and every Trust decision arrived with nothing to correlate against.
    /// </summary>
    public Guid? VerificationCaseId { get; private set; }

    /// <summary>The optimistic concurrency token, bumped on every mutation.</summary>
    public int Revision { get; private set; }

    /// <summary>Whether notices may still name it. The inverse of archived.</summary>
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

    public static Result<IssuingAuthority> Create(
        IssuingAuthorityId id,
        string? countryCode,
        string? name,
        AuthorityType authorityType,
        DateTimeOffset now,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var country = IntelligenceNormalization.Country(countryCode);

        if (country is null)
        {
            return Result.Failure<IssuingAuthority>(IntelligenceErrors.CountryInvalid);
        }

        var trimmedName = IntelligenceNormalization.Text(
            name, IntelligenceLimits.MaxAuthorityName);

        if (trimmedName is null)
        {
            return Result.Failure<IssuingAuthority>(IntelligenceErrors.AuthorityNameInvalid);
        }

        var authority = new IssuingAuthority(
            id,
            country,
            trimmedName,
            Enum.IsDefined(authorityType) ? authorityType : AuthorityType.Other,
            now);

        authority.RaiseChanged(IntelligenceChangeKinds.Created, createdBy);

        return Result.Success(authority);
    }

    /// <summary>
    /// Rewrites the record.
    /// <para>
    /// <b>The country cannot change.</b> An authority that issues for a different country is a
    /// different authority, and allowing the move would silently reassign every notice ever filed
    /// against it to a country whose readers were never told.
    /// </para>
    /// </summary>
    public Result Update(
        string? name,
        string? shortName,
        AuthorityType authorityType,
        string? description,
        string? website,
        string? contactEmail,
        string? contactPhone,
        UserId? changedBy)
    {
        var trimmedName = IntelligenceNormalization.Text(
            name, IntelligenceLimits.MaxAuthorityName);

        if (trimmedName is null)
        {
            return Result.Failure(IntelligenceErrors.AuthorityNameInvalid);
        }

        if (!Enum.IsDefined(authorityType))
        {
            return Result.Failure(IntelligenceErrors.AuthorityNameInvalid);
        }

        string? site = null;

        if (!string.IsNullOrWhiteSpace(website))
        {
            site = IntelligenceNormalization.Url(website);

            if (site is null)
            {
                return Result.Failure(IntelligenceErrors.SourceUrlInvalid);
            }
        }

        Name = trimmedName;
        ShortName = IntelligenceNormalization.Text(
            shortName, IntelligenceLimits.MaxAuthorityShortName);
        AuthorityType = authorityType;
        Description = IntelligenceNormalization.Text(
            description, IntelligenceLimits.MaxAuthorityDescription);
        Website = site;
        ContactEmail = IntelligenceNormalization.Email(contactEmail);
        ContactPhone = IntelligenceNormalization.Phone(contactPhone);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Whether this body issues for the country given.</summary>
    public bool IssuesFor(string? countryCode) =>
        string.Equals(
            CountryCode, IntelligenceNormalization.Country(countryCode), StringComparison.Ordinal);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Verification
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Records the Trust case a submission opened, so a decision has somewhere to land.</summary>
    public void RecordVerificationCase(Guid caseId)
    {
        if (caseId != Guid.Empty)
        {
            VerificationCaseId = caseId;
        }
    }

    /// <summary>
    /// Applies a decision Trust returned.
    /// <para>
    /// The only path to a verified authority, and it is inbound. It raises the event with a
    /// verification verb rather than an update, because a consumer showing an "official" badge cares
    /// about this transition and about no other edit to the record.
    /// </para>
    /// </summary>
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

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete and auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <inheritdoc cref="NoticeCategory.MarkDeleted" />
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Archived, Actor(userId));
    }

    /// <summary>Returns an archived authority to the register, verification intact.</summary>
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

    private void Bump() => Revision++;

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(new IssuingAuthorityChangedDomainEvent
        {
            IssuingAuthorityId = Id,
            CountryCode = CountryCode,
            Name = Name,
            AuthorityType = AuthorityType,
            Change = change,
            IsVerified = IsVerified,
            ChangedBy = changedBy
        });

    private static UserId? Actor(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? UserId.From(parsed) : null;
}
