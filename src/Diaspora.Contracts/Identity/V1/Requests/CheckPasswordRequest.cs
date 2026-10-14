namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Asks whether a password would be accepted, before any account exists.
/// <para>
/// Added 14 Oct 2026. Registration collects the password on its second screen and only
/// submits it after the countries and location screens, so a refused password was
/// reported three screens after it was typed. The name and email go with it because the
/// policy also refuses a password built from them.
/// </para>
/// </summary>
public sealed record CheckPasswordRequest
{
    public required string Password { get; init; }

    public string? Email { get; init; }

    public string? FullName { get; init; }

    public string? PhoneNumber { get; init; }
}
