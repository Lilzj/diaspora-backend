namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Edits registration detail, for a provider that is a legal or constituted body.
/// <para>
/// A separate endpoint from the profile because it is a different act with a different audience:
/// a verification reviewer reads this block and nothing else, and an <c>Editor</c> who may
/// rewrite the description has no business changing a company number.
/// </para>
/// </summary>
public sealed record UpdateProviderOrganizationRequest
{
    public string? LegalName { get; init; }

    public string? RegistrationNumber { get; init; }

    public string? RegistrationJurisdiction { get; init; }

    public DateOnly? RegisteredOn { get; init; }

    public string? TaxIdentifier { get; init; }

    public bool IsNonProfit { get; init; }

    /// <summary>Congregants or association members. Not the people who may edit the listing.</summary>
    public int? MembershipSize { get; init; }

    public int? StaffSize { get; init; }
}
