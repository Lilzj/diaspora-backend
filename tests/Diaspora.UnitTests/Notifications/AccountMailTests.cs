using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Extensibility.Throttling;
using Diaspora.Modules.Notifications.Infrastructure.Channels;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// Verification results reach the mailbox even though email starts switched off.
/// <para>
/// Decided 7 Oct 2026. Before it, an approval's email was suppressed as <c>ChannelDisabled</c> for
/// every member who had never opened their notification settings — which is every new member,
/// because email defaults to off — so the only copy went to the inbox of an app the member was
/// locked out of.
/// </para>
/// </summary>
public sealed class AccountMailTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly UserId Member = UserId.From(Guid.NewGuid());

    [Theory]
    [InlineData("trust.verification.approved")]
    [InlineData("trust.verification.rejected")]
    [InlineData("trust.verification.expired")]
    public async Task A_verification_result_is_emailed_with_email_switched_off(string ruleKey)
    {
        var email = await EmailPlanAsync(ruleKey, NewMember());

        email.Outcome.Should().NotBe(DeliveryStatus.Suppressed, email.ReasonCode);
    }

    [Fact]
    public async Task Any_other_message_still_respects_the_switch()
    {
        // The exception is three rules wide, not "everything High".
        var email = await EmailPlanAsync("financial.wallet.status-changed", NewMember());

        email.Outcome.Should().Be(DeliveryStatus.Suppressed);
        email.ReasonCode.Should().Be(SuppressionReasons.ChannelDisabled);
    }

    [Fact]
    public async Task Do_not_disturb_still_holds_it()
    {
        // A specific, deliberate choice by the member. Only the default-off switch is passed.
        var preference = NewMember();
        preference.SetDoNotDisturb(true);

        var email = await EmailPlanAsync("trust.verification.approved", preference);

        email.Outcome.Should().Be(DeliveryStatus.Suppressed);
        email.ReasonCode.Should().Be(SuppressionReasons.DoNotDisturb);
    }

    [Fact]
    public async Task A_muted_verification_category_still_holds_it()
    {
        var preference = NewMember();
        preference.SetCategory(CategoryPreference.Create("trust", isMuted: true));

        var email = await EmailPlanAsync("trust.verification.approved", preference);

        email.Outcome.Should().Be(DeliveryStatus.Suppressed);
        email.ReasonCode.Should().Be(SuppressionReasons.CategoryMuted);
    }

    private static NotificationPreference NewMember() =>
        NotificationPreference.CreateDefault(NotificationPreferenceId.New(), Member, Now);

    private static async Task<ChannelPlan> EmailPlanAsync(string ruleKey, NotificationPreference preference)
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.Kind.Returns(NotificationChannelKind.Email);
        channel.IsAvailable.Returns(true);

        var throttle = new OpenThrottle();

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var rule = NotificationRule.Create(
            NotificationRuleId.New(),
            ruleKey,
            "SomeIntegrationEvent",
            "member",
            "some.template",
            "trust",
            NotificationPriority.High,
            DeliveryPolicy.Immediate,
            [NotificationChannelKind.InApp, NotificationChannelKind.Email],
            "Notifications",
            Now).Value;

        var resolver = new DeliveryPolicyResolver(new NotificationChannelRegistry([channel]), throttle, clock);

        var plans = await resolver.ResolveAsync(
            Member, rule, category: null, preference, NotificationPriority.High, scheduledFor: null);

        return plans.Single(plan => plan.Channel == NotificationChannelKind.Email);
    }
}

/// <summary>Never throttles. The resolver's throttle step is not what these tests are about.</summary>
file sealed class OpenThrottle : INotificationThrottle
{
    public ValueTask<bool> TryAcquireAsync(
        UserId userId,
        NotificationChannelKind channel,
        NotificationPriority priority,
        CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
}
