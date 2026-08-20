using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.Modules.Location.Integration;

namespace Diaspora.Modules.Location.Application.Mapping;

/// <summary>
/// Maps residence history to the wire.
/// <para>
/// The verification state arrives as a parameter rather than being read off the experience,
/// because the experience does not have one. That is the boundary made structural: a mapper
/// that could reach a verification flag from a <see cref="CountryExperience"/> would mean the
/// aggregate stored one, and the whole point is that Trust owns that fact.
/// </para>
/// </summary>
public static class ExperienceMapper
{
    public static CountryExperienceResponse ToResponse(
        CountryExperience experience,
        string countryName,
        ResidencyVerificationState verificationState,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(experience);

        return new CountryExperienceResponse
        {
            Id = experience.Id.Value,
            CountryCode = experience.CountryCode.Value,
            CountryName = countryName,
            Flag = experience.CountryCode.ToFlagEmoji(),
            ArrivedOn = experience.Period.ArrivedOn,
            DepartedOn = experience.Period.DepartedOn,

            // Year forms as well as dates, because the client's timeline renders
            // "2019 – Present" and computing that from a date is work every screen would
            // repeat.
            StartYear = experience.Period.ArrivedOn.Year,
            EndYear = experience.Period.DepartedOn?.Year,
            Years = experience.YearsAsAt(today),
            IsCurrentResidence = experience.IsCurrentResidence,
            ExperienceLevel = experience.LevelAsAt(today).ToString(),
            Role = experience.Role,
            LanguageFamiliarity = experience.LanguageFamiliarity.ToString(),
            CulturalFamiliarity = experience.CulturalFamiliarity.ToString(),
            IsVerified = verificationState == ResidencyVerificationState.Verified,
            VerificationState = verificationState.ToString(),
            ExpertiseAreas = experience.Expertise
                .OrderBy(claim => claim.Area, StringComparer.OrdinalIgnoreCase)
                .Select(claim => claim.Area)
                .ToList()
        };
    }

    /// <summary>
    /// The whole timeline, with the summary figures three separate screens would otherwise
    /// each compute — and eventually compute differently.
    /// </summary>
    public static CountryExperienceTimelineResponse ToTimeline(
        IReadOnlyList<CountryExperienceResponse> experiences)
    {
        ArgumentNullException.ThrowIfNull(experiences);

        return new CountryExperienceTimelineResponse
        {
            Experiences = experiences,
            CurrentResidence = experiences.FirstOrDefault(item => item.IsCurrentResidence),
            CountryCount = experiences.Count,
            VerifiedCountryCount = experiences.Count(item => item.IsVerified),
            TotalYearsAbroad = experiences.Sum(item => item.Years)
        };
    }

    /// <summary>Maps the cross-module summary — the shape other modules consume.</summary>
    public static CountryExperienceResponse ToResponse(ResidencySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new CountryExperienceResponse
        {
            // The public projection carries no experience identifier: another member's row id
            // is not theirs to know, and nothing they can do with it needs one.
            Id = Guid.Empty,
            CountryCode = summary.CountryCode,
            CountryName = summary.CountryName,
            Flag = summary.Flag,
            ArrivedOn = summary.ArrivedOn,
            DepartedOn = summary.DepartedOn,
            StartYear = summary.ArrivedOn.Year,
            EndYear = summary.DepartedOn?.Year,
            Years = summary.Years,
            IsCurrentResidence = summary.IsCurrentResidence,
            ExperienceLevel = summary.ExperienceLevel.ToString(),
            Role = null,
            LanguageFamiliarity = FamiliarityLevel.None.ToString(),
            CulturalFamiliarity = FamiliarityLevel.None.ToString(),
            IsVerified = summary.IsVerified,
            VerificationState = summary.VerificationState.ToString(),
            ExpertiseAreas = summary.ExpertiseAreas
        };
    }
}
