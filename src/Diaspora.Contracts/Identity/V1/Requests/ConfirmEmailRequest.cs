namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>The token from an email verification link, posted by the app when it opened the link.</summary>
public sealed record ConfirmEmailRequest
{
    public string? Token { get; init; }
}
