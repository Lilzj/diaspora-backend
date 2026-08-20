namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One provider type the registration wizard may offer.
/// <para>
/// <see cref="RequiresRegistrationNumber"/> and <see cref="SupportsOrganizationDetail"/> are what
/// stop the wizard asking a braider for her company number, and what let a regulated market
/// demand one from a clinic — without either rule living in the app.
/// </para>
/// </summary>
public sealed record ProviderTypeOptionResponse
{
    /// <summary>The <c>ProviderType</c> name.</summary>
    public required string Type { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public required bool RequiresRegistrationNumber { get; init; }

    public required bool SupportsOrganizationDetail { get; init; }

    /// <summary>The <c>ProviderVerificationKind</c> names that apply to this type.</summary>
    public required IReadOnlyList<string> VerificationKinds { get; init; }

    /// <summary>The <c>ServiceDeliveryMode</c> the wizard should pre-select.</summary>
    public required string DefaultDeliveryMode { get; init; }
}
