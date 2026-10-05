using System.Text;
using Diaspora.Modules.Integrations.Application.Calendar;
using Diaspora.Modules.Integrations.Application.Capabilities;
using Diaspora.Modules.Integrations.Application.Connections;
using Diaspora.Modules.Integrations.Application.OAuth;
using Diaspora.Modules.Integrations.Application.Sync;
using Diaspora.Modules.Integrations.Application.Webhooks;
using Diaspora.Modules.Integrations.Contracts.V1.Requests;
using Diaspora.Modules.Integrations.Options;
using Diaspora.Platform.Http;
using Diaspora.Platform.Modules.Contributors;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Integrations.Api;

/// <summary>
/// The module's HTTP surface.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs, and returns through
/// <c>PlatformResults</c> so the envelope and problem documents are byte-for-byte identical to every
/// other module's. Nothing here is known to the host.
/// </para>
/// <para>
/// <b>Route shapes come from the frontend, not from this module.</b> The client's
/// <c>integrations-gap-api.ts</c> declares every one of these against <c>/integrations-gap/v0</c>
/// with the note that "when a backend phase ships integrations, the hook swaps which arm it calls
/// and the request shapes already match". They match. Only the prefix changes, to
/// <c>/api/v1/integrations</c>.
/// </para>
/// <para>
/// <b>Two routes are additive, and both are controls rather than features.</b>
/// <c>POST /oauth/{provider}/authorize</c> exists because a state the server did not mint is a state
/// the server cannot validate; <c>GET /capabilities</c> exists because "NotConfigured" has to be
/// something a client can read rather than infer from a refusal.
/// </para>
/// </summary>
public sealed class IntegrationsEndpointContributor : IEndpointContributor
{
    public string Module => IntegrationsModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(IntegrationsModuleMetadata.RoutePrefix)
            .WithTags("Integrations");

