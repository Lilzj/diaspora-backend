using Asp.Versioning;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Api.Identity.Trust;
using Diaspora.Api.Health;
using Diaspora.Api.Hosting;
using Diaspora.Api.Identity.Authentication;
using Diaspora.Api.Middleware;
using Diaspora.Api.RateLimiting;
using Diaspora.Email;
using Diaspora.Maps.Google;
using Diaspora.Operations;
using Diaspora.Application;
using Diaspora.Infrastructure;
using Diaspora.Infrastructure.Messaging;
using Diaspora.Observability;
using Diaspora.Persistence;
using Diaspora.Platform.Http;
using Diaspora.Push.Firebase;
using Diaspora.Payments.Flutterwave;
using Diaspora.Payments.PayPal;
using Diaspora.Payments.Stripe;
using Diaspora.Sms;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using Diaspora.Platform.Validation;
using Diaspora.SharedKernel.Primitives;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;

// `--health-check` runs the container HEALTHCHECK in-process and exits. The aspnet runtime
// image ships neither curl nor wget, so the app has to be its own probe.
if (ContainerHealthProbe.IsRequested(args))
{
    return await ContainerHealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

// ─── Module discovery ────────────────────────────────────────────────
// Done before anything reads it, because the observability pipeline needs the telemetry
// source names at registration time. This is the only place assemblies are scanned; the
// same list is handed to the kernel so discovery happens exactly once.
var platformAssemblies = ModuleAssemblyScanner.FindPlatformAssemblies();

// ─── Serilog ─────────────────────────────────────────────────────────
Diaspora.Observability.DependencyInjection.ConfigureSerilog(
    builder.Configuration,
    builder.Environment.EnvironmentName);
builder.Host.UseSerilog();

// ─── Services ────────────────────────────────────────────────────────

// Application layer (MediatR, FluentValidation, Mapster, pipeline behaviors)
builder.Services.AddApplication();

// Infrastructure layer (platform service implementations)
builder.Services.AddInfrastructure(builder.Configuration);

// Persistence layer (EF Core, DbContext, unit of work)
builder.Services.AddPersistence(builder.Configuration);

// Observability. Modules contribute their own activity sources and meters, so a new
// module's traces appear without touching this call.
builder.Services.AddObservability(
    builder.Configuration,
    ModuleAssemblyScanner.CreateAll<IObservabilityContributor>(platformAssemblies));

// The platform kernel: discovers modules, collects contributors, composes the permission,
// feature and policy catalogues, and calls each module's ConfigureServices.
//
// This line is why adding Trust, Marketplace or Messaging does not touch Program.cs. A new
// module is a ProjectReference; everything else it needs to say, it says through the
// contributor interfaces.
builder.Services.AddPlatformKernel(builder.Configuration, platformAssemblies);

// Reliable messaging (B1.1). Registered after the kernel because it replaces the integration
// event publisher the identity module registers during its own composition — the substitution
// that turns every module's best-effort publish into a durable, transactional enqueue without
// any module knowing. Nothing above this line changed when it was added.
builder.Services.AddReliableMessaging(builder.Configuration);

// The push transport. After the kernel for the same reason as the line above: the Notifications
// module registers a placeholder sender during its own composition, and this replaces it on
// last-registration-wins. It is a no-op unless Notifications:Push:Provider names it, so the call
// is unconditional and the configuration remains the only thing that decides.
builder.Services.AddFirebasePushAdapter(builder.Configuration);

// The SMS transport, on the same last-registration-wins seam. The Identity infrastructure
// registers a logging placeholder behind IOtpDeliveryService, and this replaces it once a
// provider is configured.
//
// Which provider carries a given number is Sms:Routes, matched longest-prefix against the
// destination: a Nigerian number and a German one do not cost the same on the same wire, and
// the cheap wire does not reach Germany at all.
builder.Services.AddSmsAdapter(builder.Configuration);

// The Resend email adapter, behind Notifications' IEmailSender. The module registers
// UnconfiguredEmailSender as a placeholder — IsConfigured => false, and a failure on every
// send — and this replaces it once Email:Resend:ApiKey is set.
//
// Until it was added, every email the platform composed was dropped at the wire: the 60
// notification template seeds in Notifications' Catalog/Templates, none of them deliverable.
//
// It also decorates IOtpDeliveryService, and that is why it has to come *after*
// AddSmsAdapter rather than anywhere convenient. The decorator wraps whichever transport is
// registered at the moment it runs, so that a recovery code goes to the mailbox the member
// just identified themselves with while phone verification stays on SMS — a code proving
// you hold a number cannot arrive anywhere else. Called first, it would wrap the logging
// placeholder, and AddSmsAdapter would then replace the whole decorated chain: recovery would
// go quietly back to SMS with nothing failing to say so.
builder.Services.AddEmailAdapter(builder.Configuration);

// The Stripe payout adapter, behind the Financial Platform's ISettlementProvider and
// IPayoutProviderVerificationSource seams. A no-op unless Payments:Stripe:SecretKey is configured,
// and deliberately so: the payout router treats every registered ISettlementProvider as usable, so
// an uncredentialed adapter would advertise a rail that answers 401 rather than refusing cleanly.
//
// Registering it enables no corridor by itself. Which countries, rails and currencies this account
// may serve is Financial:PayoutRouting, and every row there has to be confirmed against Stripe's
// own documentation - an unconfirmed row stays Unknown and refuses.
builder.Services.AddStripePayments(builder.Configuration);

// The other two payout rails, on the same seams and with the same rule: each is a no-op until its
// own credentials are configured, and registering one enables no corridor by itself.
builder.Services.AddPayPalPayments(builder.Configuration);
builder.Services.AddFlutterwavePayments(builder.Configuration);

// Google Maps, behind GeographicIntelligence's IRoutingProvider and Location's
// IPlaceSearchProvider. A no-op unless Maps:Google:ApiKey is configured, so both routes keep
// answering NotConfigured rather than advertising a provider that answers 403. Free tier only:
// every billable call takes a unit from a monthly allowance in platform.provider_usage first.
builder.Services.AddGoogleMaps(builder.Configuration);

// How a verification decision reaches a member's access state.
//
// Trust publishes every decision through ITrustDecisionPublisher and knows nothing else about
// Identity — ADR-002's seam, kept so extracting B3 stays a configuration change. What the ADR did
// not say was what an unconfigured deployment should do, and the answer was "nothing": the section
// was absent everywhere, publishing was skipped with a warning, and no member could leave
// LimitedAccess by any route the product offered.
//
// Loopback is now the default and the bridge lives here, in the one place entitled to know both
// modules. Set Trust:Decisions:Mode to "Http" and this registration stands down.
if (builder.Configuration
        .GetSection(TrustDecisionOptions.SectionName)
        .Get<TrustDecisionOptions>() is not { Mode: TrustDecisionDelivery.Http })
{
    builder.Services.AddScoped<ITrustDecisionPublisher, LoopbackTrustDecisionPublisher>();
}

// The Production Platform (B17): caching, resilience, rate limiting, feature management, runtime
// configuration, scheduling, health aggregation, dashboards, security and privacy tooling.
//
// After the kernel, because the operational policy catalogue is composed there. After the modules,
// because several of its registrations are TryAdd-shaped defaults a module or a deployment may
// already have replaced.
builder.Services.AddProductionPlatform(builder.Configuration);

// Deliberately *not* bridged: B13, B14 and B15 keep their own cache ports.
//
// B17 shipped a bridge that served IRecommendationCache and IFinancialCache from the platform cache,
// and the integration suite caught it changing behaviour. Two reasons, and the second is the one that
// matters. The module ports take a per-call lifetime that ICacheService has no way to express, so the
// bridge silently substituted a policy's expiry for the caller's. And the platform cache serializes,
// while the module caches hold references — so a cached strategy row came back as a detached,
// JSON-round-tripped copy rather than the entity the module put in. B13's own port documents that
// hazard in prose: "an EF entity in a shared cache would be handed to a second request's change
// tracker, which is a class of bug worth far more than the round trip it saves."
//
// Adopting the platform cache is each module's own change to make, in its own phase, with its own
// tests. Recorded as DEBT-066 rather than forced from the composition root.

// Authentication, plus the policies the kernel composed. Registered after the modules so
// the identity services token validation depends on are already in the container.
builder.Services.AddIdentityAuthentication();

// How an incoming path is normalised onto the canonical route space. The client's base URL is
// `/v1` and every module serves `/api/v1`; see RoutePrefixCompatibilityMiddleware for why that is
// closed here, once, rather than by moving any module's prefix.
builder.Services.AddOptions<ApiRoutingOptions>()
    .Bind(builder.Configuration.GetSection(ApiRoutingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<ApiRoutingOptions>, ApiRoutingOptionsValidator>();

builder.Services.AddOptions<RateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>();
builder.Services.AddDiasporaRateLimiting();

builder.Services.AddPlatformHealthChecks();

// API Configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new StronglyTypedIdJsonConverterFactory());
    });

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
// The schema transformer publishes the values behind string-typed enum properties.
// Without it the document describes `objective` as an unconstrained string and the
// four legal values live only in C# — see OpenApiEnumValuesAttribute.
builder.Services.AddOpenApi(options =>
    options.AddSchemaTransformer<Diaspora.Api.OpenApi.EnumValuesSchemaTransformer>());

// Forwarded headers. Without this, every security event, session record and rate-limit
// partition behind a load balancer sees the proxy's address instead of the client's —
// which makes the forensic trail useless and collapses the entire user base into a single
// rate-limit bucket. KnownProxies is configured per environment; an empty allowlist means
// the headers are ignored, which is the safe failure.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:Limit", 1);

    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        if (System.Net.IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });

    options.AddPolicy("Production", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Response compression
builder.Services.AddResponseCompression();

// Platform startup validation (B1.4). Last, and that is load-bearing: it takes a snapshot of the
// service collection as it finally stands, so anything registered after this line is invisible to
// every validator — a seeder added below would be reported as unregistered, a contributor as
// missing. It refuses to start the host on a duplicate permission key, an unregistered policy, a
// staff capability left open to members or a seeder nothing registered.
builder.Services.AddPlatformStartupValidation(builder.Configuration, platformAssemblies);

var app = builder.Build();

// ─── Middleware Pipeline ──────────────────────────────────────────────

// First, so everything downstream — including the rate limiter's partition key — sees the
// real client address rather than the proxy's.
app.UseForwardedHeaders();

// Correlation ID
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseMiddleware<SecurityHeadersMiddleware>();

// Serilog request logging
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
        diagnosticContext.Set("CorrelationId", httpContext.CorrelationId());
    };
});

