using Diaspora.Contracts.Events;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Creator.Application.EventHandlers;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Platform.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// Which publish failures the Creator relay may swallow.
/// <para>
/// An announcement that fails is logged and the decision stands. A points grant that fails is a
/// payment that will never be made, so it must reach the save and roll the claim back. And a
/// cancelled request is not a failed announcement at all.
/// </para>
/// </summary>
public sealed class CreatorIntegrationEventRelayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly IIntegrationEventPublisher _publisher = Substitute.For<IIntegrationEventPublisher>();

    private CreatorIntegrationEventRelay Relay() =>
        new(_publisher, NullLogger<CreatorIntegrationEventRelay>.Instance);

    [Fact]
    public async Task A_points_grant_that_cannot_be_enqueued_is_not_swallowed()
    {
        _publisher.PublishAsync(Arg.Any<RewardPointsGrantedIntegrationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Outbox payload too large."));

        var publishing = () => Relay().PublishAsync(PointsEarned());

        await publishing.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_points_grant_is_published_with_its_reference()
    {
        await Relay().PublishAsync(PointsEarned());

        await _publisher.Received(1).PublishAsync(
            Arg.Is<IIntegrationEvent>(published =>
                published is RewardPointsGrantedIntegrationEvent
                && ((RewardPointsGrantedIntegrationEvent)published).GrantReference == "milestone:first-guide:x"
                && ((RewardPointsGrantedIntegrationEvent)published).Points == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_announcement_is_still_logged_and_swallowed()
    {
        _publisher.PublishAsync(Arg.Any<IIntegrationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Outbox payload too large."));

        var publishing = () => Relay().PublishAsync(StatusChanged());

        await publishing.Should().NotThrowAsync("a lost notification must not undo a decision");
    }

    [Fact]
    public async Task Cancellation_is_never_swallowed()
    {
        _publisher.PublishAsync(Arg.Any<IIntegrationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var publishing = () => Relay().PublishAsync(StatusChanged());

        await publishing.Should().ThrowAsync<OperationCanceledException>();
    }

    private static CreatorPointsEarnedDomainEvent PointsEarned() => new(
        CreatorId.New(), Guid.NewGuid(), 100, "Milestone", "milestone:first-guide:x", Now);

    private static CreatorStatusChangedDomainEvent StatusChanged() => new(
        CreatorId.New(), Guid.NewGuid(), CreatorStatus.NotACreator, CreatorStatus.Contributor, Now);
}
