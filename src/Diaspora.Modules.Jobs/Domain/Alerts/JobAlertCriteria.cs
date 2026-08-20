using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Alerts;

/// <summary>
/// What a standing alert watches for.
/// <para>
/// <b>A value object rather than a saved <c>JobQueryFilter</c>, and the distinction matters.</b>
/// A query filter is a rich object with paging, sorting, viewer identity and audience flags —
/// none of which means anything to a standing alert, and all of which would be dead weight
/// serialised into a row that outlives the code that wrote it. This is only the subset that
/// makes sense to evaluate against a newly published posting, and it is deliberately small
/// enough to store as columns rather than as JSON.
/// </para>
/// <para>
/// <b>An alert with no criteria is refused.</b> It would match every posting on the platform,
/// which is not an alert — it is a way for a member to make the platform send them a hundred
/// notifications a day until they turn notifications off entirely.
/// </para>
/// <para>
/// The delimited storage of keywords and countries is a deliberate simplification: an alert's
/// lists are tiny and only ever read whole, so a child table would buy a join to fetch four
/// strings.
/// </para>
/// </summary>
public sealed class JobAlertCriteria : ValueObject
{
    /// <summary>
    /// ASCII unit separator (0x1F). Written numerically rather than as a literal control
    /// character, because a literal one is invisible in a diff and does not survive every
    /// editor. It cannot occur in a keyword, a country code or a skill key — all three are
    /// normalised to letters, digits and hyphens before they reach here — so it is safe as a
    /// delimiter in a way that a comma or a semicolon would not be.
    /// </summary>
    private const char Separator = (char)0x1F;

    private JobAlertCriteria(
        string? keywords,
        string? countryCodes,
        string? categoryKey,
        string? skillKeys,
        WorkplaceType? workplaceType,
        JobType? jobType,
        ExperienceLevel? minimumExperience,
        bool visaSponsorshipOnly,
        bool verifiedEmployersOnly,
        decimal? minimumSalary,
        string? salaryCurrency)
    {
        Keywords = keywords;
        CountryCodes = countryCodes;
        CategoryKey = categoryKey;
        SkillKeys = skillKeys;
        WorkplaceType = workplaceType;
        JobType = jobType;
        MinimumExperience = minimumExperience;
        VisaSponsorshipOnly = visaSponsorshipOnly;
        VerifiedEmployersOnly = verifiedEmployersOnly;
        MinimumSalary = minimumSalary;
        SalaryCurrency = salaryCurrency;
    }

    private JobAlertCriteria() { }

    /// <summary>Unit-separated. Read through <see cref="KeywordList"/>.</summary>
    public string? Keywords { get; private set; }

    /// <summary>Unit-separated ISO 3166-1 alpha-2 codes.</summary>
    public string? CountryCodes { get; private set; }

    public string? CategoryKey { get; private set; }

    /// <summary>Unit-separated normalised skill keys.</summary>
    public string? SkillKeys { get; private set; }

    public WorkplaceType? WorkplaceType { get; private set; }

    public JobType? JobType { get; private set; }

    public ExperienceLevel? MinimumExperience { get; private set; }

    public bool VisaSponsorshipOnly { get; private set; }

    public bool VerifiedEmployersOnly { get; private set; }

    public decimal? MinimumSalary { get; private set; }

    /// <summary>Required whenever a minimum is set. No conversion happens anywhere.</summary>
    public string? SalaryCurrency { get; private set; }

    public IReadOnlyList<string> KeywordList => Split(Keywords);

    public IReadOnlyList<string> CountryList => Split(CountryCodes);

    public IReadOnlyList<string> SkillList => Split(SkillKeys);

