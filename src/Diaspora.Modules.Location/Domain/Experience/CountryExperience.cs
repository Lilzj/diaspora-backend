using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Events;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Experience;

/// <summary>
/// A member's declared residence in one country: when they arrived, whether they are still
/// there, and what they know about the place.
/// <para>
/// <b>This is the claim, not the proof.</b> The distinction is the load-bearing boundary
/// between B4 and B3, and the reason this aggregate has no <c>IsVerified</c> property. A
/// member saying they lived in the UK from 2019 is a geographic fact they assert and may
/// amend; whether that assertion has been evidenced is a Trust decision, held in
/// <see cref="Residency.VerifiedResidency"/> as a projection of what Trust published and
/// joined at read time. Storing a verification flag here would give two modules authority
/// over one fact, which is precisely the failure ADR-002 exists to prevent — and the flag
/// would be the stale one.
/// </para>
/// <para>
/// One aggregate per member per country. A member who left and returned amends the period
/// rather than creating a second row: two overlapping periods in one country are not a
/// richer history, they are a data-entry error that doubles every derived years-of-residence
/// figure.
/// </para>
/// </summary>
public sealed class CountryExperience : AggregateRoot<CountryExperienceId>, IAuditableEntity
{
    /// <summary>
    /// Enough for a member to name every subject they genuinely know; low enough that the
    /// list stays a signal. A profile claiming expertise in thirty areas claims none.
    /// </summary>
    public const int MaxExpertiseAreas = 12;

    public const int MaxRoleLength = 60;

    private readonly List<CountryExpertise> _expertise = [];

    public UserId UserId { get; private set; } = default!;

    public CountryCode CountryCode { get; private set; } = default!;

    public ResidencePeriod Period { get; private set; } = default!;

    /// <summary>
    /// What the member was there as — "Diaspora Member", "Student", "Worker". Free text
    /// because the frontend's timeline renders it verbatim and the set is open.
    /// </summary>
    public string? Role { get; private set; }

    public FamiliarityLevel LanguageFamiliarity { get; private set; }

    public FamiliarityLevel CulturalFamiliarity { get; private set; }

    public IReadOnlyList<CountryExpertise> Expertise => _expertise.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private CountryExperience() { }

    private CountryExperience(
        CountryExperienceId id,
        UserId userId,
        CountryCode countryCode,
        ResidencePeriod period,
        string? role) : base(id)
    {
        UserId = userId;
        CountryCode = countryCode;
        Period = period;
        Role = role;
    }

    public static Result<CountryExperience> Declare(
        UserId userId,
        CountryCode countryCode,
        ResidencePeriod period,
        string? role = null)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(countryCode);
        ArgumentNullException.ThrowIfNull(period);

        var experience = new CountryExperience(
            CountryExperienceId.New(), userId, countryCode, period, Normalize(role));

        experience.RaiseDomainEvent(new CountryExperienceDeclaredDomainEvent
        {
            ExperienceId = experience.Id,
            UserId = userId,
            CountryCode = countryCode.Value,
            ArrivedOn = period.ArrivedOn,
            DepartedOn = period.DepartedOn
        });

        return Result.Success(experience);
    }

    /// <summary>Whether this is where the member currently lives.</summary>
    public bool IsCurrentResidence => Period.IsOpen;

    /// <summary>
    /// The level this residence has earned as at <paramref name="today"/>.
    /// <para>
    /// Computed rather than stored, so a member who has been somewhere four years does not
    /// have to be re-saved on the anniversary to become Seasoned.
    /// </para>
    /// </summary>
    public ExperienceLevel LevelAsAt(DateOnly today) => Period.ExperienceAsAt(today);

    public int YearsAsAt(DateOnly today) => Period.YearsAsAt(today);

    public Result AmendPeriod(ResidencePeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        var wasOpen = Period.IsOpen;
        Period = period;

        if (wasOpen && !period.IsOpen)
        {
            RaiseDomainEvent(new CountryExperienceEndedDomainEvent
            {
                ExperienceId = Id,
                UserId = UserId,
                CountryCode = CountryCode.Value,
                DepartedOn = period.DepartedOn!.Value
            });
        }

        return Result.Success();
    }

    public void SetRole(string? role) => Role = Normalize(role);

    public void SetFamiliarity(FamiliarityLevel language, FamiliarityLevel cultural)
    {
        LanguageFamiliarity = language;
        CulturalFamiliarity = cultural;
    }

    /// <summary>
    /// Adds an expertise area, or raises the confidence on one already claimed.
    /// <para>
    /// Idempotent rather than duplicating, because the frontend submits the whole
    /// <c>expertiseAreas</c> array on every save and a naive add would grow the list on each
    /// one.
    /// </para>
    /// </summary>
    public Result<CountryExpertise> ClaimExpertise(
        string area, FamiliarityLevel confidence, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(area))
        {
            return Result.Failure<CountryExpertise>(Error.Validation(
                LocationErrorCodes.ExpertiseDuplicate, "An expertise area is required."));
        }

        var existing = _expertise.FirstOrDefault(claim => claim.IsSameArea(area));

        if (existing is not null)
        {
            existing.SetConfidence(confidence);
            return Result.Success(existing);
        }

        if (_expertise.Count >= MaxExpertiseAreas)
        {
            return Result.Failure<CountryExpertise>(Error.Validation(
                LocationErrorCodes.ExpertiseLimitReached,
                $"You can claim at most {MaxExpertiseAreas} expertise areas per country."));
        }

        var claimed = CountryExpertise.Create(Id, area, confidence, at);
        _expertise.Add(claimed);

        RaiseDomainEvent(new CountryExpertiseClaimedDomainEvent
        {
            ExperienceId = Id,
            UserId = UserId,
            CountryCode = CountryCode.Value,
            Area = claimed.Area
        });

        return Result.Success(claimed);
    }

    public Result WithdrawExpertise(string area)
    {
        var existing = _expertise.FirstOrDefault(claim => claim.IsSameArea(area));

        if (existing is null)
        {
            return Result.Failure(Error.NotFound(
                LocationErrorCodes.ExperienceNotFound, $"No expertise is claimed in '{area}'."));
        }

        _expertise.Remove(existing);
        return Result.Success();
    }

    /// <summary>
    /// Replaces the whole expertise set, which is what the frontend's save actually means.
    /// </summary>
    public Result ReplaceExpertise(IReadOnlyCollection<string> areas, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(areas);

        var distinct = areas
            .Where(area => !string.IsNullOrWhiteSpace(area))
            .Select(area => area.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count > MaxExpertiseAreas)
        {
            return Result.Failure(Error.Validation(
                LocationErrorCodes.ExpertiseLimitReached,
                $"You can claim at most {MaxExpertiseAreas} expertise areas per country."));
        }

        foreach (var stale in _expertise
                     .Where(claim => !distinct.Any(claim.IsSameArea))
                     .ToList())
        {
            _expertise.Remove(stale);
        }

        foreach (var area in distinct)
        {
            var result = ClaimExpertise(area, FamiliarityLevel.Conversational, at);

            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }
        }

        return Result.Success();
    }

    private static string? Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        var trimmed = role.Trim();
        return trimmed.Length <= MaxRoleLength ? trimmed : trimmed[..MaxRoleLength];
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
}