// Global exception handler
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// The client's base URL is `https://api.diaspora.app/v1`; every module on this platform serves
// `/api/v1/{module}`. This rewrites the first onto the second, for every module identically, so no
// module becomes the only one on a different prefix.
app.UseMiddleware<RoutePrefixCompatibilityMiddleware>();

// Explicit, and the reason is the line above. WebApplication inserts UseRouting ahead of all user
// middleware unless the host calls it, which would mean the endpoint had already been selected from
// the un-rewritten path and the rewrite changed nothing. Calling it here also puts routing where the
// framework documents it — after the exception handler, before CORS, authentication, authorization
// and the rate limiter, all four of which read the endpoint metadata it produces.
app.UseRouting();

// CORS
app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

// Response compression
app.UseResponseCompression();

// HTTPS enforcement (production)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Before authentication: an unauthenticated flood must be refused without first paying for
// a signature validation and two database reads.
app.UseRateLimiter();

// Authentication must precede authorization, and both must precede the endpoints they
// protect. Placed after CORS so a pre-flight request is answered without a token.
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(PlatformHealthChecks.LiveTag),
    ResponseWriter = PlatformHealthChecks.WriteResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(PlatformHealthChecks.ReadyTag),
    ResponseWriter = PlatformHealthChecks.WriteResponse
});

