namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Adds one way to reach a provider.</summary>
public sealed record ProviderContactMethodRequest
{
    /// <summary>The <c>ContactMethodKind</c> name.</summary>
    public required string Kind { get; init; }

    /// <summary>Ignored for <c>PlatformMessage</c>, where the provider id is the address.</summary>
    public string? Value { get; init; }

    public string? Label { get; init; }

    /// <summary>Whether the client's single action button for this kind should use it.</summary>
    public bool IsPrimary { get; init; }

    /// <summary>False for a number the provider keeps but does not want published.</summary>
    public bool IsPublic { get; init; } = true;
}
