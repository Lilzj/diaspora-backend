namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>A country an employer says it hires into, and whether it sponsors there.</summary>
public sealed record HiringRegionResponse
{
    public required string CountryCode { get; init; }

    public required bool SponsorsVisas { get; init; }
}
