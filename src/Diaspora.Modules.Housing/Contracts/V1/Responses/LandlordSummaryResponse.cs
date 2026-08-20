namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>A landlord as a card renders them.</summary>
public sealed record LandlordSummaryResponse
{
    public required Guid Id { get; init; }

    public required string Slug { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Individual, EstateAgent, PropertyManager, Hostel, Hotel and the rest.</summary>
    public required string Kind { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>Trust badge, verbatim. Null when unverified.</summary>
    public string? Badge { get; init; }

    public Guid? LogoMediaId { get; init; }

    /// <summary>The B7 provider behind an agency or hostel, where there is one.</summary>
    public Guid? ProviderId { get; init; }
}
