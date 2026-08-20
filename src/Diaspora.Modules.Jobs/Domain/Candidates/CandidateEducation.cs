using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.ValueObjects;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// One qualification on a candidate's education history.
/// <para>
/// <b>The country the qualification was awarded in is a first-class field, and on this platform
/// it is not a detail.</b> Recognition of foreign qualifications is one of the central
/// obstacles diaspora professionals face, and a profile that records a nursing degree without
/// recording where it was awarded has thrown away the fact that determines whether it counts.
/// </para>
/// <para>
/// <see cref="Level"/> is the comparable rung; <see cref="Qualification"/> is what the
/// certificate actually says. Both, because a filter needs the first and a human needs the
/// second.
/// </para>
/// </summary>
public sealed class CandidateEducation : IHasEmploymentPeriod
{
    private CandidateEducation(
        Guid entryId,
        string institution,
        string? qualification,
        string? fieldOfStudy,
        EducationLevel level,
        string? countryCode,
        EmploymentPeriod period,
        int order)
    {
        EntryId = entryId;
        Institution = institution;
        Qualification = qualification;
        FieldOfStudy = fieldOfStudy;
        Level = level;
        CountryCode = countryCode;
        Period = period;
        Order = order;
    }

    private CandidateEducation() { }

    public Guid EntryId { get; private set; }

    public string Institution { get; private set; } = string.Empty;

    /// <summary>What the award is called. "BSc (Hons) Adult Nursing".</summary>
    public string? Qualification { get; private set; }

    public string? FieldOfStudy { get; private set; }

    /// <summary>The comparable rung. What a posting's minimum is checked against.</summary>
    public EducationLevel Level { get; private set; }

    /// <summary>Where it was awarded. Decides whether it is recognised, so it is never optional
    /// by accident.</summary>
    public string? CountryCode { get; private set; }

    public EmploymentPeriod Period { get; private set; } = default!;

    public int Order { get; private set; }

    internal static CandidateEducation? Create(
        Guid entryId,
        string? institution,
        string? qualification,
        string? fieldOfStudy,
        EducationLevel level,
        string? countryCode,
        EmploymentPeriod period,
        int order)
    {
        ArgumentNullException.ThrowIfNull(period);

        var school = Clip(institution, JobLimits.MaxOrganizationName);

        if (school is null || !Enum.IsDefined(level))
        {
            return null;
        }

        return new CandidateEducation(
            entryId == Guid.Empty ? Guid.NewGuid() : entryId,
            school,
            Clip(qualification, JobLimits.MaxDisplayName),
            Clip(fieldOfStudy, JobLimits.MaxDisplayName),
            level,
            CandidateWorkAuthorization.NormalizeCountry(countryCode),
            period,
            order);
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
