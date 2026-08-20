using Diaspora.Application.Identity.Models;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Mints signed JWT access tokens. Token format and signing material belong to the
/// infrastructure adapter; the Application layer only says who the token is for.
/// </summary>
public interface IAccessTokenIssuer
{
    AccessTokenDescriptor Issue(AccessTokenRequest request);
}
