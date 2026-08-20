namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One way to reach a provider.
/// <para>
/// A list where the client has four named properties, because the brief requires social links
/// and a provider with two phone numbers is ordinary. <see cref="IsPrimary"/> is what preserves
/// the client's single "Call" button: it tells the client which number that button dials.
/// </para>
/// </summary>
public sealed record ProviderContactMethodResponse
{
    public required Guid ContactMethodId { get; init; }

    /// <summary>The <c>ContactMethodKind</c> name — <c>Phone</c>, <c>WhatsApp</c>, <c>Website</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Empty for <c>PlatformMessage</c>, where the provider id is the address.</summary>
    public required string Value { get; init; }

    public string? Label { get; init; }

    public required bool IsPrimary { get; init; }
}
