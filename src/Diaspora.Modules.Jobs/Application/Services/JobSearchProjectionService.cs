using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Search;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// Flattens a posting into the document a search engine ingests.
/// <para>
/// <b>The whole OpenSearch-independence claim narrows to this class.</b> Every value object,
/// strongly typed identifier and domain enum is turned into a primitive here, so an adapter can
/// serialise a <see cref="JobSearchDocument"/> with a project reference to the Extensibility
/// namespace and nothing else. The moment a domain type leaks through, the port stops being
/// engine-independent.
/// </para>
/// <para>
/// <b>The employer is required, not optional.</b> "Verified employers only" is the filter this
/// audience reaches for first, and an index that had to join to answer it is an index that does
/// not help. A posting whose employer cannot be resolved is not indexed at all rather than
/// indexed with a false badge — the failure mode of guessing here is the platform vouching for
/// an employer Trust never verified.
/// </para>
/// <para>
/// Static because it holds no state and makes no judgement: CA1822 in Release pushes it that
/// way, and B8 records the same for its own mappers.
/// </para>
/// </summary>
public static class JobSearchProjectionService
{
    /// <summary>
    /// Projects one posting. Null when it should not be in an index at all — deleted, draft,
    /// archived, hidden or not public.
    /// </summary>
    public static JobSearchDocument? Project(JobPosting posting, EmployerProfile employer)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(employer);

        if (!posting.IsDiscoverable)
        {
            return null;
        }

        return new JobSearchDocument
        {
            JobId = posting.Id.Value,
            Slug = posting.Slug.Value,
            Title = posting.Title.Value,
            Summary = posting.Description.Summary,
            Body = posting.Description.Body,

            EmployerId = employer.Id.Value,
            EmployerName = employer.Identity.DisplayName,
            IsEmployerVerified = employer.IsVerified,
            IsIntermediary = employer.IsIntermediary,

            CategoryKey = posting.CategoryKey,
            Tags = [.. posting.Tags.Select(tag => tag.Slug)],
            SkillKeys = [.. posting.Skills.Select(skill => skill.SkillKey)],
            SkillNames = [.. posting.Skills.Select(skill => skill.DisplayName)],
            JobType = JobWireNames.ToWire(posting.JobType),
            ExperienceLevel = JobWireNames.ToWire(posting.ExperienceLevel),
            EducationLevel = JobWireNames.ToWire(posting.EducationLevel),

            CountryCode = posting.Location.CountryCode,
            RegionName = posting.Location.RegionName,
            CityName = posting.Location.CityName,
            AreaPath = posting.Location.AreaPath,
            WorkplaceType = JobWireNames.ToWire(posting.WorkArrangement.WorkplaceType),
            IsRemote = posting.WorkArrangement.IsRemote,
            OffersVisaSponsorship = posting.WorkArrangement.OffersVisaSponsorship,
            OffersRelocationAssistance = posting.WorkArrangement.OffersRelocationAssistance,

            IsSalaryDisclosed = posting.Salary.IsDisclosed,
            SalaryMinimum = posting.Salary.Minimum,
            SalaryMaximum = posting.Salary.Maximum,
            SalaryCurrency = posting.Salary.CurrencyCode,
            SalaryPeriod = posting.Salary.Period is { } period
                ? JobWireNames.ToWire(period)
                : null,

            Status = JobWireNames.ToWire(posting.Status),

            // Computed against the aggregate's own clock-aware rule rather than from the status
            // alone, so a posting past its closing date is indexed as shut even before the
            // expiry sweep has written that down.
            IsOpen = posting.AcceptsApplications(DateTimeOffset.UtcNow),
            IsFeatured = posting.IsFeatured,
            IsVerified = posting.TrustMark.IsVerified,
            VacancyCount = posting.VacancyCount,
            PublishedAt = posting.PublishedAt ?? posting.CreatedAt,
            ClosingAt = posting.ClosingAt,

            ViewCount = posting.Engagement.ViewCount,
            ApplicationCount = posting.Engagement.ApplicationCount
        };
    }

    /// <summary>
    /// Projects several postings against their employers, dropping any that should not be
    /// indexed and any whose employer is missing. The reindex sweep's entry point.
    /// </summary>
    public static IReadOnlyList<JobSearchDocument> ProjectMany(
        IReadOnlyCollection<JobPosting> postings,
        IReadOnlyDictionary<Guid, EmployerProfile> employers)
    {
        ArgumentNullException.ThrowIfNull(postings);
        ArgumentNullException.ThrowIfNull(employers);

        var documents = new List<JobSearchDocument>(postings.Count);

        foreach (var posting in postings)
        {
            if (!employers.TryGetValue(posting.EmployerId.Value, out var employer))
            {
                // Not indexed rather than indexed with a guessed badge. See the class summary.
                continue;
            }

            var document = Project(posting, employer);

            if (document is not null)
            {
                documents.Add(document);
            }
        }

        return documents;
    }
}
