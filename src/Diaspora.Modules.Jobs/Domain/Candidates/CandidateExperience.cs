using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.ValueObjects;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// One position on a candidate's work history.
/// <para>
/// <b>The employer name is free text and is never resolved against
/// <c>EmployerProfile</c>.</b> A candidate's previous jobs were overwhelmingly not on this
/// platform, and a history that could only name registered employers would be blank for
/// everyone it matters to. It is also the right boundary: a candidate saying where they worked
/// is not a claim about that organisation, and linking it would imply the organisation had
/// confirmed it.
/// </para>
/// <para>
/// The country is kept because it is genuinely load-bearing here: "five years' nursing" reads
/// differently to a recruiter depending on where it was done, and a candidate's international
/// experience is the thing this platform exists to make legible.
/// </para>
/// </summary>
public sealed class CandidateExperience : IHasEmploymentPeriod
{
    private CandidateExperience(
        Guid entryId,
        string title,
        string organization,
        string? countryCode,
        string? cityName,
        EmploymentPeriod period,
        string? description,
        int order)
    {
        EntryId = entryId;
        Title = title;
        Organization = organization;
        CountryCode = countryCode;
        CityName = cityName;
        Period = period;
        Description = description;
        Order = order;
    }

    private CandidateExperience() { }

    public Guid EntryId { get; private set; }

    /// <summary>The role held.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Who they worked for. Free text, deliberately unresolved.</summary>
    public string Organization { get; private set; } = string.Empty;

    /// <summary>Upper-case ISO 3166-1 alpha-2, when the candidate said where.</summary>
    public string? CountryCode { get; private set; }

    public string? CityName { get; private set; }

    public EmploymentPeriod Period { get; private set; } = default!;

    public string? Description { get; private set; }

    public int Order { get; private set; }

    /// <summary>Whether this is the candidate's current position.</summary>
    public bool IsCurrent => Period.IsCurrent;

    internal static CandidateExperience? Create(
        Guid entryId,
        string? title,
        string? organization,
        string? countryCode,
        string? cityName,
        EmploymentPeriod period,
        string? description,
        int order)
    {
        ArgumentNullException.ThrowIfNull(period);

        var role = Clip(title, JobLimits.MaxDisplayName);
        var employer = Clip(organization, JobLimits.MaxOrganizationName);

        if (role is null || employer is null)
        {
            return null;
        }

        var country = CandidateWorkAuthorization.NormalizeCountry(countryCode);
        var city = Clip(cityName, JobLimits.MaxLocationName);
        var body = Clip(description, JobLimits.MaxExperienceDescription);

        return new CandidateExperience(
            entryId == Guid.Empty ? Guid.NewGuid() : entryId,
            role, employer, country, city, period, body, order);
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }
}
