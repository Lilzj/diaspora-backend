using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.EventHandlers;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Policies;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Identity;

/// <summary>
/// A device another account already uses is announced, as a count (D-13, CS-D5).
/// <para>
/// One member running several accounts from one phone is how a referral or a bonus is farmed.
/// Identity is the only module that sees devices, so it says when a new one is shared, and how many
/// other accounts share it, and never who they are. The signal ships off: it waits on the privacy
/// notice saying so.
/// </para>
/// </summary>
public sealed class SharedDeviceSignalTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member = UserId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private const string Identifier = "install-7f3a";

    [Fact]
    public async Task A_device_two_other_accounts_use_is_announced_with_the_count_alone()
    {
        var harness = new Harness(enabled: true, otherAccounts: 2);

        await harness.RegisterAsync();

        var published = harness.Published.Should().ContainSingle().Which
            .Should().BeOfType<AccountDeviceSharedIntegrationEvent>().Subject;
        published.UserId.Should().Be(Member.Value);
        published.OtherAccounts.Should().Be(2);
        published.DetectedAt.Should().Be(Now);
        typeof(AccountDeviceSharedIntegrationEvent).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Identifier", StringComparison.Ordinal),
                "the device is Identity's to know");
    }

    [Fact]
    public async Task A_device_no_other_account_uses_is_not_announced()
    {
        var harness = new Harness(enabled: true, otherAccounts: 0);

        await harness.RegisterAsync();

        harness.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task The_signal_is_off_until_it_is_switched_on()
    {
        var harness = new Harness(enabled: false, otherAccounts: 3);

        await harness.RegisterAsync();

        harness.Published.Should().BeEmpty();
        await harness.Devices.DidNotReceiveWithAnyArgs().CountOtherAccountsAsync(default!, default!, default);
    }

    [Fact]
    public async Task The_registration_is_still_audited_when_the_device_is_shared()
    {
        var harness = new Harness(enabled: true, otherAccounts: 1);

        await harness.RegisterAsync();

        await harness.Audit.ReceivedWithAnyArgs(1).WriteAsync(default!, default);
    }

    private sealed class Harness
    {
        private readonly UserDeviceRegisteredEventHandler _handler;

        public Harness(bool enabled, int otherAccounts)
        {
            Devices = Substitute.For<IUserDeviceRepository>();
            Devices.CountOtherAccountsAsync(Member, Identifier, Arg.Any<CancellationToken>())
                .Returns(otherAccounts);

            var publisher = Substitute.For<IIntegrationEventPublisher>();
            publisher
                .When(item => item.PublishAsync(Arg.Any<IIntegrationEvent>(), Arg.Any<CancellationToken>()))
                .Do(call => Published.Add(call.Arg<IIntegrationEvent>()));

            var policies = Substitute.For<IIdentityPolicyProvider>();
            policies.Current.Returns(Policies(enabled));

            Audit = Substitute.For<IIdentityAuditWriter>();

            _handler = new UserDeviceRegisteredEventHandler(Audit, publisher, Devices, policies);
        }

        public IUserDeviceRepository Devices { get; }

        public IIdentityAuditWriter Audit { get; }

        public List<IIntegrationEvent> Published { get; } = [];

        public Task RegisterAsync() =>
            _handler.Handle(
                new DomainEventNotification<UserDeviceRegisteredDomainEvent>(
                    new UserDeviceRegisteredDomainEvent(Member, DeviceId.New(), Identifier, DevicePlatform.Android, Now)),
                CancellationToken.None);

        private static IdentityPolicySet Policies(bool enabled) => new()
        {
            Otp = OtpPolicy.Create(TimeSpan.FromMinutes(5), 5, 3, TimeSpan.FromSeconds(30)),
            Lockout = LockoutPolicy.Create(5, TimeSpan.FromMinutes(15)),
            PasswordHistory = PasswordHistoryPolicy.Disabled,
            RefreshTokenLifetime = TimeSpan.FromDays(30),
            AbsoluteSessionLifetime = TimeSpan.FromDays(90),
            IncludeFeaturesInAccessToken = false,
            MaxDevicesPerUser = 10,
            MaxRecoveryRequestsPerWindow = 5,
            RecoveryThrottleWindow = TimeSpan.FromMinutes(60),
            EnableSharedDeviceSignal = enabled
        };
    }
}
