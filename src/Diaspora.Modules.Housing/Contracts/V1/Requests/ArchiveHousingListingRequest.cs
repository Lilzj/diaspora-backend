namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Takes a property off the market.
/// <para>
/// <see cref="Temporarily"/> is the difference between a landlord redecorating for a fortnight and
/// a landlord finished with the platform. A temporary withdrawal returns to available without
/// republication and keeps the listing age and view count; an archive needs a deliberate restore
/// through draft.
/// </para>
/// </summary>
public sealed record ArchiveHousingListingRequest
{
    /// <summary>True to make it merely unavailable rather than archived.</summary>
    public bool Temporarily { get; init; }

    /// <summary>Recorded on the lifecycle event and readable by the landlord.</summary>
    public string? Reason { get; init; }
}
