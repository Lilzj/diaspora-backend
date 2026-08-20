namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// Who owns the provider.
/// <para>
/// <b>The field is <c>userId</c>, not <c>providerId</c>.</b> The client's
/// <c>owner.providerId</c> holds a user identifier and its provider screen compares it against
/// <c>user?.userId</c> — the name is actively wrong, and carrying it into a frozen contract
/// would make the mistake permanent. See deviation D-7 in the frontend contract report.
/// </para>
/// <para>
/// Deliberately thin: an identifier and the display fields the card needs. B7 does not own
/// member profiles, and a fatter owner block here would be B7 quietly becoming a second read
/// model for Identity.
/// </para>
/// </summary>
public sealed record ProviderOwnerResponse
{
    /// <summary>Identity's user identifier.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Null when the caller may not see it, or when Identity has no record.</summary>
    public string? DisplayName { get; init; }

    /// <summary>ISO 3166-1 alpha-2. The client's origin-country filter reads this.</summary>
    public string? OriginCountryCode { get; init; }
}