        MapConnections(group);
        MapOAuth(group);
        MapCalendar(group);
        MapSettings(group);
        MapCapabilities(group);
        MapWebhooks(group);
    }

    // ── Connections — GAP-001 ───────────────────────────────────────────────────────────────

    private static void MapConnections(RouteGroupBuilder group)
    {
        group.MapGet("/connections", async (
                string? domain,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListConnectionsQuery { Domain = domain }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.ConnectionsRead)
            .WithName("ListIntegrationConnections")
            .WithSummary("Every integration this member has connected. Self only; no token field.");

        group.MapDelete("/connections/{provider}", async (
                string provider,
                bool? revokeAtProvider,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new DisconnectIntegrationCommand
                    {
                        Provider = provider,

                        // Defaults to true. A member disconnecting means it, and the alternative
                        // default leaves the platform holding a working refresh token unless a
                        // client remembered to ask.
                        RevokeAtProvider = revokeAtProvider ?? true
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.ConnectionsWrite)
            .WithName("DisconnectIntegration")
            .WithSummary(
                "Disconnects a provider. Idempotent: a second call answers 200 with the same body, "
                + "never 404.");
    }

    // ── OAuth — GAP-002 ─────────────────────────────────────────────────────────────────────

    private static void MapOAuth(RouteGroupBuilder group)
    {
        group.MapPost("/oauth/{provider}/authorize", async (
                string provider,
                StartAuthorizationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new StartAuthorizationCommand
                    {
                        Provider = provider,
                        RedirectUri = request?.RedirectUri,
                        CodeChallenge = request?.CodeChallenge,
                        Scopes = request?.Scopes ?? [],
                        Intent = request?.Intent
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.ConnectionsWrite)
            .WithName("StartIntegrationAuthorization")
            .WithSummary(
                "Issues the state and the approved redirect for an authorization. The server mints "
                + "both, because a state it did not mint is a state it cannot validate.");

        group.MapPost("/oauth/{provider}/exchange", async (
                string provider,
                ExchangeAuthorizationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ExchangeAuthorizationCommand
                    {
                        Provider = provider,
                        Code = request?.Code,
                        CodeVerifier = request?.CodeVerifier,
                        State = request?.State,
                        RedirectUri = request?.RedirectUri,
                        Intent = request?.Intent
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.ConnectionsWrite)
            .WithName("ExchangeIntegrationAuthorizationCode")
            .WithSummary(
                "Completes an authorization. A replayed code answers 409; an external account "
                + "already attached to another member answers 409.");

        group.MapPost("/oauth/{provider}/revoke", async (
                string provider,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RevokeAuthorizationCommand { Provider = provider }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.ConnectionsWrite)
            .WithName("RevokeIntegrationAuthorization")
            .WithSummary("Revokes the grant at the provider. Idempotent; revoking twice is a no-op.");
    }

    // ── Calendar — GAP-003, GAP-004, GAP-005 ────────────────────────────────────────────────

    private static void MapCalendar(RouteGroupBuilder group)
    {
        group.MapGet("/calendar/{provider}/calendars", async (
                string provider,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListRemoteCalendarsQuery { Provider = provider }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.CalendarRead)
            .WithName("ListRemoteCalendars")
            .WithSummary(
                "The calendars a connected account can see. Never logged and never cached — a "
                + "calendar title is personal.");

        // Ahead of `/events/{externalEventId}`, so "lookup" is not resolved as an identifier.
        // ASP.NET Core matches literals before parameters, so this is belt and braces — and belt and
        // braces is right for a collision that would silently 404 a real screen.
        group.MapGet("/calendar/{provider}/events/lookup", async (
                string provider,
                string sourceId,
                string calendarId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new LookupCalendarEventQuery
                    {
                        Provider = provider,
                        SourceId = sourceId,
                        CalendarId = calendarId
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.CalendarRead)
            .WithName("LookupRemoteCalendarEvent")
            .WithSummary("Whether the provider already holds an entry Diaspora wrote for this source.");

        group.MapPost("/calendar/{provider}/events", async (
                string provider,
                CalendarEventRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    ToCommand(provider, request, IdempotencyHeader.From(context), externalEventId: null),
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.CalendarWrite)
            .WithName("CreateRemoteCalendarEvent")
            .WithSummary(
                "Writes an entry. An Idempotency-Key is mandatory: without one a retry after a "
                + "timeout is a duplicate entry in somebody's calendar.");

        group.MapPut("/calendar/{provider}/events/{externalEventId}", async (
                string provider,
                string externalEventId,
                CalendarEventRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    ToCommand(provider, request, IdempotencyHeader.From(context), externalEventId),
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.CalendarWrite)
            .WithName("UpdateRemoteCalendarEvent")
            .WithSummary("Replaces an entry this platform wrote.");

        group.MapDelete("/calendar/{provider}/events/{externalEventId}", async (
                string provider,
                string externalEventId,
                string? calendarId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new DeleteCalendarEventCommand
                    {
                        Provider = provider,
                        ExternalEventId = externalEventId,
                        CalendarId = calendarId
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.CalendarWrite)
            .WithName("DeleteRemoteCalendarEvent")
            .WithSummary("Removes an entry. Idempotent; deleting an already-deleted entry is 200.");

        group.MapPost("/calendar/{provider}/sync", async (
                string provider,
                StartCalendarSyncRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new StartCalendarSyncCommand
                    {
                        Provider = provider,
                        CalendarId = request?.CalendarId,
                        Direction = request?.Direction ?? "export"
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.CalendarSync)
            .WithName("StartCalendarSync")
            .WithSummary(
                "Starts a synchronisation. A run already going for the same calendar is returned "
                + "rather than duplicated.");

        group.MapGet("/calendar/{provider}/sync/status", async (
                string provider,
                string? calendarId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetCalendarSyncStatusQuery { Provider = provider, CalendarId = calendarId },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.CalendarSync)
            .WithName("GetCalendarSyncStatus")
            .WithSummary("The state of the most recent synchronisation. `never` when there has been none.");
    }

    // ── Settings — GAP-006 ──────────────────────────────────────────────────────────────────

    private static void MapSettings(RouteGroupBuilder group)
    {
        group.MapGet("/calendar/settings", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetCalendarSettingsQuery(), cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.ConnectionsRead)
            .WithName("GetCalendarSettings")
            .WithSummary("This member's calendar preferences, defaulted if they have set none.");

        group.MapPut("/calendar/settings", async (
                UpdateCalendarSettingsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new UpdateCalendarSettingsCommand
                    {
                        Enabled = request?.Enabled,
                        DefaultCalendarId = request?.DefaultCalendarId,
                        DefaultProvider = request?.DefaultProvider,
                        AutoExportRsvps = request?.AutoExportRsvps,
                        TwoWaySyncEnabled = request?.TwoWaySyncEnabled,
                        SyncFrequency = request?.SyncFrequency,
                        DefaultReminderMinutes = request?.DefaultReminderMinutes,
                        TimeZoneBehaviour = request?.TimeZoneBehaviour,
                        ConflictBehaviour = request?.ConflictBehaviour
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.SettingsWrite)
            .WithName("UpdateCalendarSettings")
            .WithSummary("A partial update. A missing field means 'leave it alone', never 'clear it'.");
    }

    // ── Capabilities ────────────────────────────────────────────────────────────────────────

    private static void MapCapabilities(RouteGroupBuilder group) =>
        group.MapGet("/capabilities", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetIntegrationCapabilitiesQuery(), cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(IntegrationsPolicies.ConnectionsRead)
            .WithName("GetIntegrationCapabilities")
            .WithSummary(
                "Which integration capabilities this deployment actually has, and a reason beside "
                + "every one it does not.");

    // ── Webhooks ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Where providers tell the platform what happened.
    /// <para>
    /// <b>The only anonymous route this module has, and it is anonymous because the signature is the
    /// credential.</b> A provider holds no bearer token; it holds a shared secret and signs the body
    /// with it. The registered verifier checks that before a field of the payload is read.
    /// </para>
    /// <para>
    /// <b>The raw bytes are read once and passed through unaltered.</b> Model binding would
    /// deserialise and re-serialise, and a signature verified over a re-serialised object is a
    /// signature verified over something the provider never sent.
    /// </para>
    /// </summary>
    private static void MapWebhooks(RouteGroupBuilder group) =>
        group.MapPost("/webhooks/{provider}", async (
                string provider,
                HttpContext context,
                ISender sender,
                IOptionsMonitor<IntegrationsOptions> options,
                CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync(
                    context.Request, options.CurrentValue.MaxWebhookBodyBytes, cancellationToken);

                if (body is null)
                {
                    return Results.Problem(PlatformProblem.Create(
                        context,
                        StatusCodes.Status413PayloadTooLarge,
                        IntegrationsErrorCodes.WebhookPayloadTooLarge,
                        "Payload too large",
                        "That callback body is larger than this platform will read."));
                }

                return PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ProcessIntegrationWebhookCommand
                        {
                            Provider = provider,
                            RawBody = body,
                            Headers = Headers(context.Request)
                        },
                        cancellationToken));
            })
            .AllowAnonymous()
            .WithName("ProcessIntegrationWebhook")
            .WithSummary(
                "A provider's callback. Signature-verified, replay-protected and idempotent on the "
                + "provider's own event identifier.");

    /// <summary>
    /// Exactly the bytes that arrived, as text — or null when there are too many of them.
    /// <para>
    /// Read into a bounded buffer rather than with <c>ReadToEndAsync</c>: an unauthenticated caller
    /// choosing how much memory the server allocates is the whole of that vulnerability, and a
    /// content-length header is a claim rather than a fact.
    /// </para>
    /// </summary>
    private static async Task<string?> ReadBodyAsync(
        HttpRequest request, int maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var accumulated = new MemoryStream();

        while (true)
        {
            var read = await request.Body.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            if (accumulated.Length + read > maxBytes)
            {
                return null;
            }

            accumulated.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(accumulated.ToArray());
    }

    /// <summary>
    /// The request headers, flattened.
    /// <para>
    /// Which header carries the signature is the provider's decision and therefore the verifier's
    /// knowledge, so all of them travel and the verifier picks. A repeated header is joined rather
    /// than dropped — a signature split across two values would otherwise verify against half of
    /// itself.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> Headers(HttpRequest request)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(',', header.Value.ToArray());
        }

        return headers;
    }

    private static WriteCalendarEventCommand ToCommand(
        string provider,
        CalendarEventRequest? request,
        string? idempotencyKey,
        string? externalEventId) =>
        new()
        {
            Provider = provider,
            CalendarId = request?.CalendarId,
            SourceId = request?.SourceId,
            SourceKind = request?.SourceKind,
            Title = request?.Title,
            StartsAt = request?.StartsAt,
            EndsAt = request?.EndsAt,
            TimeZone = request?.TimeZone,
            IsAllDay = request?.IsAllDay ?? false,
            Location = request?.Location,
            Notes = request?.Notes,
            Url = request?.Url,
            ReminderMinutes = request?.ReminderMinutes ?? [],
            RecurrenceRule = request?.RecurrenceRule,
            IdempotencyKey = idempotencyKey,
            ExternalEventId = externalEventId
        };
}

/// <summary>Reads the <c>Idempotency-Key</c> header.</summary>
internal static class IdempotencyHeader
{
    private const string Name = "Idempotency-Key";

    public static string? From(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Headers.TryGetValue(Name, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