    public static Result<JobAlertCriteria> Create(
        IReadOnlyList<string>? keywords = null,
        IReadOnlyList<string>? countryCodes = null,
        string? categoryKey = null,
        IReadOnlyList<string>? skills = null,
        WorkplaceType? workplaceType = null,
        JobType? jobType = null,
        ExperienceLevel? minimumExperience = null,
        bool visaSponsorshipOnly = false,
        bool verifiedEmployersOnly = false,
        decimal? minimumSalary = null,
        string? salaryCurrency = null)
    {
        var terms = Normalize(keywords, JobLimits.MaxAlertKeywords, value =>
        {
            var trimmed = value.Trim().ToLowerInvariant();

            return trimmed.Length is >= JobLimits.MinSearchTerm and <= JobLimits.MaxTagLength
                ? trimmed
                : null;
        });

        var countries = Normalize(countryCodes, JobLimits.MaxAlertCountries, value =>
        {
            var trimmed = value.Trim().ToUpperInvariant();

            return trimmed.Length == 2 && trimmed.All(char.IsAsciiLetterUpper) ? trimmed : null;
        });

        var skillKeys = Normalize(
            skills, JobLimits.MaxAlertKeywords, JobSkillRequirement.NormalizeKey);

        var category = categoryKey is null ? null : Catalog.JobCategory.NormalizeKey(categoryKey);

        string? currency = null;

        if (minimumSalary is not null)
        {
            if (string.IsNullOrWhiteSpace(salaryCurrency))
            {
                return Result.Failure<JobAlertCriteria>(JobErrors.CurrencyInvalid);
            }

            currency = salaryCurrency.Trim().ToUpperInvariant();

            if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
            {
                return Result.Failure<JobAlertCriteria>(JobErrors.CurrencyInvalid);
            }

            if (minimumSalary < 0)
            {
                return Result.Failure<JobAlertCriteria>(JobErrors.SalaryInvalid);
            }
        }

        var criteria = new JobAlertCriteria(
            Join(terms),
            Join(countries),
            category,
            Join(skillKeys),
            workplaceType,
            jobType,
            minimumExperience,
            visaSponsorshipOnly,
            verifiedEmployersOnly,
            minimumSalary,
            currency);

        return criteria.IsEmpty
            ? Result.Failure<JobAlertCriteria>(JobErrors.AlertCriteriaEmpty)
            : Result.Success(criteria);
    }

    /// <summary>
    /// Whether the alert watches for nothing at all. Flags alone do not count as criteria: an
    /// alert for "any verified employer anywhere" is every posting on the platform.
    /// </summary>
    public bool IsEmpty =>
        Keywords is null
        && CountryCodes is null
        && CategoryKey is null
        && SkillKeys is null
        && WorkplaceType is null
        && JobType is null
        && MinimumExperience is null
        && MinimumSalary is null;

    /// <summary>
    /// Turns the alert into the filter the posting repository understands, so a sweep and a
    /// candidate's own search run through exactly the same query path. An alert that matched
    /// differently from the search that created it would be the worst kind of bug: silent, and
    /// only visible as jobs the candidate never heard about.
    /// </summary>
    public JobQueryFilter ToQueryFilter(DateTimeOffset? publishedAfter) =>
        new()
        {
            Text = KeywordList.Count > 0 ? string.Join(' ', KeywordList) : null,
            CountryCode = CountryList.Count == 1 ? CountryList[0] : null,
            CategoryKey = CategoryKey,
            SkillKeys = SkillList.Count > 0 ? SkillList : null,
            JobTypes = JobType is { } type ? [type] : null,
            WorkplaceTypes = WorkplaceType is { } workplace ? [workplace] : null,
            MinimumExperience = MinimumExperience,
            VisaSponsorshipOnly = VisaSponsorshipOnly,
            VerifiedEmployersOnly = VerifiedEmployersOnly,
            MinimumSalary = MinimumSalary,
            SalaryCurrency = SalaryCurrency,
            PublishedAfter = publishedAfter,
            OpenOnly = true,
            Sort = JobSortOrder.Newest
        };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Keywords;
        yield return CountryCodes;
        yield return CategoryKey;
        yield return SkillKeys;
        yield return WorkplaceType;
        yield return JobType;
        yield return MinimumExperience;
        yield return VisaSponsorshipOnly;
        yield return VerifiedEmployersOnly;
        yield return MinimumSalary;
        yield return SalaryCurrency;
    }

    private static List<string> Normalize(
        IReadOnlyList<string>? values, int max, Func<string, string?> normalize)
    {
        var result = new List<string>();

        if (values is null)
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || result.Count >= max)
            {
                continue;
            }

            var normalized = normalize(value);

            if (normalized is not null && seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static string? Join(List<string> values) =>
        values.Count == 0 ? null : string.Join(Separator, values);

    private static string[] Split(string? value) =>
        string.IsNullOrEmpty(value)
            ? []
            : value.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
}
