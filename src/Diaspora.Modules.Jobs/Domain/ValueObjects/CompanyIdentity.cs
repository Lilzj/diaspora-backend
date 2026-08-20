using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// Who an employer says it is.
/// <para>
/// <b>Legal name and display name are separate fields on purpose.</b> Candidates recognise the
/// trading name and registries hold the legal one, and an employer forced to pick one will pick
/// the recognisable one — which is precisely the field Trust needs to be the registered one when
/// it checks. Keeping both means the badge can be earned against the legal entity while the card
/// shows the name people know.
/// </para>
/// <para>
/// <see cref="RegistrationNumber"/> is unvalidated free text, and deliberately so: every
/// jurisdiction on this platform formats company numbers differently, and a regex that
/// understood four of them would quietly reject employers from the rest.
/// </para>
/// </summary>
public sealed class CompanyIdentity : ValueObject
{
    private CompanyIdentity(
        string displayName,
        string? legalName,
        string? registrationNumber,
        EmployerOrganizationType organizationType,
        CompanySize size,
        int? foundedYear)
    {
        DisplayName = displayName;
        LegalName = legalName;
        RegistrationNumber = registrationNumber;
        OrganizationType = organizationType;
        Size = size;
        FoundedYear = foundedYear;
    }

    private CompanyIdentity() { }

    /// <summary>The trading name a card renders.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The registered entity name, when the employer supplied one.</summary>
    public string? LegalName { get; private set; }

    /// <summary>A company registration number, in whatever form the jurisdiction uses.</summary>
    public string? RegistrationNumber { get; private set; }

    public EmployerOrganizationType OrganizationType { get; private set; }

    public CompanySize Size { get; private set; }

    public int? FoundedYear { get; private set; }

    public static Result<CompanyIdentity> Create(
        string? displayName,
        string? legalName,
        string? registrationNumber,
        EmployerOrganizationType organizationType,
        CompanySize size = CompanySize.Unspecified,
        int? foundedYear = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure<CompanyIdentity>(JobErrors.EmployerNameInvalid);
        }

        var display = string.Join(' ', displayName.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (display.Length < JobLimits.MinEmployerName || display.Length > JobLimits.MaxEmployerName)
        {
            return Result.Failure<CompanyIdentity>(JobErrors.EmployerNameInvalid);
        }

        var legal = Clean(legalName);

        if (legal is { Length: > JobLimits.MaxEmployerName })
        {
            return Result.Failure<CompanyIdentity>(JobErrors.EmployerNameInvalid);
        }

        var registration = Clean(registrationNumber);

        if (registration is { Length: > JobLimits.MaxRegistrationNumber })
        {
            return Result.Failure<CompanyIdentity>(JobErrors.EmployerNameInvalid);
        }

        if (!Enum.IsDefined(organizationType) || !Enum.IsDefined(size))
        {
            return Result.Failure<CompanyIdentity>(JobErrors.EmployerNameInvalid);
        }

        // A founding year in the future is a typo; one before the first joint-stock company is
        // another. Both are worth catching, and neither is worth being clever about.
        if (foundedYear is { } year && (year < 1600 || year > 2200))
        {
            return Result.Failure<CompanyIdentity>(JobErrors.EmployerNameInvalid);
        }

        return Result.Success(new CompanyIdentity(
            display, legal, registration, organizationType, size, foundedYear));
    }

    /// <summary>
    /// Whether this employer is an intermediary. Candidates are entitled to know, so the card
    /// says so rather than leaving them to infer it from the description.
    /// </summary>
    public bool IsIntermediary =>
        OrganizationType is EmployerOrganizationType.RecruitmentAgency
            or EmployerOrganizationType.StaffingFirm;

    /// <summary>The name Trust should be checking. The legal one where there is one.</summary>
    public string VerifiableName => LegalName ?? DisplayName;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DisplayName;
        yield return LegalName;
        yield return RegistrationNumber;
        yield return OrganizationType;
        yield return Size;
        yield return FoundedYear;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
