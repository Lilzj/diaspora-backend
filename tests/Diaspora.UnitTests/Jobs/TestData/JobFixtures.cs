using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.UnitTests.Jobs.TestData;

/// <summary>
/// Valid aggregates, built the way the handlers build them.
/// <para>
/// Every factory goes through the real <c>Create</c> and unwraps the result, so a fixture that
/// stopped being valid is a compile-or-throw here rather than a puzzling failure in a test that
/// was checking something else.
/// </para>
/// </summary>
internal static class JobFixtures
{
    public static readonly DateTimeOffset Now = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    // Named New* rather than after the types they return: a method called UserId() shadows the
    // UserId type inside this namespace and makes every other reference need qualifying.
    public static UserId NewUserId() => UserId.New();

    public static EmployerProfileId NewEmployerId() => EmployerProfileId.New();

    public static JobApplicationId NewApplicationId() => JobApplicationId.New();

    public static CandidateProfileId NewCandidateId() => CandidateProfileId.New();

    /// <summary>A publishable draft: title, description, category and country all present.</summary>
    public static JobPosting Posting(
        EmployerProfileId? employerId = null,
        string countryCode = "DE",
        WorkplaceType workplace = WorkplaceType.OnSite,
        bool sponsorsVisas = false) =>
        JobPosting.Create(
            JobPostingId.New(),
            employerId ?? NewEmployerId(),
            NewUserId(),
            JobTitle.Create("Registered Nurse").Value,
            JobSlug.Create("registered-nurse-berlin").Value,
            JobDescription.Create("Ward nursing on a busy acute unit.").Value,
            "healthcare",
            JobType.FullTime,
            ExperienceLevel.Mid,
            JobLocationContext.Create(countryCode, cityName: "Berlin").Value,
            WorkArrangement.Create(workplace, sponsorsVisas).Value,
            Now).Value;

    public static EmployerProfile Employer(
        UserId? ownerId = null,
        string displayName = "Charité Berlin",
        string countryCode = "DE",
        EmployerOrganizationType type = EmployerOrganizationType.Healthcare) =>
        EmployerProfile.Create(
            EmployerProfileId.New(),
            ownerId ?? NewUserId(),
            CompanyIdentity.Create(displayName, null, null, type).Value,
            JobSlug.Create("charite-berlin").Value,
            countryCode,
            Now).Value;

    public static CandidateProfile Candidate(UserId? userId = null) =>
        CandidateProfile.Create(
            CandidateProfileId.New(),
            userId ?? NewUserId(),
            CandidateHeadline.Create("Registered nurse, 8 years, ICU").Value,
            "NG",
            Now).Value;

    public static JobApplication Application(
        JobPostingId? postingId = null,
        EmployerProfileId? employerId = null,
        CandidateProfileId? candidateId = null,
        UserId? applicantId = null) =>
        JobApplication.Submit(
            JobApplicationId.New(),
            postingId ?? JobPostingId.New(),
            employerId ?? NewEmployerId(),
            candidateId ?? NewCandidateId(),
            applicantId ?? NewUserId(),
            resumeId: null,
            coverLetter: null,
            answers: [],
            attachments: [],
            Now).Value;
}
