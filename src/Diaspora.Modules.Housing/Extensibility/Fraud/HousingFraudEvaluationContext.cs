using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;

namespace Diaspora.Modules.Housing.Extensibility.Fraud;

/// <summary>
/// What a fraud provider is given.
/// <para>
/// The listing and the landlord together, because nearly every real rental-fraud signal is about
/// the relationship between them: a brand-new unverified profile advertising six furnished flats
/// well under the local rent, a landlord whose last three viewings nobody attended, a description
/// copied from a listing in another country.
/// </para>
/// </summary>
public sealed record HousingFraudEvaluationContext
{
    public required HousingListing Listing { get; init; }

    public required LandlordProfile Landlord { get; init; }

    /// <summary>
    /// Upheld reports already recorded against this landlord. Supplied rather than looked up, so a
    /// provider does not need a repository to ask the most useful question there is.
    /// </summary>
    public int UpheldReportsAgainstLandlord { get; init; }

    /// <summary>Live listings the landlord already holds.</summary>
    public int LandlordActiveListings { get; init; }

    /// <summary>When the evaluation is running. Passed in so a provider stays a pure function.</summary>
    public required DateTimeOffset Now { get; init; }
}
