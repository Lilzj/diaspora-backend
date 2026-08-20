using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A deployment with the real-time runtime switched off.
/// <para>
/// <b>The documentation claims turning it off is safe at any time.</b> That claim is worth a test
/// rather than a sentence, because the path it takes is one no other test walks: the module skips
/// <c>AddSignalR</c>, skips the SignalR notifier and skips mapping the hub. The trap is the notifier
/// — leaving the SignalR implementation registered leaves <c>IHubContext</c> unresolvable behind it,
/// and every in-app delivery on that deployment throws. Nothing at startup catches that; the failure
/// arrives in the delivery sweep, in production, on a deployment whose only sin was setting a flag
/// to false.
/// </para>
/// </summary>
public sealed class CommunicationRuntimeDisabledTests : IdentityTestBase<RealtimeDisabledWebApplicationFactory>
{
    private const string Root = "/api/v1/notifications";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunicationRuntimeDisabledTests(RealtimeDisabledWebApplicationFactory factory)
        : base(factory) { }

    /// <summary>
    /// <b>The defect this file exists for.</b> The port must still resolve, and it must not be the
    /// implementation whose dependency is absent.
    /// </summary>
    [Fact]
    public void The_notifier_still_resolves_and_reports_itself_disabled()
    {
        using var scope = Factory.Services.CreateScope();

        var notifier = scope.ServiceProvider.GetService<IRealtimeNotifier>();

        notifier.Should().NotBeNull();
        notifier!.IsEnabled.Should().BeFalse();
    }

    /// <summary>
    /// The in-app channel resolves the broadcaster on every delivery, and the broadcaster resolves
    /// the notifier. A container that cannot build that chain is a platform that cannot deliver a
    /// notification at all.
    /// </summary>
    [Fact]
    public async Task Pushing_to_a_member_is_a_silent_no_op_rather_than_a_failure()
    {
        using var scope = Factory.Services.CreateScope();

        var notifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();

        var send = async () => await notifier.SendToUserAsync(
            SharedKernel.Identity.UserId.New(),
            RealtimeEvents.InboxCountersChanged,
            new InboxCounters(0, new Dictionary<string, int>(), DateTimeOffset.UtcNow));

        await send.Should().NotThrowAsync();
    }

    /// <summary>
    /// <b>The route is absent rather than inert.</b> A hub that accepts a socket and never sends
    /// anything is indistinguishable, from a client, from a hub that is broken — so the client
    /// reconnects forever against a deployment working exactly as configured. A 404 is something a
    /// client can act on.
    /// </summary>
    [Fact]
    public async Task The_hub_route_does_not_exist()
    {
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.PostAsync(
            new Uri("/hubs/realtime/negotiate?negotiateVersion=1", UriKind.Relative), null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>And the client is told before it tries.</summary>
    [Fact]
    public async Task The_configuration_endpoint_reports_the_runtime_as_disabled()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            new Uri($"{Root}/realtime/configuration", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        envelope.GetProperty("data").GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// The claim in the documentation, asserted: notifications still reach the inbox. Real-time is
    /// an enhancement to a channel, never the channel.
    /// </summary>
    [Fact]
    public async Task The_inbox_still_works()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(new Uri(Root, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
