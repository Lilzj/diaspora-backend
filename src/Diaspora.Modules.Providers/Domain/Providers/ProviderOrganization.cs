using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// The facts that only apply to a provider which is a legal or constituted body.
/// <para>
/// <b>Optional, and that is the point.</b> The brief is explicit that B7 must not "hardcode
/// assumptions about businesses only", and this is where that constraint lands: an individual
/// braider has no registration number, no legal name distinct from her own, no VAT number and
/// no charity number. Putting these on <see cref="Provider"/> would have meant nine nullable
/// columns on every row and a model that quietly reads as "a business, possibly missing some
/// details" rather than "a person or a business or a church".
/// </para>
/// <para>
/// It is one owned reference that is simply absent for most providers, which also makes the
/// permission natural: editing organizational detail is a different act from editing a profile,
/// and a verification reviewer reads this block and nothing else.
/// </para>
/// </summary>
public sealed class ProviderOrganization : Entity<ProviderOrganizationId>
{
    public const int MaxLegalNameLength = 300;

    public const int MaxRegistrationNumberLength = 64;

    public const int MaxJurisdictionLength = 120;

    private ProviderOrganization(ProviderOrganizationId id) : base(id) { }

    private ProviderOrganization() { }

    /// <summary>The name on the register, when it differs from the trading name.</summary>
    public string? LegalName { get; private set; }

    /// <summary>Company number, charity number, association registration.</summary>
    public string? RegistrationNumber { get; private set; }

    /// <summary>Where it is registered. "England and Wales", "Mauritius", "Ghana".</summary>
    public string? RegistrationJurisdiction { get; private set; }

    public DateOnly? RegisteredOn { get; private set; }

    /// <summary>Tax registration, where the provider chooses to publish it.</summary>
    public string? TaxIdentifier { get; private set; }

    /// <summary>Whether the body is a registered charity or non-profit.</summary>
    public bool IsNonProfit { get; private set; }

    /// <summary>
    /// Members of the organization itself — congregants, association members. Distinct from
    /// <see cref="ProviderMembership"/>, which is who may edit the listing. The client's
    /// <c>membersCount</c> on an organization means this one.
    /// </summary>
    public int? MembershipSize { get; private set; }

    /// <summary>Staff or volunteers. What a member reads as "how big are they".</summary>
    public int? StaffSize { get; private set; }

    public static ProviderOrganization Create(ProviderOrganizationId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new ProviderOrganization(id);
    }

    public Result Update(
        string? legalName,
        string? registrationNumber,
        string? registrationJurisdiction,
        DateOnly? registeredOn,
        string? taxIdentifier,
        bool isNonProfit,
        int? membershipSize,
        int? staffSize)
    {
        var trimmedLegalName = Trim(legalName);

        if (trimmedLegalName is { Length: > MaxLegalNameLength })
        {
            return Result.Failure(ProviderErrors.NameTooLong);
        }

        var trimmedNumber = Trim(registrationNumber);

        if (trimmedNumber is { Length: > MaxRegistrationNumberLength })
        {
            return Result.Failure(ProviderErrors.OrganizationNumberTooLong);
        }

        var trimmedJurisdiction = Trim(registrationJurisdiction);

        if (trimmedJurisdiction is { Length: > MaxJurisdictionLength })
        {
            return Result.Failure(ProviderErrors.OrganizationNumberTooLong);
        }

        var trimmedTax = Trim(taxIdentifier);

        if (trimmedTax is { Length: > MaxRegistrationNumberLength })
        {
            return Result.Failure(ProviderErrors.OrganizationNumberTooLong);
        }

        LegalName = trimmedLegalName;
        RegistrationNumber = trimmedNumber;
        RegistrationJurisdiction = trimmedJurisdiction;
        RegisteredOn = registeredOn;
        TaxIdentifier = trimmedTax;
        IsNonProfit = isNonProfit;
        MembershipSize = membershipSize is > 0 ? membershipSize : null;
        StaffSize = staffSize is > 0 ? staffSize : null;

        return Result.Success();
    }

    /// <summary>
    /// Whether there is enough here for Trust to review a business or organization claim.
    /// <para>
    /// Read by the verification submission path so a provider is told what is missing
    /// <em>before</em> a reviewer picks the case up and refuses it a day later.
    /// </para>
    /// </summary>
    public bool HasRegistrationEvidence =>
        !string.IsNullOrWhiteSpace(RegistrationNumber)
        && !string.IsNullOrWhiteSpace(RegistrationJurisdiction);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
