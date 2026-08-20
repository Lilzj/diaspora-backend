namespace Diaspora.Application.Identity.Authentication;

/// <summary>
/// Identifies an authentication mechanism.
/// <para>
/// A string key rather than an enum, and deliberately so. The whole point of the provider
/// model is that Google, Apple, a magic link or a government identity provider can be added
/// without editing existing code — and an enum would force every one of those to edit a
/// shared definition, which is exactly the closed pattern B2.6 is removing everywhere else.
/// A new provider declares its own <see cref="AuthenticationMethod"/> and registers itself;
/// nothing here changes.
/// </para>
/// </summary>
public readonly record struct AuthenticationMethod(string Key)
{
    /// <summary>Phone number and password.</summary>
    public static readonly AuthenticationMethod Password = new("password");

    /// <summary>Phone number and a one-time code — the frontend's primary sign-in.</summary>
    public static readonly AuthenticationMethod PhoneOtp = new("phone-otp");

    public override string ToString() => Key;
}
