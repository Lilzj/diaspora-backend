using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// A member's professional self. The other half of the platform from
/// <c>EmployerProfile</c>.
/// <para>
/// <b>Private by default, and that default is the most important decision in this file.</b>
/// Someone job-hunting while employed can be dismissed for being found, and a diaspora member's
/// profile carries their nationality, their visa status and where they live. A product that
/// defaults to discoverable has made that trade on their behalf and got it wrong for exactly
/// the people with the most to lose. Applying is unaffected — a candidate who applies has chosen
/// to show that employer their profile, and <see cref="CandidateVisibility.Private"/> never
/// hides it from an employer they approached.
/// </para>
/// <para>
/// <b>What it owns:</b> skills, work history, education, certifications, languages,
/// work authorisations, preferred locations and portfolio. All of it is loaded together because
/// the profile is only ever read whole. <b>What it does not:</b> résumés, which are a separate
/// aggregate — a candidate may hold ten versions, applications reference them individually, and
/// loading a profile to render a name should not drag ten file references with it.
/// </para>
/// <para>
/// <b>Nothing here is verified, and the module must not pretend otherwise.</b> Every claim is
/// the candidate's own. Trust owns verification, résumé parsing is out of scope, and the
/// platform runs no background checks.
/// </para>
/// </summary>
public sealed class CandidateProfile
    : AggregateRoot<CandidateProfileId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private readonly List<CandidateSkill> _skills = [];
    private readonly List<CandidateExperience> _experience = [];
    private readonly List<CandidateEducation> _education = [];
    private readonly List<CandidateCertification> _certifications = [];
    private readonly List<CandidateLanguage> _languages = [];
    private readonly List<CandidateWorkAuthorization> _workAuthorizations = [];
    private readonly List<CandidatePreferredLocation> _preferredLocations = [];
    private readonly List<CandidatePortfolioItem> _portfolio = [];

    private CandidateProfile(
        CandidateProfileId id,
        UserId userId,
        CandidateHeadline headline,
        string? countryCode,
        DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        Headline = headline;
        CountryCode = countryCode;
        SalaryExpectation = SalaryExpectation.Unstated;
        Visibility = CandidateVisibility.Private;
        RemotePreference = RemotePreference.Flexible;
        ExperienceLevel = ExperienceLevel.Entry;
        HighestEducation = EducationLevel.None;
        IsOpenToWork = true;
        CreatedAt = createdAt;
    }

    private CandidateProfile() { }

    // ── Identity ────────────────────────────────────────────────────────────────────────────

    /// <summary>The member this profile belongs to. One profile per member.</summary>
    public UserId UserId { get; private set; } = default!;

    /// <summary>The platform's ownership seam. The member owns their own profile.</summary>
    public Guid OwnerId => UserId.Value;

    public CandidateHeadline Headline { get; private set; } = default!;

    /// <summary>Where the candidate currently is. Upper-case ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; private set; }

    public string? CityName { get; private set; }

    // ── Preferences ─────────────────────────────────────────────────────────────────────────

    /// <summary>Who may see this profile. Private until the candidate says otherwise.</summary>
    public CandidateVisibility Visibility { get; private set; }

    /// <summary>Whether the candidate is looking right now.</summary>
    public bool IsOpenToWork { get; private set; }

    public RemotePreference RemotePreference { get; private set; }

    /// <summary>Never disclosed to an employer. A filter input on the candidate's own searches.</summary>
    public SalaryExpectation SalaryExpectation { get; private set; } = default!;

    /// <summary>Where the candidate places themselves. Their claim, not a computed figure.</summary>
    public ExperienceLevel ExperienceLevel { get; private set; }

    /// <summary>The highest rung on their education history. Denormalised for filtering.</summary>
    public EducationLevel HighestEducation { get; private set; }

    /// <summary>How soon they could start, in days. Null when unstated.</summary>
    public int? NoticePeriodDays { get; private set; }

    /// <summary>The optimistic concurrency token.</summary>
    public int Revision { get; private set; }

    // ── Owned collections ───────────────────────────────────────────────────────────────────

    public IReadOnlyList<CandidateSkill> Skills =>
        _skills.OrderBy(skill => skill.Order).ToList().AsReadOnly();

    public IReadOnlyList<CandidateExperience> Experience =>
        _experience.OrderBy(entry => entry.Order).ToList().AsReadOnly();

    public IReadOnlyList<CandidateEducation> Education =>
        _education.OrderBy(entry => entry.Order).ToList().AsReadOnly();

    public IReadOnlyList<CandidateCertification> Certifications =>
        _certifications.OrderBy(entry => entry.Order).ToList().AsReadOnly();

    public IReadOnlyList<CandidateLanguage> Languages =>
        _languages.OrderBy(entry => entry.Order).ToList().AsReadOnly();

    public IReadOnlyList<CandidateWorkAuthorization> WorkAuthorizations =>
        _workAuthorizations.OrderBy(entry => entry.Order).ToList().AsReadOnly();

    public IReadOnlyList<CandidatePreferredLocation> PreferredLocations =>
        _preferredLocations.OrderBy(entry => entry.Order).ToList().AsReadOnly();

    /// <summary>The brief's "Portfolio", modelled as an owned collection. See the item type.</summary>
    public IReadOnlyList<CandidatePortfolioItem> Portfolio =>
        _portfolio.OrderBy(entry => entry.Order).ToList().AsReadOnly();

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// How complete the profile is, as a percentage. The candidate dashboard's nudge.
    /// <para>
    /// Weighted by what actually gets someone hired rather than by field count: work history and
    /// skills carry most of it, because an employer skimming twenty applications reads those and
    /// little else.
    /// </para>
    /// </summary>
    public int CompletenessPercent
    {
        get
        {
            var score = 20; // A headline exists by construction.

            score += _skills.Count > 0 ? 25 : 0;
            score += _experience.Count > 0 ? 25 : 0;
            score += _education.Count > 0 ? 10 : 0;
            score += _languages.Count > 0 ? 5 : 0;
            score += _workAuthorizations.Count > 0 ? 10 : 0;
            score += Headline.Summary is not null ? 5 : 0;

            return Math.Min(100, score);
        }
    }

    /// <summary>Whether this candidate can work in a country without the employer sponsoring.</summary>
    public bool CanWorkIn(string countryCode, DateOnly asOf)
    {
        var country = CandidateWorkAuthorization.NormalizeCountry(countryCode);

        if (country is null)
        {
            return false;
        }

        return _workAuthorizations.Exists(authorization =>
            string.Equals(authorization.CountryCode, country, StringComparison.Ordinal)
            && authorization.Kind != WorkAuthorizationKind.RequiresSponsorship
            && !authorization.HasExpired(asOf));
    }

    /// <summary>Whether this candidate would need sponsorship to work in a country.</summary>
    public bool NeedsSponsorshipFor(string countryCode, DateOnly asOf) =>
        !CanWorkIn(countryCode, asOf);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a profile with the minimum that makes it useful: a headline. Everything else
    /// arrives through later edits, because a profile wizard that demands a full work history
    /// before it saves anything is a profile wizard nobody finishes.
    /// </summary>
    public static Result<CandidateProfile> Create(
        CandidateProfileId id,
        UserId userId,
        CandidateHeadline headline,
        string? countryCode,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(headline);

        // Optional, unlike on a posting: a candidate part-way through moving genuinely may not
        // have an answer, and refusing the profile over it helps nobody.
        var country = countryCode is null
            ? null
            : CandidateWorkAuthorization.NormalizeCountry(countryCode);

        if (countryCode is not null && country is null)
        {
            return Result.Failure<CandidateProfile>(JobErrors.CountryInvalid);
        }

        var profile = new CandidateProfile(id, userId, headline, country, now);

        profile.RaiseDomainEvent(new CandidateProfileCreatedDomainEvent
        {
            CandidateId = id,
            UserId = userId,
            Headline = headline.Headline
        });

        return Result.Success(profile);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrency
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rejects a save based on a revision the editor no longer holds.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(JobErrors.RevisionConflict)
            : Result.Success();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Profile
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Updates the headline, location and working preferences.</summary>
    public Result UpdateProfile(
        CandidateHeadline headline,
        string? countryCode,
        string? cityName,
        ExperienceLevel experienceLevel,
        RemotePreference remotePreference,
        SalaryExpectation salaryExpectation,
        int? noticePeriodDays,
        bool isOpenToWork,
        int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(headline);
        ArgumentNullException.ThrowIfNull(salaryExpectation);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var country = countryCode is null
            ? null
            : CandidateWorkAuthorization.NormalizeCountry(countryCode);

        if (countryCode is not null && country is null)
        {
            return Result.Failure(JobErrors.CountryInvalid);
        }

        if (!Enum.IsDefined(experienceLevel) || !Enum.IsDefined(remotePreference))
        {
            return Result.Failure(JobErrors.HeadlineInvalid);
        }

        // Two years' notice is not a notice period, it is a typo.
        if (noticePeriodDays is < 0 or > 365)
        {
            return Result.Failure(JobErrors.HeadlineInvalid);
        }

        var city = string.IsNullOrWhiteSpace(cityName) ? null : cityName.Trim();

        if (city is { Length: > JobLimits.MaxLocationName })
        {
            return Result.Failure(JobErrors.PreferredLocationInvalid);
        }

        var changed = new List<string>();

        if (!Headline.Equals(headline))
        {
            Headline = headline;
            changed.Add("headline");
        }

        if (!string.Equals(CountryCode, country, StringComparison.Ordinal)
            || !string.Equals(CityName, city, StringComparison.Ordinal))
        {
            CountryCode = country;
            CityName = city;
            changed.Add("location");
        }

        if (ExperienceLevel != experienceLevel
            || RemotePreference != remotePreference
            || NoticePeriodDays != noticePeriodDays
            || IsOpenToWork != isOpenToWork
            || !SalaryExpectation.Equals(salaryExpectation))
        {
            ExperienceLevel = experienceLevel;
            RemotePreference = remotePreference;
            NoticePeriodDays = noticePeriodDays;
            IsOpenToWork = isOpenToWork;
            SalaryExpectation = salaryExpectation;
            changed.Add("preferences");
        }

        if (changed.Count == 0)
        {
            return Result.Success();
        }

        Touch();
        RaiseUpdated(changed);

        return Result.Success();
    }

    /// <summary>
    /// Changes who may see the profile.
    /// <para>
    /// Its own mutator rather than a field on the profile save, because it is the one setting a
    /// candidate changes urgently and on its own — and burying it in a form that also rewrites
    /// their work history would make "hide me now" a risky operation.
    /// </para>
    /// </summary>
    public Result ChangeVisibility(CandidateVisibility visibility)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.CandidateNotFound);
        }

        if (!Enum.IsDefined(visibility))
        {
            return Result.Failure(JobErrors.HeadlineInvalid);
        }

        if (Visibility == visibility)
        {
            return Result.Success();
        }

        var previous = Visibility;
        Visibility = visibility;
        Touch();

        RaiseDomainEvent(new CandidateVisibilityChangedDomainEvent
        {
            CandidateId = Id,
            UserId = UserId,
            PreviousVisibility = previous,
            CurrentVisibility = visibility
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Collections — every one replaced wholesale
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Replaces the skill set. Deduplicated on the normalised key.</summary>
    public Result SetSkills(IReadOnlyList<CandidateSkillDraft> skills, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(skills);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (skills.Count > JobLimits.MaxCandidateSkills)
        {
            return Result.Failure(JobErrors.SkillLimit);
        }

        var rebuilt = new List<CandidateSkill>(skills.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draft in skills)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.SkillInvalid);
            }

            var skill = CandidateSkill.Create(
                draft.Name, draft.Proficiency, draft.YearsOfExperience, rebuilt.Count);

            if (skill is null)
            {
                return Result.Failure(JobErrors.SkillInvalid);
            }

            if (seen.Add(skill.SkillKey))
            {
                rebuilt.Add(skill);
            }
        }

        _skills.Clear();
        _skills.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["skills"]);

        return Result.Success();
    }

    /// <summary>Replaces the work history, most recent first.</summary>
    public Result SetExperience(
        IReadOnlyList<CandidateExperienceDraft> entries, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (entries.Count > JobLimits.MaxExperience)
        {
            return Result.Failure(JobErrors.ExperienceLimit);
        }

        var rebuilt = new List<CandidateExperience>(entries.Count);

        // Ordered by start date descending, so the profile always reads most-recent-first
        // regardless of what order the client happened to send.
        foreach (var draft in entries.Where(entry => entry is not null)
                     .OrderByDescending(entry => entry.Start))
        {
            var period = EmploymentPeriod.Create(draft.Start, draft.End, draft.IsCurrent);

            if (period.IsFailure)
            {
                return Result.Failure(period.Error);
            }

            var entry = CandidateExperience.Create(
                draft.EntryId,
                draft.Title,
                draft.Organization,
                draft.CountryCode,
                draft.CityName,
                period.Value,
                draft.Description,
                rebuilt.Count);

            if (entry is null)
            {
                return Result.Failure(JobErrors.ExperienceInvalid);
            }

            rebuilt.Add(entry);
        }

        if (rebuilt.Count != entries.Count)
        {
            return Result.Failure(JobErrors.ExperienceInvalid);
        }

        _experience.Clear();
        _experience.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["experience"]);

        return Result.Success();
    }

    /// <summary>Replaces the education history and recomputes the highest rung.</summary>
    public Result SetEducation(IReadOnlyList<CandidateEducationDraft> entries, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (entries.Count > JobLimits.MaxEducation)
        {
            return Result.Failure(JobErrors.EducationLimit);
        }

        var rebuilt = new List<CandidateEducation>(entries.Count);

        foreach (var draft in entries.Where(entry => entry is not null)
                     .OrderByDescending(entry => entry.Start))
        {
            var period = EmploymentPeriod.Create(draft.Start, draft.End, draft.IsCurrent);

            if (period.IsFailure)
            {
                return Result.Failure(period.Error);
            }

            var entry = CandidateEducation.Create(
                draft.EntryId,
                draft.Institution,
                draft.Qualification,
                draft.FieldOfStudy,
                draft.Level,
                draft.CountryCode,
                period.Value,
                rebuilt.Count);

            if (entry is null)
            {
                return Result.Failure(JobErrors.EducationInvalid);
            }

            rebuilt.Add(entry);
        }

        if (rebuilt.Count != entries.Count)
        {
            return Result.Failure(JobErrors.EducationInvalid);
        }

        _education.Clear();
        _education.AddRange(rebuilt);

        // Denormalised so a posting's minimum-education filter is a column comparison rather
        // than a join across every qualification the candidate holds.
        HighestEducation = _education.Count == 0
            ? EducationLevel.None
            : _education.Max(entry => entry.Level);

        Touch();
        RaiseUpdated(["education"]);

        return Result.Success();
    }

    /// <summary>Replaces the certifications.</summary>
    public Result SetCertifications(
        IReadOnlyList<CandidateCertificationDraft> entries, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (entries.Count > JobLimits.MaxCertifications)
        {
            return Result.Failure(JobErrors.CertificationLimit);
        }

        var rebuilt = new List<CandidateCertification>(entries.Count);

        foreach (var draft in entries)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.CertificationInvalid);
            }

            var entry = CandidateCertification.Create(
                draft.EntryId,
                draft.Name,
                draft.Issuer,
                draft.CredentialId,
                draft.CountryCode,
                draft.IssuedOn,
                draft.ExpiresOn,
                draft.MediaId,
                rebuilt.Count);

            if (entry is null)
            {
                return Result.Failure(JobErrors.CertificationInvalid);
            }

            rebuilt.Add(entry);
        }

        _certifications.Clear();
        _certifications.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["certifications"]);

        return Result.Success();
    }

    /// <summary>Replaces the languages. Deduplicated on the code.</summary>
    public Result SetLanguages(IReadOnlyList<CandidateLanguageDraft> entries, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (entries.Count > JobLimits.MaxLanguages)
        {
            return Result.Failure(JobErrors.LanguageLimit);
        }

        var rebuilt = new List<CandidateLanguage>(entries.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draft in entries)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.LanguageInvalid);
            }

            var entry = CandidateLanguage.Create(draft.Code, draft.Proficiency, rebuilt.Count);

            if (entry is null)
            {
                return Result.Failure(JobErrors.LanguageInvalid);
            }

            if (seen.Add(entry.Code))
            {
                rebuilt.Add(entry);
            }
        }

        _languages.Clear();
        _languages.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["languages"]);

        return Result.Success();
    }

    /// <summary>Replaces the work authorisations. Deduplicated on the country.</summary>
    public Result SetWorkAuthorizations(
        IReadOnlyList<CandidateWorkAuthorizationDraft> entries, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (entries.Count > JobLimits.MaxWorkAuthorizations)
        {
            return Result.Failure(JobErrors.WorkAuthorizationLimit);
        }

        var rebuilt = new List<CandidateWorkAuthorization>(entries.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draft in entries)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.WorkAuthorizationInvalid);
            }

            var entry = CandidateWorkAuthorization.Create(
                draft.CountryCode, draft.Kind, draft.ExpiresOn, rebuilt.Count);

            if (entry is null)
            {
                return Result.Failure(JobErrors.WorkAuthorizationInvalid);
            }

            // One answer per country. A candidate cannot both be a citizen of somewhere and
            // need sponsorship for it, and keeping both would make every filter ambiguous.
            if (seen.Add(entry.CountryCode))
            {
                rebuilt.Add(entry);
            }
        }

        _workAuthorizations.Clear();
        _workAuthorizations.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["authorizations"]);

        return Result.Success();
    }

    /// <summary>Replaces the preferred locations.</summary>
    public Result SetPreferredLocations(
        IReadOnlyList<CandidatePreferredLocationDraft> entries, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (entries.Count > JobLimits.MaxPreferredLocations)
        {
            return Result.Failure(JobErrors.PreferredLocationLimit);
        }

        var rebuilt = new List<CandidatePreferredLocation>(entries.Count);

        foreach (var draft in entries)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.PreferredLocationInvalid);
            }

            var entry = CandidatePreferredLocation.Create(
                draft.CountryCode, draft.CityName, draft.WillRelocate, rebuilt.Count);

            if (entry is null)
            {
                return Result.Failure(JobErrors.PreferredLocationInvalid);
            }

            rebuilt.Add(entry);
        }

        _preferredLocations.Clear();
        _preferredLocations.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["preferences"]);

        return Result.Success();
    }

    /// <summary>Replaces the portfolio.</summary>
    public Result SetPortfolio(
        IReadOnlyList<CandidatePortfolioItemDraft> entries, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (entries.Count > JobLimits.MaxPortfolioItems)
        {
            return Result.Failure(JobErrors.PortfolioLimit);
        }

        var rebuilt = new List<CandidatePortfolioItem>(entries.Count);

        foreach (var draft in entries)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.PortfolioLinkInvalid);
            }

            var item = CandidatePortfolioItem.Create(
                draft.ItemId, draft.Label, draft.Url, draft.MediaId, draft.Description,
                rebuilt.Count);

            if (item is null)
            {
                return Result.Failure(JobErrors.PortfolioLinkInvalid);
            }

            rebuilt.Add(item);
        }

        _portfolio.Clear();
        _portfolio.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["portfolio"]);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks the profile deleted, and forces it private on the way out.
    /// <para>
    /// The visibility change is not redundant with the soft-delete filter. A rebuilt search
    /// index, a cache with a long tail or a consumer that mis-handles the delete would all still
    /// honour a private flag, and a deleted candidate must not be discoverable by any of those
    /// routes. Defence in depth, on the one field where being wrong hurts a person.
    /// </para>
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        Visibility = CandidateVisibility.Private;
        IsOpenToWork = false;
    }

    /// <summary>Undoes a soft delete. The profile comes back private, whatever it was before.</summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        Visibility = CandidateVisibility.Private;
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

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    private void Touch() => Revision++;

    private Result EnsureEditable(int? baseRevision) =>
        IsDeleted
            ? Result.Failure(JobErrors.CandidateNotFound)
            : EnsureRevision(baseRevision);

    private void RaiseUpdated(IReadOnlyList<string> changedFields) =>
        RaiseDomainEvent(new CandidateProfileUpdatedDomainEvent
        {
            CandidateId = Id,
            UserId = UserId,
            ChangedFields = changedFields
        });
}
