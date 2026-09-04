using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The advertising campaign against a real database.
/// <para>
/// <b>Three things here cannot be reached by a unit test, and each is a rule the aggregate cannot
/// enforce alone.</b> One placement belongs to one campaign — a campaign cannot see the other
/// campaigns, so only a unique index stops two claiming the same booked slot and double-counting it
/// in every spend rollup. The audience is five primitive collections inside an owned type, and
/// whether it survives a round trip is a mapping question, not a domain one. And the ownership check
/// lives inside a query, so the only way to know a stranger gets nothing is to ask as one.
/// </para>
/// </summary>
public sealed class AdCampaignRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly CustomWebApplicationFactory _factory;

    public AdCampaignRepositoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        // The schema is created on demand — a fixture that skips this gets "no such table" on its
        // first write and passes every assertion that expected an absence.
        _factory.EnsureDatabaseCreated();
    }

    // ── The index ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task One_placement_cannot_belong_to_two_campaigns()
    {
        // Without this, a booked slot counts twice in every per-campaign spend rollup, and neither
        // campaign is wrong on its own.
        var placementId = PlacementId.New();

        await WriteAsync(async (campaigns, unitOfWork) =>
        {
            var first = Drafted("First claim");
            first.AddPlacement(AdCampaignPlacement.For(placementId, PlacementKind.Featured));
            campaigns.Add(first);
            await unitOfWork.SaveChangesAsync();
        });

        var second = async () => await WriteAsync(async (campaigns, unitOfWork) =>
        {
            var other = Drafted("Second claim");
            other.AddPlacement(AdCampaignPlacement.For(placementId, PlacementKind.Featured));
            campaigns.Add(other);
            await unitOfWork.SaveChangesAsync();
        });

        await second.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task A_campaign_refuses_the_same_placement_twice_before_the_database_has_to()
    {
        // The aggregate can see its own placements even though it cannot see other campaigns', so
        // this one is a refusal rather than an exception.
        var campaign = Drafted("Double booked");
        var placementId = PlacementId.New();

        campaign.AddPlacement(AdCampaignPlacement.For(placementId, PlacementKind.Featured))
            .IsSuccess.Should().BeTrue();

        campaign.AddPlacement(AdCampaignPlacement.For(placementId, PlacementKind.Boost))
            .IsFailure.Should().BeTrue();
    }

    // ── The round trip ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_audience_survives_a_round_trip_intact()
    {
        // Five primitive collections inside an owned type. Whether they come back is a mapping
        // question the aggregate's own tests cannot answer.
        var community = Guid.NewGuid();
        var neighbourhood = Guid.NewGuid();
        var id = AdCampaignId.New();

        await WriteAsync(async (campaigns, unitOfWork) =>
        {
            var campaign = Drafted("Audience round trip", id);

            campaign.Update(
                "Audience round trip",
                AdCampaignObjective.Leads,
                Money.FromMinorUnits(25_000, "GBP"),
                DateWindow.Create(Now, Now.AddDays(14)),
                AdAudience.Create(
                    countryCodes: ["GB", "NG"],
                    communityIds: [community],
                    neighbourhoodIds: [neighbourhood],
                    interests: ["technology", "music"],
                    categories: ["software"]));

            campaigns.Add(campaign);
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async campaigns =>
        {
            var stored = await campaigns.FindAsync(id);

            stored.Should().NotBeNull();
            stored!.Audience.CountryCodes.Should().BeEquivalentTo(["GB", "NG"]);
            stored.Audience.CommunityIds.Should().BeEquivalentTo([community]);
            stored.Audience.NeighbourhoodIds.Should().BeEquivalentTo([neighbourhood]);
            stored.Audience.Interests.Should().BeEquivalentTo(["technology", "music"]);
            stored.Audience.Categories.Should().BeEquivalentTo(["software"]);

            stored.Audience.IsEveryone.Should().BeFalse();
        });
    }

    [Fact]
    public async Task Money_survives_a_round_trip_as_three_columns()
    {
        var id = AdCampaignId.New();

        await WriteAsync(async (campaigns, unitOfWork) =>
        {
            var campaign = Drafted("Money round trip", id);
            campaigns.Add(campaign);
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async campaigns =>
        {
            var stored = await campaigns.FindAsync(id);

            stored!.Budget.MinorUnits.Should().Be(10_000);
            stored.Budget.Currency.Should().Be("GBP");
            stored.Spend.MinorUnits.Should().Be(0);
            stored.RemainingBudget.MinorUnits.Should().Be(10_000);

            stored.SettledAmount.Should().BeNull("nothing has been cancelled");
        });
    }

    // ── Ownership ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_campaign_somebody_else_owns_reads_as_absent()
    {
        // The check is inside the query so a handler cannot load a campaign it may not see and then
        // forget to compare. A competitor's budget, spend and targeting are all on this row.
        var id = AdCampaignId.New();
        var owner = Guid.NewGuid();

        await WriteAsync(async (campaigns, unitOfWork) =>
        {
            campaigns.Add(Drafted("Private numbers", id, owner));
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async campaigns =>
        {
            (await campaigns.FindForOwnerAsync(id, owner)).Should().NotBeNull();
            (await campaigns.FindForOwnerAsync(id, Guid.NewGuid())).Should().BeNull();
        });
    }

    [Fact]
    public async Task The_owner_list_returns_terminal_campaigns_too()
    {
        // A completed campaign is what an advertiser goes back to when they want to run it again.
        var owner = Guid.NewGuid();

        await WriteAsync(async (campaigns, unitOfWork) =>
        {
            var live = Drafted("Still going", AdCampaignId.New(), owner);

            var finished = Drafted("All done", AdCampaignId.New(), owner);
            finished.Cancel("Changed our minds.", Now);

            campaigns.Add(live);
            campaigns.Add(finished);
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async campaigns =>
        {
            var mine = await campaigns.ListForOwnerAsync(owner);

            mine.Should().HaveCount(2);
            mine.Should().Contain(campaign => campaign.Status == AdCampaignStatus.Cancelled);
        });
    }

    [Fact]
    public async Task The_owner_list_filters_by_state_when_asked()
    {
        var owner = Guid.NewGuid();

        await WriteAsync(async (campaigns, unitOfWork) =>
        {
            var draft = Drafted("A draft", AdCampaignId.New(), owner);

            var cancelled = Drafted("Cancelled", AdCampaignId.New(), owner);
            cancelled.Cancel("No.", Now);

            campaigns.Add(draft);
            campaigns.Add(cancelled);
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async campaigns =>
        {
            var drafts = await campaigns.ListForOwnerAsync(owner, [AdCampaignStatus.Draft]);

            drafts.Should().OnlyContain(campaign => campaign.Status == AdCampaignStatus.Draft);
            drafts.Should().NotBeEmpty();
        });
    }

    // ── The queue and the sweep ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_review_queue_holds_only_what_is_waiting_and_puts_the_oldest_first()
    {
        var owner = Guid.NewGuid();

        await WriteAsync(async (campaigns, unitOfWork) =>
        {
            var waiting = Drafted("Waiting for review", AdCampaignId.New(), owner);
            waiting.Submit(Now);
            waiting.MarkFunded(MonetizationPurchaseId.New());

            var stillDrafting = Drafted("Not submitted", AdCampaignId.New(), owner);

            campaigns.Add(waiting);
            campaigns.Add(stillDrafting);
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async campaigns =>
        {
            var queue = await campaigns.ListAwaitingReviewAsync();

            queue.Should().NotBeEmpty();
            queue.Should().OnlyContain(campaign =>
                campaign.Status == AdCampaignStatus.Submitted
                || campaign.Status == AdCampaignStatus.UnderReview);

            queue.Should().BeInAscendingOrder(campaign => campaign.CreatedAt);
        });
    }

    [Fact]
    public async Task The_sweep_finds_campaigns_due_to_start_and_campaigns_that_are_over()
    {
        var owner = Guid.NewGuid();
        var due = AdCampaignId.New();
        var over = AdCampaignId.New();

        await WriteAsync(async (campaigns, unitOfWork) =>
        {
            // Approved, and its window has opened.
            var ready = Approved("Due to start", due, owner, startsAt: Now.AddDays(-1));

            // Running, and its window has closed.
            var finished = Approved("Over", over, owner, startsAt: Now.AddDays(-30));
            finished.MarkScheduled();
            finished.Activate(Now.AddDays(-30));

            campaigns.Add(ready);
            campaigns.Add(finished);
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async campaigns =>
        {
            var swept = await campaigns.ListForDeliverySweepAsync(Now);
            var ids = swept.Select(campaign => campaign.Id).ToList();

            ids.Should().Contain(due);
            ids.Should().Contain(over);
        });
    }

    [Fact]
    public async Task The_sweep_includes_a_paused_campaign_whose_window_has_closed()
    {
        // Its window closes whether or not it is delivering, and leaving it Paused for ever would
        // leave its budget unsettled.
        var id = AdCampaignId.New();

        await WriteAsync(async (campaigns, unitOfWork) =>
        {
            var paused = Approved("Paused and over", id, Guid.NewGuid(), startsAt: Now.AddDays(-30));
            paused.MarkScheduled();
            paused.Activate(Now.AddDays(-30));
            paused.Pause(Now.AddDays(-10));

            campaigns.Add(paused);
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async campaigns =>
        {
            var swept = await campaigns.ListForDeliverySweepAsync(Now);

            swept.Select(campaign => campaign.Id).Should().Contain(id);
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static AdCampaign Drafted(
        string name, AdCampaignId? id = null, Guid? owner = null) =>
        AdCampaign.Draft(
            id ?? AdCampaignId.New(),
            owner ?? Guid.NewGuid(),
            name,
            AdCampaignObjective.Traffic,
            Money.FromMinorUnits(10_000, "GBP"),
            DateWindow.Create(Now, Now.AddDays(30))).Value;

    private static AdCampaign Approved(
        string name, AdCampaignId id, Guid owner, DateTimeOffset startsAt)
    {
        var campaign = AdCampaign.Draft(
            id, owner, name, AdCampaignObjective.Awareness,
            Money.FromMinorUnits(10_000, "GBP"),
            DateWindow.Create(startsAt, startsAt.AddDays(7))).Value;

        campaign.Submit(startsAt);
        campaign.MarkFunded(MonetizationPurchaseId.New());
        campaign.Approve(Guid.NewGuid(), null, startsAt);

        return campaign;
    }

    private async Task WriteAsync(Func<IAdCampaignRepository, IUnitOfWork, Task> work)
    {
        using var scope = _factory.Services.CreateScope();

        await work(
            scope.ServiceProvider.GetRequiredService<IAdCampaignRepository>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }

    private async Task ReadAsync(Func<IAdCampaignRepository, Task> work)
    {
        using var scope = _factory.Services.CreateScope();

        await work(scope.ServiceProvider.GetRequiredService<IAdCampaignRepository>());
    }
}
