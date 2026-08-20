using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Joins a member's declared residence history to Trust's published verification decisions.
/// <para>
/// The join is the whole service, and it exists in one place for a reason. Every consumer that
/// wants "the countries this person has verifiably lived in" would otherwise write the same
/// three steps — fetch the experiences, fetch the projections, match on country code and check
/// expiry — and the expiry check is the one that gets forgotten. A verification that lapsed
/// last week is not a verification, and a consumer that only read the state enum would show a
/// badge that should have gone.
/// </para>
/// </summary>
public sealed class ResidencyDirectory : IResidencyDirectory
{
    private readonly ICountryExperienceRepository _experiences;
    private readonly IVerifiedResidencyRepository _residencies;
    private readonly CountryNameResolver _countryNames;
    private readonly IClock _clock;

    public ResidencyDirectory(
        ICountryExperienceRepository experiences,
        IVerifiedResidencyRepository residencies,
        CountryNameResolver countryNames,
        IClock clock)
    {
        _experiences = experiences;
        _residencies = residencies;
        _countryNames = countryNames;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ResidencySummary>> GetResidenciesAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var experiences = await _experiences.ListByUserAsync(userId, cancellationToken);
        var residencies = await _residencies.ListByUserAsync(userId, cancellationToken);
        var countries = await _countryNames.AllAsync(cancellationToken);

        return Combine(experiences, residencies, countries);
    }

    public async Task<bool> IsResidencyVerifiedAsync(
        UserId userId, CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(countryCode);

        var residency = await _residencies.GetAsync(userId, countryCode, cancellationToken);

        return residency?.IsVerifiedAt(_clock.UtcNow) == true;
    }

    public async Task<IReadOnlyDictionary<UserId, IReadOnlyList<ResidencySummary>>> GetResidenciesAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<UserId, IReadOnlyList<ResidencySummary>>();
        }

        // Two queries for the whole page, not two per member.
        var experiences = await _experiences.ListByUsersAsync(userIds, cancellationToken);
        var residencies = await _residencies.ListByUsersAsync(userIds, cancellationToken);
        var countries = await _countryNames.AllAsync(cancellationToken);

        return userIds
            .Distinct()
            .ToDictionary(
                userId => userId,
                userId => (IReadOnlyList<ResidencySummary>)Combine(
                    experiences.GetValueOrDefault(userId) ?? [],
                    residencies.GetValueOrDefault(userId) ?? [],
                    countries));
    }

    private List<ResidencySummary> Combine(
        IReadOnlyList<CountryExperience> experiences,
        IReadOnlyList<VerifiedResidency> residencies,
        IReadOnlyDictionary<string, Country> countries)
    {
        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var byCountry = residencies.ToDictionary(
            residency => residency.CountryCode.Value, StringComparer.Ordinal);

        return experiences
            .OrderByDescending(experience => experience.Period.ArrivedOn)
            .Select(experience =>
            {
                var residency = byCountry.GetValueOrDefault(experience.CountryCode.Value);

                return new ResidencySummary
                {
                    CountryCode = experience.CountryCode.Value,
                    CountryName = countries.GetValueOrDefault(experience.CountryCode.Value)?.Name.Value
                                  ?? experience.CountryCode.Value,
                    Flag = experience.CountryCode.ToFlagEmoji(),
                    ArrivedOn = experience.Period.ArrivedOn,
                    DepartedOn = experience.Period.DepartedOn,
                    IsCurrentResidence = experience.IsCurrentResidence,
                    Years = experience.YearsAsAt(today),
                    ExperienceLevel = experience.LevelAsAt(today),
                    VerificationState = ResolveState(residency, now),
                    ExpertiseAreas = experience.Expertise
                        .Select(claim => claim.Area)
                        .ToList()
                };
            })
            .ToList();
    }

    /// <summary>
    /// Collapses a stored state and an expiry into the state that is true right now.
    /// <para>
    /// A grant whose expiry has passed reports <see cref="ResidencyVerificationState.Expired"/>
    /// even though the row still says Verified. The row is what Trust last said; this is what
    /// is true. Reconciling them is a background sweep's job, and until it runs the read path
    /// must not report a lapsed verification as current.
    /// </para>
    /// </summary>
    private static ResidencyVerificationState ResolveState(
        VerifiedResidency? residency, DateTimeOffset now)
    {
        if (residency is null)
        {
            return ResidencyVerificationState.Unverified;
        }

        if (residency.State != ResidencyVerificationState.Verified)
        {
            return residency.State;
        }

        return residency.IsVerifiedAt(now)
            ? ResidencyVerificationState.Verified
            : ResidencyVerificationState.Expired;
    }
}
