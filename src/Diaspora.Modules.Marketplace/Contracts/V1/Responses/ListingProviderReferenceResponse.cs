namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A shop in the Providers directory that stands behind this listing.
/// <para>
/// The name, badge and rating are B7's answers at read time rather than copies stored here, so a
/// shop that renamed itself or lost its verification is never misrepresented by a listing written
/// six months ago. When B7 cannot answer, the reference renders with its identifier alone rather
/// than failing the page.
/// </para>
/// </summary>
public sealed record ListingProviderReferenceResponse
{
    public required Guid ProviderId { get; init; }

    /// <summary>The shop's current name, from B7. Null when the directory could not answer.</summary>
    public string? Name { get; init; }

    /// <summary>Whether B7 says the shop is verified. Never cached here.</summary>
    public bool IsVerified { get; init; }

    /// <summary>What the shop is to this listing — "supplier", "repairs", "delivery".</summary>
    public string? Role { get; init; }

    public string? Note { get; init; }
}
