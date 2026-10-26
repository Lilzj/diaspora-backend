using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Trust.Application.Consumers;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A confirmed outcome pays reward points through the outbox, and its removal takes them back (D-2,
/// D-9, D-12).
/// <para>
/// The unit tests prove Trust publishes the grant. This proves the half they cannot: that the grant
/// reaches the Financial Platform's points account under the award's key, pending until it vests,
/// and that the reversal finds it there.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ReputationPointsTests : PooledIdentityTestBase
{
    public ReputationPointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task An_approved_guide_pays_its_points_and_its_deletion_takes_them_back()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        await Factory.DrainOutboxAsync();

        var guideId = Guid.NewGuid();
        var verified = new GuideVerificationChangedIntegrationEvent
        {
            GuideId = guideId,
            State = "Verified",
            ChangedAt = DateTimeOffset.UtcNow,
            AuthorUserId = auth.User.Id
        };

        await DeliverAsync<GuideVerifiedReputationConsumer, GuideVerificationChangedIntegrationEvent>(verified);
        await DeliverAsync<GuideVerifiedReputationConsumer, GuideVerificationChangedIntegrationEvent>(verified);
        await Factory.DrainOutboxAsync();

        var account = await RewardAccountAsync(auth.User.Id);
        var grant = account.Entries.Should().ContainSingle(entry => entry.Source == ReputationActions.GuideApproved,
            "a redelivered approval pays once").Subject;

        grant.Points.Should().Be(250);
        grant.Reference.Should().Be(ReputationAwardKey.GuideApproved(guideId).Value);
        grant.PolicyVersion.Should().Be(ReputationService.PolicyVersion);
        grant.IsPending.Should().BeTrue("granted points vest after a week (D-9)");

        await DeliverAsync<GuideDeletedReversalConsumer, GuideDeletedIntegrationEvent>(new GuideDeletedIntegrationEvent
        {
            GuideId = guideId,
            AuthorId = auth.User.Id,
            DeletedAt = DateTimeOffset.UtcNow
        });
        await Factory.DrainOutboxAsync();

        account = await RewardAccountAsync(auth.User.Id);
        account.LifetimeReversed.Should().Be(250, "the pending points went with the award");
        account.Entries.Single(entry => entry.Reference == grant.Reference).ReversedAt.Should().NotBeNull();
        account.Entries.Should().ContainSingle(entry => entry.IsReversal);
    }

    private async Task DeliverAsync<TConsumer, TEvent>(TEvent integrationEvent)
        where TConsumer : IntegrationEventHandler<TEvent>
        where TEvent : Diaspora.Contracts.Events.IIntegrationEvent
    {
        using var scope = Factory.Services.CreateScope();

        var consumer = ActivatorUtilities.CreateInstance<TConsumer>(scope.ServiceProvider);

        await consumer.Handle(new IntegrationEventNotification<TEvent>(integrationEvent), CancellationToken.None);
    }

    private async Task<RewardAccount> RewardAccountAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var account = await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(userId);

        account.Should().NotBeNull("the grant opens the account");

        return account!;
    }
}
