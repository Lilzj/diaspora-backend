using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Application.Consumers.Advertising;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The advertising campaign over real HTTP — MON-GAP-001, -006 and -007.
/// <para>
/// <b>The whole surface was drawn against simulated persistence for a phase</b>, so what these pin
/// is not that the screens work but that the entity behind them is real: a draft that survives a
/// round trip, a state machine the client cannot talk its way past, and an ownership rule that holds
/// when somebody else asks.
/// </para>
/// <para>
/// The wire shapes are re-declared here rather than imported from the module, for the reason the
/// B12 suite records: a contract test bound to the module's own records passes whenever the module
/// compiles.
/// </para>
/// </summary>
public sealed class AdCampaignEndpointTests : IdentityTestBase
{
    private const string Root = "/api/v1/financial";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AdCampaignEndpointTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Drafting ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_campaign_is_created_as_an_editable_draft()
    {
        using var client = await AdvertiserAsync();

        var campaign = await ReadDataAsync<CampaignDto>(
            await client.PostAsJsonAsync($"{Root}/campaigns", NewCampaign()));

        campaign.Status.Should().Be("Draft");
        campaign.CanEdit.Should().BeTrue();
        campaign.CanSubmit.Should().BeTrue();
        campaign.CanPause.Should().BeFalse();
        campaign.Spend.MinorUnits.Should().Be(0);
        campaign.Remaining.MinorUnits.Should().Be(5_000);
        campaign.Review.Decision.Should().Be("Pending");
    }

    /// <summary>
    /// <b>The five "can I" flags come from the server.</b> A client deriving "pausable" from a
    /// status string would be re-implementing a twelve-state machine on a device, which is how a
    /// Pause button appears on a draft.
    /// </summary>
    [Fact]
    public async Task The_permitted_actions_change_with_the_state()
    {
        using var client = await AdvertiserAsync();
        var created = await CreateAsync(client);

        var submitted = await ReadDataAsync<SubmissionDto>(
            await client.PostAsJsonAsync($"{Root}/campaigns/{created.Id}/submit", new { }));

        submitted.Campaign.Status.Should().Be("PendingPayment");
        submitted.Campaign.CanEdit.Should().BeFalse("submission is what freezes it, not payment");
        submitted.Campaign.CanSubmit.Should().BeFalse();
        submitted.Campaign.CanCancel.Should().BeTrue();

        // Everything the client needs to pay, in one call: the package, what it is for, and what
        // it costs. The intent itself is opened by the existing purchase route — see the handler.
        submitted.ProductCode.Should().Be("advertising.campaign.standard.30d");
        submitted.SubjectType.Should().Be("Campaign");
        submitted.SubjectId.Should().Be(created.Id);
        submitted.Amount.MinorUnits.Should().Be(5_000);
    }

