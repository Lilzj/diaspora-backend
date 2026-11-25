using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Modules.Notifications.Realtime;
using Diaspora.Platform.Http;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Api;

/// <summary>
/// The HTTP half of the real-time runtime: what a client should do before it opens a socket, and
/// what it can ask without one.
/// <para>
/// <b>Neither route goes through MediatR, and that is the exception rather than a shortcut.</b> The
/// CQRS pipeline exists to give a use case validation, authorisation, transactions, telemetry and an
/// audit trail. Reading bound options has none of those to give: there is no command, nothing to
/// validate, nothing to write and nothing to audit. A handler here would be six files whose whole
/// content was returning a configuration object.
/// </para>
/// <para>
/// <b>So each route states its own authorization, because nothing downstream will.</b> Bypassing the
/// pipeline bypasses <c>AuthorizationBehavior</c> with it, and until 25 Nov 2026 both routes answered
/// an anonymous caller while the published contract told every client a token was required
/// (BSEC-009, BBL-007). Each now asks for <see cref="NotificationPolicies.Recipient"/>, the policy
/// <c>RealtimeHub</c> asks for: everyone who needs these answers is about to open that socket.
/// </para>
/// </summary>
internal static class RealtimeEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/realtime/configuration", (
                IOptionsSnapshot<RealtimeOptions> options,
                RealtimeConnectionCoordinator coordinator,
                HttpContext context) =>
            {
                var realtime = options.Value;

                return PlatformResults.Ok(context, Result.Success(new RealtimeConfigurationResponse
                {
                    Enabled = realtime.Enabled,
                    HubPath = NotificationsModuleMetadata.RealtimeHubPath,
                    PresenceEnabled = realtime.Presence.Enabled,
                    TypingEnabled = realtime.Typing.Enabled,
                    TypingTimeoutSeconds = realtime.Typing.TimeoutSeconds,
                    ReadReceiptsEnabled = realtime.Receipts.EnableReadReceipts,
                    DeliveryReceiptsEnabled = realtime.Receipts.EnableDeliveryReceipts,

                    // The same answer the handshake gives, from the same source — the registered
                    // group authorizer. A client reads it here before connecting and again on
                    // ConnectionAcknowledged; two sources would eventually disagree, and the client
                    // would gate its typing UI on whichever it happened to read first.
                    ConversationRelayEnabled = coordinator.ConversationRelayEnabled,

                    // Half the away threshold, so a member who is genuinely active never tips into
                    // "away" because one heartbeat was lost.
                    HeartbeatSeconds = Math.Max(1, realtime.Presence.AwayAfterSeconds / 2),
                    MaxConnectionsPerUser = realtime.MaxConnectionsPerUser
                }));
            })
            .RequireAuthorization(NotificationPolicies.Recipient)
            .WithName("GetRealtimeConfiguration")
            .WithSummary("Where the real-time hub is and what this deployment supports.");

        // The caller's own availability, and only ever their own — the shipped group authorizer
        // permits nobody to observe anybody else, so a route that accepted a list of identifiers
        // would be a route that always answered with an empty list.
        group.MapGet("/realtime/presence", async (
                IPresenceStore presence,
                ICurrentUser currentUser,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                // The policy below admits only a caller holding the recipient permission, which a
                // member's token carries, so there is a member here. The branch this replaces
                // answered an anonymous caller with an empty presence: authorization written inline,
                // because nothing upstream refused one. A caller with no member behind it now means
                // a token was issued wrongly, and that fails loudly rather than being answered.
                var userId = currentUser.UserId
                    ?? throw new InvalidOperationException(
                        "The recipient policy admitted a caller with no member behind it.");

                return PlatformResults.Ok(
                    context, Result.Success(await presence.GetAsync(userId, cancellationToken)));
            })
            .RequireAuthorization(NotificationPolicies.Recipient)
            .WithName("GetOwnPresence")
            .WithSummary("The caller's own availability, as other members would see it.");
    }
}
