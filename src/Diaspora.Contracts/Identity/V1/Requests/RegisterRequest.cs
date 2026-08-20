namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Everything Diaspora asks for at sign-up. Email is deliberately absent: onboarding
/// runs on a phone number, and an address can be added later from the profile.
/// </summary>
public sealed record RegisterRequest
{
    /// <summary>The member's full name, as they wish to be addressed.</summary>
    public required string FullName { get; init; }

    /// <summary>Phone number in E.164 form, for example "+2348012345678".</summary>
    public required string PhoneNumber { get; init; }

    public required string Password { get; init; }

    /// <summary>ISO 3166-1 alpha-2 code of the country the member holds nationality in.</summary>
    public required string Nationality { get; init; }

    /// <summary>ISO 3166-1 alpha-2 code of the country the member currently lives in.</summary>
    public required string CountryOfResidence { get; init; }

    /// <summary>BCP 47 language tag. Defaults to "en" when omitted.</summary>
    public string? PreferredLanguage { get; init; }

    /// <summary>IANA time zone identifier. Defaults to "UTC" when omitted.</summary>
    public string? PreferredTimeZone { get; init; }
}
