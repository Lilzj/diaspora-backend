using System.Security.Claims;
using System.Text;
using Diaspora.Platform.Http;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Infrastructure.Identity.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Diaspora.Api.Identity.Authentication;

/// <summary>
/// Configures JWT bearer validation from the bound <see cref="JwtOptions"/>.
/// <para>
/// Deliberately resolved through the options system rather than read from
/// <c>IConfiguration</c> at registration time. Reading eagerly binds whatever configuration
/// sources happen to exist at that moment, which silently misses any added afterwards — a
/// test host layering in a signing key, or a secret provider registered later. The failure
/// mode is the worst kind: the app starts, options validation passes because it reads the
/// merged configuration, and every request then fails inside the authentication handler
/// with an empty key.
/// </para>
/// </summary>
public sealed class JwtBearerOptionsConfigurator : IConfigureNamedOptions<JwtBearerOptions>
{
    /// <summary>
    /// The development fallback signing key.
    /// <para>
    /// <b>Kept only as an alias.</b> The value — and the substitution — now live in
    /// <see cref="JwtOptions.DevelopmentSigningKey"/> and
    /// <see cref="JwtDevelopmentSigningKeyResolver"/>, where the token issuer can see them
    /// too. It was declared here while this class performed the substitution itself, which
    /// resolved the key for token *validation* and left token *issuance* reading an empty
    /// string.
    /// </para>
    /// </summary>
    public const string DevelopmentSigningKey = JwtOptions.DevelopmentSigningKey;

    /// <summary>
    /// The one URL prefix where a bearer token may arrive in the query string.
    /// <para>
    /// Owned by the host rather than by whichever module maps a hub, because the decision being made
    /// is Identity's — "where may a token appear" — and it must have exactly one answer. A module
    /// that could widen it would be a module that could put a bearer token into an access log.
    /// </para>
    /// </summary>
    public const string HubPathPrefix = "/hubs";

    private readonly IOptionsMonitor<JwtOptions> _jwtOptions;
    private readonly IHostEnvironment _environment;

    public JwtBearerOptionsConfigurator(
        IOptionsMonitor<JwtOptions> jwtOptions,
        IHostEnvironment environment)
    {
        _jwtOptions = jwtOptions;
        _environment = environment;
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name is not null && name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var jwt = _jwtOptions.CurrentValue;

        // No fallback here. `JwtDevelopmentSigningKeyResolver` resolves the Development key
        // into JwtOptions before anything reads it, so this validating half and the issuing
        // half necessarily agree. Substituting a key here as well is precisely what let the
        // two halves disagree: a token could be validated in Development and never issued.
        var signingKey = jwt.SigningKey;

        options.MapInboundClaims = false;
        options.SaveToken = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,

            ValidateAudience = true,
            ValidAudience = jwt.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),

            // Only HMAC-SHA256 is accepted. Leaving the algorithm open is how
            // signature-stripping and algorithm-confusion attacks get a foothold.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            // WebSocket upgrades cannot carry an Authorization header. Neither a browser's
            // WebSocket nor React Native's lets a caller set one, so every SignalR client
            // authenticates by putting the token in the query string — which is what
            // `accessTokenFactory` does. Without this, a hub is unreachable to every client
            // that is not long-polling, and the symptom is a 401 on negotiate that looks
            // like an expired token.
            //
            // Scoped to the /hubs prefix rather than accepted everywhere. A token in a query
            // string is a token in an access log, a proxy cache and a browser history, and
            // the whole reason it is tolerated here is that the transport leaves no
            // alternative. Allowing it on the REST surface would hand every caller a way to
            // put a bearer token somewhere it will be written down. The prefix is the
            // platform's, not one module's, so a future module mapping a hub beneath it is
            // authenticated with no change to this file.
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token)
                    && context.Request.Path.StartsWithSegments(
                        HubPathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var token = context.Request.Query["access_token"].ToString();

                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        context.Token = token;
                    }
                }

                return Task.CompletedTask;
            },

            // Signature validity is necessary but not sufficient — see
            // AccessTokenValidationHandler for why every request also checks live session
            // and account state.
            OnTokenValidated = AccessTokenValidationHandler.ValidateAsync,

            // Suppress the default WWW-Authenticate body so authentication failures come
            // back as ProblemDetails like every other error in the platform.
            OnChallenge = context =>
            {
                context.HandleResponse();

                return PlatformProblem.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    PlatformErrorCodes.Unauthorized,
                    "Unauthorized",
                    "Authentication is required to access this resource.",
                    cancellationToken: context.HttpContext.RequestAborted);
            },

            // B2 left this unhandled, so every authorisation failure returned a bare 403
            // with an empty body — the one error in the platform a client could not parse,
            // on the path where knowing *why* matters most.
            OnForbidden = context => PlatformProblem.WriteAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                PlatformErrorCodes.Forbidden,
                "Forbidden",
                "Your account does not have access to this resource.",
                cancellationToken: context.HttpContext.RequestAborted)
        };
    }
}
