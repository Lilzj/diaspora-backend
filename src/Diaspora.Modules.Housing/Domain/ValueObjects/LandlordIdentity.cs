using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// Who is letting the property, as the platform will name them.
/// <para>
/// <b>The display name and the legal name are different fields on purpose.</b> A member letting a
/// spare room trades as themselves; an agency trades as "Rose Hill Lettings" and is registered as
/// something duller. Trust verifies the legal one and the platform renders the other, and
/// collapsing them would either publish a registration document's wording on every card or submit
/// a trading name to a company register that has never heard of it.
/// </para>
/// <para>
/// <b><see cref="VerifiableName"/> is what goes to Trust</b>, and it is the legal name where there
/// is one. That choice is the whole reason the two are separate.
/// </para>
/// </summary>
public sealed class LandlordIdentity : ValueObject
{
    private LandlordIdentity(
        string displayName,
        LandlordKind kind,
        string? legalName,
        string? registrationNumber)
    {
        DisplayName = displayName;
        Kind = kind;
        LegalName = legalName;
        RegistrationNumber = registrationNumber;
    }

    private LandlordIdentity() { }

    /// <summary>What every card and profile renders.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public LandlordKind Kind { get; private set; }

    /// <summary>The registered name, for a business. Null for an individual.</summary>
    public string? LegalName { get; private set; }

    /// <summary>
    /// A company or letting-agency registration number. Passed to Trust in the opaque metadata bag
    /// and never parsed here - the moment Housing interpreted it, it would have learned one
    /// country's company-register format and broken in the next.
    /// </summary>
    public string? RegistrationNumber { get; private set; }

    /// <summary>Whether this is a business rather than a person. Drives which Trust claim is made.</summary>
    public bool IsBusiness => Kind != LandlordKind.Individual;

    /// <summary>The name submitted for verification. See the class summary.</summary>
    public string VerifiableName => LegalName ?? DisplayName;

    public static Result<LandlordIdentity> Create(
        string? displayName,
        LandlordKind kind,
        string? legalName = null,
        string? registrationNumber = null)
    {
        var name = Clamp(displayName, HousingLimits.MaxDisplayName);

        if (name is null)
        {
            return Result.Failure<LandlordIdentity>(HousingErrors.LandlordNameInvalid);
        }

        return Result.Success(new LandlordIdentity(
            name,
            kind,
            Clamp(legalName, HousingLimits.MaxDisplayName),
            Clamp(registrationNumber, HousingLimits.MaxDisplayName)));
    }

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? null : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DisplayName;
        yield return Kind;
        yield return LegalName;
        yield return RegistrationNumber;
    }
}
