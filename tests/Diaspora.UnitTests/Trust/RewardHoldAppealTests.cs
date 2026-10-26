using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Financial.Application.Rewards;
using Diaspora.Modules.Trust.Application.EventHandlers;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// A member whose points are held can ask for a review, through Trust's appeals (RWD-14, CS-4a).
/// <para>
/// A held member was told to contact support, and there was nothing to contact. A hold is now one
/// more thing a member can appeal: the target is the member themselves, and an approved appeal is
/// announced with the reviewer who approved it, so the Financial Platform can lift the hold in that
/// reviewer's name.
/// </para>
/// </summary>
public sealed class RewardHoldAppealTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member = UserId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private static readonly UserId Reviewer = UserId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

    [Fact]
    public void A_points_hold_is_something_a_member_can_appeal()
    {
        Enum.GetNames<AppealType>().Should().Contain("RewardHold");
    }

    [Fact]
    public void An_approved_appeal_names_the_reviewer_who_approved_it()
    {
        var appeal = Appeal.File(
            Member, AppealType.RewardHold, Member.Value.ToString(),
            "My points are paused and I share a phone with my sister.", Now);

        appeal.Decide(Reviewer, upheld: true, "Reviewed: a family phone.", Now);

        appeal.DomainEvents.OfType<AppealApprovedDomainEvent>().Should().ContainSingle()
            .Which.DecidedBy.Should().Be(Reviewer);
    }

    [Fact]
    public async Task The_published_approval_carries_the_reviewer()
    {
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var handler = new AppealApprovedEventHandler(publisher);

        await handler.Handle(
            new DomainEventNotification<AppealApprovedDomainEvent>(new AppealApprovedDomainEvent(
                AppealId.New(), Member, AppealType.RewardHold, Member.Value.ToString(), "Reviewed.", Now, Reviewer)),
            CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            Arg.Is<IIntegrationEvent>(published =>
                published is AppealApprovedIntegrationEvent
                && ((AppealApprovedIntegrationEvent)published).DecidedByUserId == Reviewer.Value
                && ((AppealApprovedIntegrationEvent)published).AppealType == "RewardHold"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>The member is told there is a way to ask, and still nothing about why they are held.</summary>
    [Fact]
    public void The_held_message_offers_a_review_and_names_no_check()
    {
        RewardConversionPricer.HeldMessage.Should().Contain("ask us to review it");
        RewardConversionPricer.HeldMessage.Should().NotContainAny("support", "device", "risk", "fraud", "flag");
    }
}
