using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Creator.Application.Consumers;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Options;
using Diaspora.Modules.Financial.Application.Campaigns;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// What an earning links to, and what it must not.
///
/// <para>
/// <b>The defect these pin.</b> An earning published the <c>transactionId</c> of the capture that
/// funded it, and the contract described it as resolvable at
/// <c>GET /financial/transactions/{id}</c> — "which is what lets a creator open an earning and see
/// the money movement behind it". It never could. A donation capture posts clearing to platform
/// revenue: the earner owns neither leg and did not initiate the payment, and that route answers
/// 404 rather than 403 on purpose, so an identifier cannot be used to discover a transaction
/// exists. The revenue screen gated the row's tap on the field being present, so every earning
/// invited a tap that could only fail.
/// </para>
///
/// <para>
/// <b>Why the subject moved to the campaign.</b> The consumer named the donation, which is the
/// donor's record and not the beneficiary's. The campaign is the thing the earner owns, can open,
/// and would look for anyway — "who gave me a thousand pounds" is answered by the campaign page.
/// A donation carries no campaign reference, so the tie is the cause code, read by the module that
/// mints it.
/// </para>
/// </summary>
public sealed class CreatorEarningSubjectTests
{
    private static readonly Guid Beneficiary = Guid.NewGuid();
    private static readonly Guid CampaignGuid = Guid.NewGuid();
    private static readonly Guid Donation = Guid.NewGuid();

    // ── The cause code, both ways ───────────────────────────────────────────────────────────

    [Fact]
    public void A_minted_cause_code_reads_back_as_its_campaign()
    {
        // The round trip is the point: one place owns the format, and a second copy of it in
        // another module is how a campaign's donations quietly stop being attributed to it.
        var campaign = Campaign_();

        CampaignMapper.CampaignIdFrom(CampaignMapper.CauseCodeFor(campaign))
            .Should().Be(CampaignGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hurricane-appeal")]
    [InlineData("campaign-")]
    [InlineData("campaign-not-a-guid")]
    public void A_cause_of_another_kind_names_no_campaign(string? causeCode)
    {
        // A fund or a seeded appeal is a cause without a campaign. Answering null is the whole
        // contract; throwing here would kill the relay for every donation to one.
        CampaignMapper.CampaignIdFrom(causeCode).Should().BeNull();
    }

    // ── What the consumer records ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_campaign_donation_is_recorded_against_the_campaign()
    {
        var earnings = Consumer(out var consumer);

        await Deliver(consumer, Event(campaignId: CampaignGuid));

        var recorded = Recorded(earnings);
        recorded.SubjectType.Should().Be("campaign");
        recorded.SubjectId.Should().Be(CampaignGuid, "the campaign is what the earner can open");
    }

    [Fact]
    public async Task A_donation_with_no_campaign_still_names_the_donation()
    {
        // Not every donation to a member runs through a campaign. Naming the donation is no worse
        // than naming nothing, and the response omits a subject a client cannot route to.
        var earnings = Consumer(out var consumer);

        await Deliver(consumer, Event(campaignId: null));

        var recorded = Recorded(earnings);
        recorded.SubjectType.Should().Be("donation");
        recorded.SubjectId.Should().Be(Donation);
    }

    [Fact]
    public async Task The_transaction_is_still_recorded_on_the_row()
    {
        // Dropped from the response, kept on the row. It is the audit tie between an earning and
        // the money movement that funded it, and losing it would make a payout unreconcilable.
        var earnings = Consumer(out var consumer);
        var completed = Event(campaignId: CampaignGuid);

        await Deliver(consumer, completed);

        Recorded(earnings).TransactionId.Should().Be(completed.TransactionId);
    }

    // ── What the response publishes ─────────────────────────────────────────────────────────

    [Fact]
    public void The_response_does_not_publish_a_transaction_identifier()
    {
        // Asserted on the type rather than on a value, because the defect was not a wrong value —
        // it was publishing a field at all, described as openable, that the earner cannot open.
        // Re-adding it would restore a tap that can only 404.
        typeof(CreatorEarningResponse).GetProperty("TransactionId")
            .Should().BeNull(
                "an earner owns no leg of the capture that funded them, and the transactions "
                + "route answers 404 by design");
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private static DonationCompletedIntegrationEvent Event(Guid? campaignId) => new()
    {
        DonationId = Donation,
        TransactionId = Guid.NewGuid(),
        DonorUserId = Guid.NewGuid(),
        IsAnonymous = false,
        BeneficiaryType = "Member",
        BeneficiaryId = Beneficiary,
        CauseCode = campaignId is { } id ? $"campaign-{id:n}" : null,
        CampaignId = campaignId,
        AmountMinorUnits = 100_000,
        Currency = "GBP",
        ReceiptReference = "R-1",
        CompletedAt = DateTimeOffset.UtcNow,
    };

    private static Campaign Campaign_() =>
        Campaign.Open(
            CampaignId.From(CampaignGuid),
            Guid.NewGuid(),
            "Ada Nwosu",
            "Help Ada travel home",
            "A short summary of why.",
            "The whole story, at length.",
            CampaignCategory.Repatriation,
            BeneficiaryType.Member,
            "Ada Nwosu",
            "GBP",
            2,
            acceptedTerms: true,
            DateTimeOffset.UtcNow,
            target: Money.FromAmount(100m, "GBP")).Value;

    private static Task Deliver(
        DonationCompletedEarningConsumer consumer, DonationCompletedIntegrationEvent completed) =>
        consumer.Handle(
            new IntegrationEventNotification<DonationCompletedIntegrationEvent>(completed),
            CancellationToken.None);

    private static ICreatorEarningRepository Consumer(out DonationCompletedEarningConsumer consumer)
    {
        var creators = Substitute.For<ICreatorProfileRepository>();
        var earnings = Substitute.For<ICreatorEarningRepository>();

        creators
            .FindByUserAsync(Beneficiary, Arg.Any<CancellationToken>())
            .Returns(CreatorProfile.Open(
                CreatorId.From(Guid.NewGuid()), Beneficiary, "Member", "GB", DateTimeOffset.UtcNow));

        var recorder = new CreatorEarningRecorder(
            creators,
            earnings,
            Substitute.For<IUnitOfWork>(),
            Options.Create(new CreatorRevenueShareOptions()));

        consumer = new DonationCompletedEarningConsumer(recorder);
        return earnings;
    }

    private static CreatorEarning Recorded(ICreatorEarningRepository earnings)
    {
        var calls = earnings.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ICreatorEarningRepository.Add))
            .ToList();

        calls.Should().ContainSingle("the consumer records exactly one earning");
        return (CreatorEarning)calls[0].GetArguments()[0]!;
    }
}
