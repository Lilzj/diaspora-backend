using System.Net.Http.Json;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Financial.Application.Consumers.Rewards;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The shared-device signal, from Identity's count to the hold staff review (D-13, CS-D5).
/// <para>
/// Identity counts the other accounts on a newly registered device and says how many, never who.
/// Financial holds that member's points for review, decided by nobody, and Creator flags the profile
/// so a referral the member was invited by waits for the same review. Staff lifting the hold, from the
/// review queue, is the review: it clears the flag.
/// </para>
/// <para>
/// The signal is off in this host, as it is in every deployment until the privacy notice says so;
/// the switched-on half is unit-tested on Identity's handler, and here the announcement is published
/// through the outbox as Identity would publish it.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class SharedDeviceSignalTests : PooledIdentityTestBase
{
    public SharedDeviceSignalTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Accounts_on_one_device_are_counted_without_naming_any_of_them()
    {
        var shared = NewDevice();
        var alone = NewDevice();
        var (_, first) = await RegisterAndVerifyAsync(deviceIdentifier: shared);
        var (_, second) = await RegisterAndVerifyAsync(deviceIdentifier: shared);
        var (_, elsewhere) = await RegisterAndVerifyAsync(deviceIdentifier: alone);

        using var scope = Factory.Services.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IUserDeviceRepository>();

        (await devices.CountOtherAccountsAsync(UserId.From(second.User.Id), shared)).Should().Be(1);
        (await devices.CountOtherAccountsAsync(UserId.From(first.User.Id), shared)).Should().Be(1);
        (await devices.CountOtherAccountsAsync(UserId.From(elsewhere.User.Id), alone)).Should().Be(0);
    }

    [Fact]
    public async Task Sharing_a_device_holds_nothing_while_the_signal_is_off()
    {
        var shared = NewDevice();
        await RegisterAndVerifyAsync(deviceIdentifier: shared);
        var (_, second) = await RegisterAndVerifyAsync(deviceIdentifier: shared);
        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();

        var standing = await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>().FindStandingAsync(second.User.Id);
        (standing?.IsConversionBlocked ?? false).Should().BeFalse();

        (await scope.ServiceProvider.GetRequiredService<ICreatorProfileRepository>()
            .FindByUserAsync(second.User.Id))!.IsSharedDeviceFlagged.Should().BeFalse();
    }

    [Fact]
    public async Task A_shared_device_holds_the_members_points_by_nobody_and_flags_their_profile()
    {
        var (_, member) = await RegisterAndVerifyAsync();
        await Factory.DrainOutboxAsync();

        await AnnounceAsync(member.User.Id, otherAccounts: 1);
        await AnnounceAsync(member.User.Id, otherAccounts: 2);

        using var scope = Factory.Services.CreateScope();

        var account = await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>().FindByMemberAsync(member.User.Id);
        account.Should().NotBeNull("a hold opens the account it holds, as a staff hold does");
        account!.IsConversionBlocked.Should().BeTrue();
        var decision = account.HoldDecisions.Should().ContainSingle("a second announcement holds nothing twice").Subject;
        decision.IsHeld.Should().BeTrue();
        decision.DecidedByUserId.Should().BeNull("the platform held this member itself");
        decision.Reason.Should().Be(AccountDeviceSharedRewardHoldConsumer.Reason);

        (await scope.ServiceProvider.GetRequiredService<ICreatorProfileRepository>()
            .FindByUserAsync(member.User.Id))!.IsSharedDeviceFlagged.Should().BeTrue();
    }

    /// <summary>A hold staff already placed keeps its note: the system does not write over a person.</summary>
    [Fact]
    public async Task A_member_staff_already_hold_keeps_the_staff_note()
    {
        var (_, member) = await RegisterAndVerifyAsync();
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Admin");
        (await PutHoldAsync(member.User.Id, true, "Chargeback, case 12")).EnsureSuccessStatusCode();

        await AnnounceAsync(member.User.Id, otherAccounts: 1);

        using var scope = Factory.Services.CreateScope();
        var account = (await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>().FindByMemberAsync(member.User.Id))!;

        account.BlockReason.Should().Be("Chargeback, case 12");
        account.HoldDecisions.Should().ContainSingle();
    }

    [Fact]
    public async Task Staff_lifting_the_hold_is_the_review_and_clears_the_flag()
    {
        var (_, member) = await RegisterAndVerifyAsync();
        await Factory.DrainOutboxAsync();
        await AnnounceAsync(member.User.Id, otherAccounts: 1);

        await SignInAsRoleAsync("Admin");
        (await PutHoldAsync(member.User.Id, false, "A family phone; reviewed.")).EnsureSuccessStatusCode();
        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();

        (await scope.ServiceProvider.GetRequiredService<ICreatorProfileRepository>()
            .FindByUserAsync(member.User.Id))!.IsSharedDeviceFlagged.Should().BeFalse();
    }

    private static string NewDevice() => "install-" + Guid.NewGuid().ToString("N");

    /// <summary>The announcement as Identity publishes it, through the outbox.</summary>
    private async Task AnnounceAsync(Guid userId, int otherAccounts)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().PublishAsync(
                new AccountDeviceSharedIntegrationEvent
                {
                    UserId = userId,
                    OtherAccounts = otherAccounts,
                    DetectedAt = DateTimeOffset.UtcNow
                });

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await Factory.DrainOutboxAsync();
    }

    private Task<HttpResponseMessage> PutHoldAsync(Guid userId, bool isHeld, string reason) =>
        HttpClient.PutAsJsonAsync(
            new Uri($"/api/v1/financial/operations/rewards/members/{userId}/conversion-hold", UriKind.Relative),
            new { isHeld, reason });
}