// API Documentation
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Diaspora Platform API")
            .WithTheme(ScalarTheme.BluePlanet);
    });
}

// Controllers (Identity and anything else using MVC)
app.MapControllers();

// Minimal-API endpoints contributed by modules. The second half of "Program.cs never
// changes": a module in its own assembly maps its own routes here without the host
// referencing its controllers or its request types.
app.MapModuleEndpoints();

// Platform info endpoint. The environment name is deliberately not reported: an
// unauthenticated caller has no need to know which deployment they reached.
app.MapGet("/", (PlatformManifest manifest) => new
{
    platform = "Diaspora",
    version = "1.0.0",
    modules = manifest.GetRegisteredModules(),
    health = "/health/live"
});

// Stripe Checkout return pages.
//
// Stripe requires http(s) return URLs and rejects a custom scheme, so a Checkout cannot send a
// member straight back to diaspora://. These two pages are the bridge: they land in the system
// browser and immediately hand off to the app, with a visible link for the case where the automatic
// hand-off does not fire.
//
// Neither page decides anything. Whether the money is held is settled by the verified
// checkout.session.completed callback, and a member who never lands here — closed the tab, lost
// signal — is not treated differently from one who did. Reading an outcome off a redirect the member
// controls would be an unauthenticated write to the payment state machine.
static IResult CheckoutReturn(string heading, string message, string deepLink)
{
    // $$ so the CSS braces stay literal and only {{...}} interpolates. With a single $ the raw
    // string reads every { as an interpolation hole and the stylesheet stops compiling.
    var html = $$"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{heading}}</title>
        <style>
          body { font-family: system-ui, -apple-system, "Segoe UI", sans-serif; margin: 0;
                 display: grid; place-items: center; min-height: 100vh; background: #f7f8fa;
                 color: #14161a; padding: 24px; }
          main { max-width: 22rem; text-align: center; }
          h1 { font-size: 1.25rem; margin: 0 0 .5rem; }
          p { margin: 0 0 1.5rem; line-height: 1.5; color: #55606e; }
          a { display: inline-block; background: #f59e0b; color: #fff; text-decoration: none;
              font-weight: 700; padding: .75rem 1.5rem; border-radius: 999px; }
        </style>
        </head>
        <body>
        <main>
          <h1>{{heading}}</h1>
          <p>{{message}}</p>
          <a href="{{deepLink}}">Return to Diaspora</a>
        </main>
        <script>location.replace({{System.Text.Json.JsonSerializer.Serialize(deepLink)}});</script>
        </body>
        </html>
        """;

    return Results.Content(html, "text/html; charset=utf-8");
}

app.MapGet("/payments/checkout/complete", () => CheckoutReturn(
    "Payment received",
    "Your wallet will update as soon as Diaspora has confirmed it with your bank. You can close this page.",
    "diaspora://wallet"));

app.MapGet("/payments/checkout/cancelled", () => CheckoutReturn(
    "Payment cancelled",
    "Nothing was charged. You can try again from your wallet whenever you are ready.",
    "diaspora://wallet"));

// ─── Start ───────────────────────────────────────────────────────────

try
{
    Log.Information("Starting Diaspora Platform API...");
    await app.RunAsync();
    return 0;
}
// The host-abort signal must not be swallowed: tooling and test hosts stop the app by
// throwing it out of Run(), and treating that as a crash both logs a false fatal and makes
// the process exit successfully when it should not.
catch (Exception ex) when (ex.GetType().Name is not ("StopTheHostException" or "HostAbortedException"))
{
    Log.Fatal(ex, "Diaspora Platform API terminated unexpectedly.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

namespace Diaspora.Api
{
    public partial class Program { }
}
