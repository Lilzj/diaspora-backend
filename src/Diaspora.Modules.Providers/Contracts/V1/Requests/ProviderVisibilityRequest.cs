namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Sets who may see a provider.</summary>
public sealed record ProviderVisibilityRequest
{
    /// <summary>The <c>ProviderVisibilityLevel</c> name.</summary>
    public required string Level { get; init; }

    /// <summary>Required for <c>CountryCommunity</c>; ignored otherwise.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Required for <c>AreaOnly</c>; ignored otherwise.</summary>
    public Guid? AreaId { get; init; }
}