    [Fact]
    public async Task A_campaign_with_no_end_date_is_refused()
    {
        // An advertising campaign may not run open-ended, unlike a fundraising one.
        using var client = await AdvertiserAsync();

        var response = await client.PostAsJsonAsync(
            $"{Root}/campaigns", NewCampaign() with { EndsAt = null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("ad_campaign.schedule.requires_end");
    }

    [Fact]
    public async Task A_campaign_with_nothing_to_spend_is_refused()
    {
        using var client = await AdvertiserAsync();

        var response = await client.PostAsJsonAsync(
            $"{Root}/campaigns", NewCampaign() with { BudgetMinorUnits = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("ad_campaign.budget.required");
    }

    [Fact]
    public async Task An_audience_survives_the_round_trip_and_says_what_it_narrows()
    {
        // MON-GAP-007's second half: the register asked for a statement of which dimensions the
        // engine honours, because the builder collected seven and the wire carried one.
        using var client = await AdvertiserAsync();
        var community = Guid.NewGuid();

        var campaign = await ReadDataAsync<CampaignDto>(
            await client.PostAsJsonAsync($"{Root}/campaigns", NewCampaign() with
            {
                Audience = new AudienceDto(
                    ["gb", "NG"], [community], null, ["technology"], null)
            }));

        campaign.Audience.CountryCodes.Should().BeEquivalentTo(["GB", "NG"]);
        campaign.Audience.CommunityIds.Should().BeEquivalentTo([community]);
        campaign.Audience.IsEveryone.Should().BeFalse();

        campaign.Audience.ConstrainedDimensions.Should().BeEquivalentTo(
            ["Country", "Community", "Interest"]);
    }

    [Fact]
    public async Task A_campaign_that_names_no_audience_reaches_everyone()
    {
        using var client = await AdvertiserAsync();

        var campaign = await ReadDataAsync<CampaignDto>(
            await client.PostAsJsonAsync($"{Root}/campaigns", NewCampaign() with { Audience = null }));

        campaign.Audience.IsEveryone.Should().BeTrue();
        campaign.Audience.ConstrainedDimensions.Should().BeEmpty();
    }

    // ── Editing ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_draft_can_be_rewritten_and_a_submitted_one_cannot()
    {
        using var client = await AdvertiserAsync();
        var created = await CreateAsync(client);

        var edited = await ReadDataAsync<CampaignDto>(
            await client.PutAsJsonAsync(
                $"{Root}/campaigns/{created.Id}",
                NewCampaign() with { Name = "Second thoughts" }));

        edited.Name.Should().Be("Second thoughts");

        await client.PostAsJsonAsync($"{Root}/campaigns/{created.Id}/submit", new { });

        var refused = await client.PutAsJsonAsync(
            $"{Root}/campaigns/{created.Id}", NewCampaign() with { Name = "Too late" });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("ad_campaign.not_editable");
    }

    // ── The state machine over HTTP ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_draft_cannot_be_paused()
    {
        using var client = await AdvertiserAsync();
        var created = await CreateAsync(client);

        var response = await client.PostAsync($"{Root}/campaigns/{created.Id}/pause", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ad_campaign.not_pausable");
    }

    [Fact]
    public async Task Cancelling_records_the_whole_budget_as_owed_when_nothing_was_spent()
    {
        using var client = await AdvertiserAsync();
        var created = await CreateAsync(client);

        var cancelled = await ReadDataAsync<CampaignDto>(
            await client.PostAsJsonAsync(
                $"{Root}/campaigns/{created.Id}/cancel", new { reason = "Changed our minds." }));

        cancelled.Status.Should().Be("Cancelled");
        cancelled.Settled!.MinorUnits.Should().Be(5_000);
        cancelled.EndReason.Should().Be("Changed our minds.");
        cancelled.CanCancel.Should().BeFalse();
    }

    [Fact]
    public async Task A_finished_campaign_cannot_be_cancelled_again()
    {
        using var client = await AdvertiserAsync();
        var created = await CreateAsync(client);

        await client.PostAsJsonAsync($"{Root}/campaigns/{created.Id}/cancel", new { reason = "Stop." });

        var again = await client.PostAsJsonAsync(
            $"{Root}/campaigns/{created.Id}/cancel", new { reason = "Again." });

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain("already_finished");
    }

    // ── Funding ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Without this the state machine has an unreachable half.</b> Submission leaves a campaign
    /// in PendingPayment, and if nothing moved it out then every campaign ever submitted would sit
    /// waiting for a payment that had already cleared, and no reviewer would see one.
    /// </summary>
    [Fact]
    public async Task A_captured_purchase_moves_the_campaign_to_review()
    {
        using var client = await AdvertiserAsync();
        var created = await CreateAsync(client);

        var submitted = await ReadDataAsync<SubmissionDto>(
            await client.PostAsJsonAsync($"{Root}/campaigns/{created.Id}/submit", new { }));

        submitted.Campaign.Status.Should().Be("PendingPayment");

        await ConsumePurchaseAsync(created.Id);

        var funded = await ReadDataAsync<CampaignDto>(
            await client.GetAsync($"{Root}/campaigns/{created.Id}"));

        funded.Status.Should().Be("Submitted", "the money arrived, so it is queued for review");
    }

    /// <summary>
    /// <b>A purchase with no campaign passes in silence.</b> Every other monetization product goes
    /// through the same event — a boost, a subscription, a credit bundle — and none has a campaign.
    /// </summary>
    [Fact]
    public async Task A_purchase_with_no_campaign_behind_it_is_ignored()
    {
        var consuming = async () => await ConsumePurchaseAsync(Guid.NewGuid());

        await consuming.Should().NotThrowAsync();
    }

    /// <summary>
    /// <b>A campaign with nothing to charge against cannot be submitted.</b> It would reach review
    /// unpriced, and somebody would have to price it by hand.
    /// </summary>
    [Fact]
    public async Task A_campaign_with_no_package_cannot_be_submitted()
    {
        using var client = await AdvertiserAsync();

        var created = await ReadDataAsync<CampaignDto>(
            await client.PostAsJsonAsync(
                $"{Root}/campaigns", NewCampaign() with { ProductCode = null }));

        var response = await client.PostAsJsonAsync(
            $"{Root}/campaigns/{created.Id}/submit", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("product.required");
    }

    /// <summary>
    /// <b>A budget that does not match the package is refused.</b> Otherwise one figure lands on
    /// the dashboard and a different one on the bank statement.
    /// </summary>
    [Fact]
    public async Task A_budget_that_does_not_match_the_package_is_refused()
    {
        using var client = await AdvertiserAsync();

        var created = await ReadDataAsync<CampaignDto>(
            await client.PostAsJsonAsync(
                $"{Root}/campaigns", NewCampaign() with { BudgetMinorUnits = 1_234 }));

        var response = await client.PostAsJsonAsync(
            $"{Root}/campaigns/{created.Id}/submit", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("budget.mismatch");
    }

    // ── Ownership ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_campaign_somebody_else_owns_answers_absent_rather_than_forbidden()
    {
        // A 403 would confirm to a competitor that a guessed id is real, and the row carries their
        // budget, spend, creative and targeting.
        using var owner = await AdvertiserAsync();
        var created = await CreateAsync(owner);

        using var stranger = await AdvertiserAsync();

        var response = await stranger.GetAsync($"{Root}/campaigns/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_stranger_cannot_cancel_somebody_elses_campaign()
    {
        using var owner = await AdvertiserAsync();
        var created = await CreateAsync(owner);

        using var stranger = await AdvertiserAsync();

        var response = await stranger.PostAsJsonAsync(
            $"{Root}/campaigns/{created.Id}/cancel", new { reason = "Not mine." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_list_holds_only_the_callers_own_campaigns()
    {
        using var owner = await AdvertiserAsync();
        var mine = await CreateAsync(owner);

        using var other = await AdvertiserAsync();
        await CreateAsync(other);

        var listed = await ReadDataAsync<List<CampaignDto>>(
            await owner.GetAsync($"{Root}/campaigns/me"));

        listed.Should().Contain(campaign => campaign.Id == mine.Id);
        listed.Should().OnlyContain(campaign => campaign.Id == mine.Id);
    }

    [Fact]
    public async Task The_list_keeps_terminal_campaigns_and_filters_on_request()
    {
        using var client = await AdvertiserAsync();
        var draft = await CreateAsync(client);
        var cancelled = await CreateAsync(client);

        await client.PostAsJsonAsync(
            $"{Root}/campaigns/{cancelled.Id}/cancel", new { reason = "Stop." });

        var all = await ReadDataAsync<List<CampaignDto>>(
            await client.GetAsync($"{Root}/campaigns/me"));

        all.Should().Contain(campaign => campaign.Id == cancelled.Id,
            "a completed campaign is what an advertiser goes back to to run it again");

        var drafts = await ReadDataAsync<List<CampaignDto>>(
            await client.GetAsync($"{Root}/campaigns/me?status=Draft"));

        drafts.Should().Contain(campaign => campaign.Id == draft.Id);
        drafts.Should().NotContain(campaign => campaign.Id == cancelled.Id);
    }

    [Fact]
    public async Task An_unrecognised_status_filter_is_dropped_rather_than_refused()
    {
        // A newer client naming a state this deployment does not have should get the states it
        // does, not a 400 on the whole dashboard.
        using var client = await AdvertiserAsync();
        await CreateAsync(client);

        var response = await client.GetAsync($"{Root}/campaigns/me?status=Teleporting");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Review ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_review_reads_as_pending_before_anybody_looks()
    {
        // "Nobody has decided yet" is an answer the client renders, and an absent object is not.
        using var client = await AdvertiserAsync();
        var created = await CreateAsync(client);

        var review = await ReadDataAsync<ReviewDto>(
            await client.GetAsync($"{Root}/campaigns/{created.Id}/review"));

        review.Decision.Should().Be("Pending");
        review.CanAppeal.Should().BeFalse();
        review.Note.Should().BeNull();
    }

    [Fact]
    public async Task Nothing_can_be_appealed_before_it_is_rejected()
    {
        using var client = await AdvertiserAsync();
        var created = await CreateAsync(client);

        var response = await client.PostAsJsonAsync(
            $"{Root}/campaigns/{created.Id}/appeal", new { note = "Please look again." });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("nothing_to_appeal");
    }

    [Fact]
    public async Task The_review_queue_is_not_open_to_an_ordinary_advertiser()
    {
        // A queue of everybody's creative, budget and targeting is not something a purchase
        // permission should open.
        using var client = await AdvertiserAsync();

        var response = await client.GetAsync($"{Root}/campaigns/reviews/queue");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Drives the funding consumer directly rather than through the outbox, so the test asserts
    /// the transition rather than the dispatcher's timing.
    /// </summary>
    private async Task ConsumePurchaseAsync(Guid campaignId)
    {
        using var scope = Factory.Services.CreateScope();

        var consumer = ActivatorUtilities.CreateInstance<AdCampaignFundedConsumer>(
            scope.ServiceProvider);

        await consumer.Handle(
            new IntegrationEventNotification<MonetizationProductPurchasedIntegrationEvent>(
                new MonetizationProductPurchasedIntegrationEvent
                {
                    PurchaseId = Guid.NewGuid(),
                    BuyerUserId = Guid.NewGuid(),
                    ProductCode = "advertising.campaign.standard.30d",
                    Category = "Campaign",
                    SubjectType = "Campaign",
                    SubjectId = campaignId,
                    PaidMinorUnits = 5_000,
                    ListMinorUnits = 5_000,
                    Currency = "GBP",
                    PurchasedAt = DateTimeOffset.UtcNow
                }),
            CancellationToken.None);
    }

    private async Task<HttpClient> AdvertiserAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        return client;
    }

    /// <summary>
    /// A draft with a product code, because submission opens a purchase and a campaign with
    /// nothing to charge against cannot be submitted.
    /// </summary>
    private static CreateCampaignDto NewCampaign() => new(
        Name: "Nigerians in London — spring",
        Objective: "Traffic",
        // The Standard tier: £50, which is what advertising.campaign.standard.30d charges. The
        // budget has to equal the tier's price or submission refuses — one number, not two.
        BudgetMinorUnits: 5_000,
        Currency: "GBP",
        Exponent: 2,
        ProductCode: "advertising.campaign.standard.30d",
        StartsAt: DateTimeOffset.UtcNow.AddDays(1),
        EndsAt: DateTimeOffset.UtcNow.AddDays(31),
        Audience: new AudienceDto(["GB"], null, null, null, null),
        Headline: "Find your people in London",
        Body: null);

    private async Task<CampaignDto> CreateAsync(HttpClient client) =>
        await ReadDataAsync<CampaignDto>(
            await client.PostAsJsonAsync($"{Root}/campaigns", NewCampaign()));

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded, and the body says why it did not: {0}", body);

        var envelope = JsonSerializer.Deserialize<EnvelopeDto<T>>(body, JsonOptions);

        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();

        return envelope.Data!;
    }

    // ── Wire shapes, re-declared. See the class summary. ─────────────────────────────────────

    private sealed record EnvelopeDto<T>(T? Data);

    private sealed record CreateCampaignDto(
        string Name,
        string Objective,
        long BudgetMinorUnits,
        string Currency,
        int Exponent,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt,
        AudienceDto? Audience,
        string? Headline,
        string? Body,
        string? ProductCode = null);

    private sealed record AudienceDto(
        List<string>? CountryCodes,
        List<Guid>? CommunityIds,
        List<Guid>? NeighbourhoodIds,
        List<string>? Interests,
        List<string>? Categories);

    private sealed record MoneyDto(long MinorUnits, string Currency, int Exponent);

    private sealed record AudienceResponseDto(
        List<string> CountryCodes,
        List<Guid> CommunityIds,
        List<Guid> NeighbourhoodIds,
        List<string> Interests,
        List<string> Categories,
        bool IsEveryone,
        List<string> ConstrainedDimensions);

    private sealed record ReviewDto(
        string Decision,
        string? Note,
        DateTimeOffset? DecidedAt,
        bool CanAppeal,
        string? AppealNote,
        DateTimeOffset? AppealedAt);

    private sealed record SubmissionDto(
        CampaignDto Campaign,
        string ProductCode,
        string SubjectType,
        Guid SubjectId,
        MoneyDto Amount);

    private sealed record CampaignDto(
        Guid Id,
        string Name,
        string Objective,
        string Status,
        MoneyDto Budget,
        MoneyDto Spend,
        MoneyDto Remaining,
        MoneyDto? Settled,
        AudienceResponseDto Audience,
        ReviewDto Review,
        string? EndReason,
        bool CanEdit,
        bool CanSubmit,
        bool CanPause,
        bool CanResume,
        bool CanCancel);
}
