using System.Security.Claims;
using System.Text;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Diaspora.Infrastructure.Identity.Tokens;

/// <summary>
/// Mints short-lived tokens for trusted platform services.
/// <para>
/// Signed with the same key and validated by the same middleware as member tokens, but
/// stamped with <see cref="DiasporaClaimTypes.TokenType"/> = <c>service</c> and carrying no
/// session. Validation branches on that claim, so a service token can never satisfy a
/// member-scoped endpoint and a member's token can never reach a service-only one.
/// </para>
/// <para>
/// <b>Retired with its interface, 18 November 2026 (BSEC-013).</b> The class is kept rather than
/// deleted because the integration suite mints a Trust Engine token with it, deliberately: that is
/// what lets those tests exercise the real validation branch instead of a bypass that would prove
/// nothing about it. The one file allowed to construct this says so, and is the only suppression of
/// <c>DSP7001</c> in the repository.
/// </para>
/// </summary>
/// <seealso cref="IServiceTokenIssuer"/>
[Obsolete(
    "ServiceTokenIssuer is retired with IServiceTokenIssuer: nothing in this platform mints a " +
    "service token. It is kept so the integration suite can mint the token the Trust Engine " +
    "would present and exercise the real validation path. See ADR-020 and BSEC-013.",
    error: true,
    DiagnosticId = "DSP7001")]
public sealed class ServiceTokenIssuer : IServiceTokenIssuer
{
    /// <summary>
    /// Default lifetime. Deliberately short — a service can re-mint at will, and a leaked
    /// service token is more dangerous than a member's because it is not tied to a session
    /// anyone can revoke.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5);

    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _signingCredentials;

    public ServiceTokenIssuer(IOptions<JwtOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _clock = clock;

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenDescriptor Issue(ServiceTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;
        var expiresAt = now.Add(request.Lifetime ?? DefaultLifetime);
        var tokenId = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.ServiceName),
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(DiasporaClaimTypes.TokenType, DiasporaClaimTypes.ServiceTokenType)
        };

        foreach (var permission in request.Permissions)
        {
            claims.Add(new Claim(DiasporaClaimTypes.Permission, permission));
        }

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            claims.Add(new Claim(DiasporaClaimTypes.CorrelationId, request.CorrelationId));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials
        };

        var token = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }
            .CreateToken(descriptor);

        return new AccessTokenDescriptor
        {
            Token = token,
            TokenId = tokenId,
            IssuedAt = now,
            ExpiresAt = expiresAt
        };
    }
}
