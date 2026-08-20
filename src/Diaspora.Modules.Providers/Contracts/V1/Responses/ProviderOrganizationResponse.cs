namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// Registration detail, for a provider that is a legal or constituted body.
/// <para>
/// <b>Absent for most providers, and that is the design.</b> An individual braider has no
/// registration number and no legal name distinct from her own; a model that always returned
/// this block, full of nulls, would read as "a business, possibly missing details" rather than
/// "a person or a business or a church".
/// </para>
/// </summary>
public sealed record ProviderOrganizationResponse
{
    public string? LegalName { get; init; }

    public string? RegistrationNumber { get; init; }

    public string? RegistrationJurisdiction { get; init; }

    public DateOnly? RegisteredOn { get; init; }

    public string? TaxIdentifier { get; init; }

    public required bool IsNonProfit { get; init; }

    /// <summary>Congregants, association members. Distinct from the people who may edit.</summary>
    public int? MembershipSize { get; init; }

    public int? StaffSize { get; init; }
}
