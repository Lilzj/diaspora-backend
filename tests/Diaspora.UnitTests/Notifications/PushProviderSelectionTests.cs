using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Channels;
using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using Diaspora.Modules.Notifications.Options;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// The two implementations that ship without a vendor, and the one that ships without a transport.
/// <para>
/// <b>All three exist so that a component which does nothing still does it honestly.</b> The
/// difference between "reports unconfigured" and "reports success and sends nothing" decides whether
/// a delivery row reaches <c>Unsupported</c> or <c>Delivered</c>, and getting it the wrong way round
/// in a deployment means the platform is confidently wrong about whether members were reached.
/// </para>
/// </summary>
public class PushProviderSelectionTests
{
    private static PushMessage Message(int endpoints = 1) => new()
    {
        Endpoints = Enumerable.Range(0, endpoints)
            .Select(_ => new ChannelEndpoint { Address = "token-value", Platform = "IOS" })
            .ToList(),
        Title = "Title",
        Body = "Body",
        IsTimeSensitive = false,
        NotificationId = Guid.NewGuid(),
        CategoryKey = "security"
    };

    /// <summary>
    /// Unconfigured must report *permanent* failure, so the policy resolver records the channel
    /// unsupported once rather than failing eight times up a retry ladder.
    /// </summary>
    [Fact]
    public async Task The_unconfigured_sender_reports_a_permanent_failure()
    {
        var sender = new UnconfiguredPushNotificationSender(
            NullLogger<UnconfiguredPushNotificationSender>.Instance);

        sender.IsConfigured.Should().BeFalse();

        var result = await sender.SendAsync(Message());

        result.Succeeded.Should().BeFalse();
        result.IsPermanent.Should().BeTrue();
        result.ReasonCode.Should().Be("NotConfigured");
    }

    /// <summary>
    /// Loopback must report *configured*, which is the only difference that matters: it is what makes
    /// the delivery row, the analytics rollup and the endpoint failure count all move.
    /// </summary>
    [Fact]
    public async Task The_loopback_sender_reports_configured_and_succeeds()
    {
        var sender = Loopback(PushProviderOptions.LoopbackProvider);

        sender.IsConfigured.Should().BeTrue();

        var result = await sender.SendAsync(Message(endpoints: 3));

        result.Succeeded.Should().BeTrue();
        result.DeadEndpoints.Should().BeEmpty();
        result.ProviderMessageId.Should().StartWith(
            "loopback:", "an operator searching a provider console for it must find nothing and know why");
    }

    /// <summary>
    /// It reports itself unconfigured when the deployment has not asked for it, so a stray
    /// registration cannot quietly start marking pushes delivered.
    /// </summary>
    [Fact]
    public void The_loopback_sender_is_inert_unless_the_deployment_named_it()
    {
        Loopback(PushProviderOptions.NoProvider).IsConfigured.Should().BeFalse();
        Loopback("Firebase").IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task A_send_with_no_endpoint_fails_permanently_rather_than_succeeding_emptily()
    {
        var result = await Loopback(PushProviderOptions.LoopbackProvider).SendAsync(Message(endpoints: 0));

        result.Succeeded.Should().BeFalse();
        result.IsPermanent.Should().BeTrue();
        result.ReasonCode.Should().Be("NoEndpoint");
    }

    [Theory]
    [InlineData("None", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("none", false)]
    [InlineData("Firebase", true)]
    [InlineData("Loopback", true)]
    public void Whether_a_provider_is_configured_is_read_case_insensitively(string provider, bool expected)
    {
        new PushProviderOptions { Provider = provider }.IsConfigured.Should().Be(expected);
    }

    /// <summary>
    /// <b>The registration a disabled deployment gets, and the defect it prevents.</b> With real-time
    /// off, <c>AddSignalR</c> is never called, so the SignalR notifier's own dependency is absent —
    /// leaving it registered would make every in-app delivery throw on a deployment whose only sin
    /// was setting a flag to false.
    /// </summary>
    [Fact]
    public async Task The_disabled_notifier_reports_disabled_and_swallows_every_send()
    {
        var notifier = new DisabledRealtimeNotifier();

        notifier.IsEnabled.Should().BeFalse();

        var payload = new InboxCounters(0, new Dictionary<string, int>(), DateTimeOffset.UtcNow);

        var sends = async () =>
        {
            await notifier.SendToUserAsync(UserId.New(), RealtimeEvents.InboxCountersChanged, payload);
            await notifier.SendToGroupAsync("group", RealtimeEvents.InboxCountersChanged, payload);
            await notifier.SendToGroupExceptAsync(
                "group", "c1", RealtimeEvents.InboxCountersChanged, payload);
        };

        await sends.Should().NotThrowAsync();
    }

    private static LoopbackPushNotificationSender Loopback(string provider) =>
        new(
            new StaticOptionsMonitor<PushProviderOptions>(
                new PushProviderOptions { Provider = provider }),
            NullLogger<LoopbackPushNotificationSender>.Instance);
}
