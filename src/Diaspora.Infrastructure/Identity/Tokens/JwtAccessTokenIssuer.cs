using System.Globalization;
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
/// Mints signed JWT access tokens.
/// <para>
/// The token carries a snapshot of permissions so ordinary requests authorise without a
/// database round trip. That snapshot is necessarily stale between issuance and expiry,
/// which is why the lifetime is short and why <see cref="DiasporaClaimTypes.SecurityStamp"/>
/// rides along: bumping the stamp invalidates every token already in the wild without
/// waiting for any of them to expire.
/// </para>
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _signingCredentials;

    public JwtAccessTokenIssuer(IOptions<JwtOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _clock = clock;

        var keyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenDescriptor Issue(AccessTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var tokenId = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.Value.ToString()),
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(DiasporaClaimTypes.SessionId, request.SessionId.Value.ToString()),
            new(DiasporaClaimTypes.AccessState, request.AccessState.Name),
            new(DiasporaClaimTypes.SecurityStamp, request.SecurityStamp.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Role, request.Role.Name),
            new(DiasporaClaimTypes.Role, request.Role.Name)
        };

        if (request.DeviceId is not null)
        {
            claims.Add(new Claim(DiasporaClaimTypes.DeviceId, request.DeviceId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            claims.Add(new Claim(DiasporaClaimTypes.CorrelationId, request.CorrelationId));
        }

        foreach (var permission in request.Permissions)
        {
            claims.Add(new Claim(DiasporaClaimTypes.Permission, permission));
        }

        if (_options.IncludeFeatureClaims && request.Features is not null)
        {
            foreach (var feature in request.Features)
            {
                claims.Add(new Claim(DiasporaClaimTypes.Feature, feature));
            }
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
