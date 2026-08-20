namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>A B7 provider connected to a property. An identifier and a cached name.</summary>
public sealed record HousingProviderReferenceResponse
{
    public required Guid ProviderId { get; init; }

    public string? ProviderName { get; init; }

    /// <summary>What they do here: managing agent, letting agent, relocation partner.</summary>
    public string? Role { get; init; }
}
