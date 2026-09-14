using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Resumes;

namespace Diaspora.Modules.Jobs.Application.Mapping;

/// <summary>
/// Turns a candidate profile into the shapes the wire carries.
/// <para>
/// <b><paramref name="forOwner"/> gates the salary expectation, and that is the whole reason the
/// flag exists.</b> A candidate's expected salary is a filter input on their own searches and is
/// never disclosed to an employer — handing one side of a negotiation the other side's floor is
/// a thing job products do, and one this platform will not. Deciding it in the mapper means no
/// route can leak it by forgetting.
/// </para>
/// </summary>
public static class CandidateMapper
{
    public static CandidateProfileResponse ToResponse(
        CandidateProfile profile, DateOnly asOf, bool forOwner)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new CandidateProfileResponse
        {
            CandidateId = profile.Id.Value,
            UserId = profile.UserId.Value,
            Headline = profile.Headline.IsStated ? profile.Headline.Headline : null,
            Summary = profile.Headline.Summary,
            CountryCode = profile.CountryCode,
            CityName = profile.CityName,
            ExperienceLevel = JobWireNames.ToWire(profile.ExperienceLevel),
            HighestEducation = JobWireNames.ToWire(profile.HighestEducation),
            RemotePreference = JobWireNames.ToWire(profile.RemotePreference),
            NoticePeriodDays = profile.NoticePeriodDays,
            IsOpenToWork = profile.IsOpenToWork,
            Visibility = JobWireNames.ToWire(profile.Visibility),

            // Owner only. See the class summary.
            SalaryExpectation = forOwner && profile.SalaryExpectation.IsStated
                ? new CandidateSalaryExpectationResponse
                {
                    Minimum = profile.SalaryExpectation.Minimum,
                    CurrencyCode = profile.SalaryExpectation.CurrencyCode,
                    Period = profile.SalaryExpectation.Period is { } period
                        ? JobWireNames.ToWire(period)
                        : null,
                    IsFlexible = profile.SalaryExpectation.IsFlexible
                }
                : null,

            Skills = [.. profile.Skills.Select(skill => new CandidateSkillResponse
            {
                Key = skill.SkillKey,
                Name = skill.DisplayName,
                Proficiency = JobWireNames.ToWire(skill.Proficiency),
                YearsOfExperience = skill.YearsOfExperience
            })],

            Experience = [.. profile.Experience.Select(entry => new CandidateExperienceResponse
            {
                EntryId = entry.EntryId,
                Title = entry.Title,
                Organization = entry.Organization,
                CountryCode = entry.CountryCode,
                CityName = entry.CityName,
                Start = entry.Period.Start,
                End = entry.Period.End,
                IsCurrent = entry.Period.IsCurrent,
                Description = entry.Description
            })],

            Education = [.. profile.Education.Select(entry => new CandidateEducationResponse
            {
                EntryId = entry.EntryId,
                Institution = entry.Institution,
                Qualification = entry.Qualification,
                FieldOfStudy = entry.FieldOfStudy,
                Level = JobWireNames.ToWire(entry.Level),
                CountryCode = entry.CountryCode,
                Start = entry.Period.Start,
                End = entry.Period.End,
                IsCurrent = entry.Period.IsCurrent
            })],

            Certifications = [.. profile.Certifications.Select(entry =>
                new CandidateCertificationResponse
                {
                    EntryId = entry.EntryId,
                    Name = entry.Name,
                    Issuer = entry.Issuer,
                    CredentialId = entry.CredentialId,
                    CountryCode = entry.CountryCode,
                    IssuedOn = entry.IssuedOn,
                    ExpiresOn = entry.ExpiresOn,

                    // Computed here so every reader gets the same answer. A lapsed licence is
                    // worse than none, and a client working it out itself will get it wrong.
                    HasExpired = entry.HasExpired(asOf),
                    MediaId = entry.MediaId
                })],

            Languages = [.. profile.Languages.Select(entry => new CandidateLanguageResponse
            {
                Code = entry.Code,
                Proficiency = JobWireNames.ToWire(entry.Proficiency)
            })],

            WorkAuthorizations = [.. profile.WorkAuthorizations.Select(entry =>
                new CandidateWorkAuthorizationResponse
                {
                    CountryCode = entry.CountryCode,
                    Kind = JobWireNames.ToWire(entry.Kind),
                    ExpiresOn = entry.ExpiresOn,
                    IsUnrestricted = entry.IsUnrestricted,
                    HasExpired = entry.HasExpired(asOf)
                })],

            PreferredLocations = [.. profile.PreferredLocations.Select(entry =>
                new CandidatePreferredLocationResponse
                {
                    CountryCode = entry.CountryCode,
                    CityName = entry.CityName,
                    WillRelocate = entry.WillRelocate
                })],

            Portfolio = [.. profile.Portfolio.Select(item => new CandidatePortfolioItemResponse
            {
                ItemId = item.ItemId,
                Label = item.Label,
                Url = item.Url,
                MediaId = item.MediaId,
                Description = item.Description
            })],

            // A nudge for the owner. Telling an employer a profile is 40% complete would be the
            // platform editorialising about a candidate.
            CompletenessPercent = forOwner ? profile.CompletenessPercent : null,

            Revision = profile.Revision
        };
    }

    public static ResumeResponse ToResponse(Resume resume)
    {
        ArgumentNullException.ThrowIfNull(resume);

        return new ResumeResponse
        {
            ResumeId = resume.Id.Value,
            MediaId = resume.MediaId,
            Label = resume.Label,
            FileName = resume.FileName,
            SizeInBytes = resume.SizeInBytes,
            ContentType = resume.ContentType,
            IsDefault = resume.IsDefault,
            UseCount = resume.UseCount,
            UploadedAt = resume.CreatedAt
        };
    }
}
