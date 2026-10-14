using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Guides.Integration;
using Diaspora.Modules.Providers.Integration;

namespace Diaspora.Modules.Jobs.Application.Mapping;

/// <summary>
/// Turns a posting into the shapes the wire carries.
/// <para>
/// Static, because it holds no state and makes no judgement beyond the one the caller passed in.
/// CA1822 in Release pushes stateless helpers this way, and B8 records the same for its own
/// mappers.
/// </para>
/// <para>
/// <b>The viewer-relative fields are parameters, not lookups.</b> A mapper that resolved
/// "has this person applied" itself would issue a query per card, so the caller batches those
/// answers once and hands them in. That is why <see cref="ToCard"/> takes sets rather than a
/// user.
/// </para>
/// </summary>
public static class JobMapper
{
    /// <summary>The detail response.</summary>
    public static JobResponse ToResponse(
        JobPosting posting,
        EmployerProfile employer,
        DateTimeOffset now,
        bool canManage,
        bool? hasApplied = null,
        bool? isSaved = null,
        IReadOnlyDictionary<Guid, ProviderSummary>? providers = null,
        IReadOnlyDictionary<Guid, GuideSummary>? guides = null)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(employer);

        return new JobResponse
        {
            JobId = posting.Id.Value,
            Slug = posting.Slug.Value,
            Title = posting.Title.Value,
            Summary = posting.Description.Summary,
            Description = posting.Description.Body,

            Employer = EmployerMapper.ToSummary(employer),

            CategoryKey = posting.CategoryKey,
            JobType = JobWireNames.ToWire(posting.JobType),
            ExperienceLevel = JobWireNames.ToWire(posting.ExperienceLevel),
            EducationLevel = JobWireNames.ToWire(posting.EducationLevel),
            Tags = [.. posting.Tags.Select(tag => tag.Slug)],
            Skills = [.. posting.Skills.Select(ToSkill)],
            Responsibilities = [.. posting.Responsibilities.Select(ToDetailLine)],
            Requirements = [.. posting.Requirements.Select(ToDetailLine)],
            Benefits = [.. posting.Benefits.Select(ToDetailLine)],

            Location = ToLocation(posting),
            WorkArrangement = ToWorkArrangement(posting),
            Salary = ToSalary(posting),

            Status = JobWireNames.ToWire(posting.Status),
            Visibility = JobWireNames.ToWire(posting.Visibility),
            IsFeatured = posting.IsFeatured,
            IsVerified = posting.TrustMark.IsVerified,
            VacancyCount = posting.VacancyCount,
            RemainingVacancies = posting.RemainingVacancies,
            RequiresResume = posting.RequiresResume,
            PublishedAt = posting.PublishedAt,
            ClosingAt = posting.ClosingAt,
            ClosureReason = posting.ClosureReason,

            Media = [.. posting.MediaReferences.Select(reference =>
                new JobMediaReferenceResponse
                {
                    MediaId = reference.MediaId,
                    Role = JobWireNames.ToWire(reference.Role),
                    Caption = reference.Caption
                })],

            Providers = [.. posting.ProviderReferences.Select(reference =>
                ToProviderReference(reference, providers))],

            Guides = [.. posting.GuideReferences.Select(reference =>
                ToGuideReference(reference, guides))],

            ScreeningQuestions = [.. posting.ScreeningQuestions.Select(question =>
                new JobScreeningQuestionResponse
                {
                    QuestionId = question.QuestionId,
                    Text = question.Text,
                    IsRequired = question.IsRequired
                })],

            Engagement = new JobEngagementResponse
            {
                ViewCount = posting.Engagement.ViewCount,
                ApplicationCount = posting.Engagement.ApplicationCount,
                SaveCount = posting.Engagement.SaveCount
            },

            Revision = posting.Revision,

            HasApplied = hasApplied,
            IsSaved = isSaved,
            CanApply = posting.AcceptsApplications(now),
            CanManage = canManage
        };
    }

    /// <summary>The list response. Deliberately far smaller than the detail one.</summary>
    public static JobCardResponse ToCard(
        JobPosting posting,
        EmployerProfile? employer,
        IReadOnlySet<Guid>? appliedPostingIds = null,
        IReadOnlySet<Guid>? savedPostingIds = null,
        string? recommendationReason = null)
    {
        ArgumentNullException.ThrowIfNull(posting);

        return new JobCardResponse
        {
            JobId = posting.Id.Value,
            Slug = posting.Slug.Value,
            Title = posting.Title.Value,
            Summary = posting.Description.Summary,

            EmployerId = posting.EmployerId.Value,

            // An employer the caller could not batch-resolve renders as a blank name rather than
            // failing the card. A results page missing one employer chip is a much smaller loss
            // than a results page that 500s.
            EmployerName = employer?.Identity.DisplayName ?? string.Empty,
            EmployerLogoMediaId = employer?.Branding.LogoMediaId,
            IsEmployerVerified = employer?.IsVerified ?? false,
            IsIntermediary = employer?.IsIntermediary ?? false,

            CategoryKey = posting.CategoryKey,
            JobType = JobWireNames.ToWire(posting.JobType),
            ExperienceLevel = JobWireNames.ToWire(posting.ExperienceLevel),

            CountryCode = posting.Location.CountryCode,
            LocationLabel = posting.Location.DisplayName,
            WorkplaceType = JobWireNames.ToWire(posting.WorkArrangement.WorkplaceType),
            IsRemote = posting.WorkArrangement.IsRemote,
            OffersVisaSponsorship = posting.WorkArrangement.OffersVisaSponsorship,
            OffersRelocationAssistance = posting.WorkArrangement.OffersRelocationAssistance,

            Salary = ToSalary(posting),

            Tags = [.. posting.Tags.Select(tag => tag.Slug)],

            // The top few only. A card renders chips, not a specification.
            Skills = [.. posting.Skills.Take(5).Select(skill => skill.DisplayName)],

            Status = JobWireNames.ToWire(posting.Status),
            IsFeatured = posting.IsFeatured,
            IsVerified = posting.TrustMark.IsVerified,
            PublishedAt = posting.PublishedAt,
            ClosingAt = posting.ClosingAt,
            ApplicationCount = posting.Engagement.ApplicationCount,

            HasApplied = appliedPostingIds?.Contains(posting.Id.Value),
            IsSaved = savedPostingIds?.Contains(posting.Id.Value),
            RecommendationReason = recommendationReason
        };
    }

    public static JobSalaryResponse ToSalary(JobPosting posting)
    {
        ArgumentNullException.ThrowIfNull(posting);

        return new JobSalaryResponse
        {
            IsDisclosed = posting.Salary.IsDisclosed,
            Minimum = posting.Salary.Minimum,
            Maximum = posting.Salary.Maximum,
            CurrencyCode = posting.Salary.CurrencyCode,
            Period = posting.Salary.Period is { } period ? JobWireNames.ToWire(period) : null,
            IsNegotiable = posting.Salary.IsNegotiable,
            IsUnpaid = posting.Salary.IsUnpaid
        };
    }

    private static JobLocationResponse ToLocation(JobPosting posting) =>
        new()
        {
            CountryCode = posting.Location.CountryCode,
            RegionName = posting.Location.RegionName,
            CityName = posting.Location.CityName,
            AreaId = posting.Location.AreaId,
            Latitude = posting.Latitude,
            Longitude = posting.Longitude,
            DisplayName = posting.Location.DisplayName
        };

    private static JobWorkArrangementResponse ToWorkArrangement(JobPosting posting) =>
        new()
        {
            WorkplaceType = JobWireNames.ToWire(posting.WorkArrangement.WorkplaceType),
            IsRemote = posting.WorkArrangement.IsRemote,
            OffersVisaSponsorship = posting.WorkArrangement.OffersVisaSponsorship,
            OffersRelocationAssistance = posting.WorkArrangement.OffersRelocationAssistance,
            OnSiteDaysPerWeek = posting.WorkArrangement.OnSiteDaysPerWeek,
            RemotePolicy = posting.WorkArrangement.RemotePolicy
        };

    private static JobSkillResponse ToSkill(JobSkillRequirement skill) =>
        new()
        {
            Key = skill.SkillKey,
            Name = skill.DisplayName,
            MinimumProficiency = JobWireNames.ToWire(skill.MinimumProficiency),
            IsEssential = skill.IsEssential
        };

    private static JobDetailLineResponse ToDetailLine(JobDetailLine line) =>
        new()
        {
            LineId = line.LineId,
            Text = line.Text,
            IsEssential = line.IsEssential
        };

    /// <summary>
    /// A provider reference, hydrated where B7 answered. An unresolved one keeps its editorial
    /// text and loses only the card — never the page.
    /// </summary>
    private static JobProviderReferenceResponse ToProviderReference(
        JobProviderReference reference, IReadOnlyDictionary<Guid, ProviderSummary>? providers)
    {
        var resolved = providers is not null
            && providers.TryGetValue(reference.ProviderId, out var found)
                ? found
                : null;

        return new JobProviderReferenceResponse
        {
            ProviderId = reference.ProviderId,
            Role = reference.Role,
            Note = reference.Note,
            Name = resolved?.Name,
            Slug = resolved?.Slug,
            IsVerified = resolved?.IsVerified,
            AverageRating = resolved?.AverageRating,
            LogoMediaId = resolved?.LogoMediaId
        };
    }

    /// <summary>
    /// A guide reference, hydrated where B8 answered. An unresolved one has a null title, which
    /// is the client's signal to omit the link — a posting must never link a guide that has been
    /// unpublished.
    /// </summary>
    private static JobGuideReferenceResponse ToGuideReference(
        JobGuideReference reference, IReadOnlyDictionary<Guid, GuideSummary>? guides)
    {
        var resolved = guides is not null
            && guides.TryGetValue(reference.GuideId, out var found)
                ? found
                : null;

        return new JobGuideReferenceResponse
        {
            GuideId = reference.GuideId,
            Label = reference.Label,
            IsAutomatic = reference.IsAutomatic,
            Title = resolved?.Title,
            Slug = resolved?.Slug,
            Summary = resolved?.Summary,
            IsVerified = resolved?.IsVerified
        };
    }
}
