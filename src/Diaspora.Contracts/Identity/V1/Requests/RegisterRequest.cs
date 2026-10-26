namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Everything Diaspora asks for at sign-up.
/// </summary>
/// <remarks>
/// Email used to be deliberately absent, because onboarding ran on a phone number and
/// sign-in was a one-time code. Sign-in is now an email address and a password, so the
/// address is required here: it is the credential the member will use from now on.
/// <para>
/// The phone number is still collected and still verified by SMS — that is what it is
/// for. What it is no longer used for is signing in, which is what a message costs
/// money for every single time.
/// </para>
/// </remarks>
public sealed record RegisterRequest
{
    /// <summary>The member's full name, as they wish to be addressed.</summary>
    public required string FullName { get; init; }

    /// <summary>Phone number in E.164 form, for example "+2348012345678".</summary>
    /// <remarks>Verified by SMS after registration. Not a sign-in credential.</remarks>
    public required string PhoneNumber { get; init; }

    /// <summary>
    /// The address the member will sign in with. Unique across the platform.
    /// </summary>
    public required string Email { get; init; }

    public required string Password { get; init; }

    /// <summary>ISO 3166-1 alpha-2 code of the country the member holds nationality in.</summary>
    public required string Nationality { get; init; }

    /// <summary>ISO 3166-1 alpha-2 code of the country the member currently lives in.</summary>
    public required string CountryOfResidence { get; init; }

    /// <summary>BCP 47 language tag. Defaults to "en" when omitted.</summary>
    public string? PreferredLanguage { get; init; }

    /// <summary>IANA time zone identifier. Defaults to "UTC" when omitted.</summary>
    public string? PreferredTimeZone { get; init; }

    /// <summary>
    /// The code of the member who invited them, from an invitation link or typed in. Optional.
    /// <para>
    /// Letters and digits only. A code that is well formed but unknown, inactive or the member's own
    /// registers the account all the same and credits nobody: the referral programme answers every
    /// refusal with the same silence.
    /// </para>
    /// </summary>
    public string? ReferralCode { get; init; }
}
