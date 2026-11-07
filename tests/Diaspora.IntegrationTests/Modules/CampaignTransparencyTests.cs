using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Infrastructure.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Everything a campaign test needs, over a host the derived class chooses.
/// </summary>
public abstract class CampaignTestBase<TFactory> : IdentityTestBase<TFactory>
    where TFactory : CustomWebApplicationFactory, new()
{
    protected const string Communities = "/api/v1/communities";
    protected const string CommunityLife = "/api/v1/community-life";
    protected const string Financial = "/api/v1/financial";

    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected CampaignTestBase(TFactory factory) : base(factory)
    {
    }

    protected void Authorize(string accessToken) =>
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    protected async Task<Guid> MemberAsync() => (await SignedInMemberAsync()).UserId;

    /// <summary>
    /// A verified member with an active financial identity, signed in, with the token kept so a test
    /// that signs somebody else in can come back to them.
    /// </summary>
    protected async Task<(Guid UserId, string Token)> SignedInMemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        ActAs(auth.Tokens.AccessToken);

        await Factory.DrainOutboxAsync();
        await ActivateFinancialIdentityAsync(auth.User.Id);

        return (auth.User.Id, auth.Tokens.AccessToken);
    }

    protected void ActAs(string accessToken)
    {
        Api.Authenticate(accessToken);
        Authorize(accessToken);
    }

    /// <summary>The staff route onto a live fundraiser: suspend or restore, with a reason.</summary>
    protected Task<HttpResponseMessage> ModerateAsync(Guid campaignId, string action, string reason) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/giving/campaigns/{campaignId}/moderation", UriKind.Relative),
            new { action, reason });

    protected Task<HttpResponseMessage> EditAsync(Guid campaignId, string title) =>
        HttpClient.PutAsJsonAsync(
            new Uri($"{Communities}/campaigns/{campaignId}", UriKind.Relative),
            new { title });

    protected static async Task<string?> ReviewNoteAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.GetProperty("data").TryGetProperty("reviewNote", out var note)
            && note.ValueKind == JsonValueKind.String
                ? note.GetString()
                : null;
    }

    protected async Task ActivateFinancialIdentityAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = await accounts.FindByOwnerAsync(userId, FinancialAccountType.Personal)
            ?? await identities.GetOrCreateAsync(userId, FinancialAccountType.Personal, "GBP");

        await identities.ActivateAsync(account);
        await unitOfWork.SaveChangesAsync();
    }

    protected Task<HttpResponseMessage> PostCampaignAsync(
        bool acceptedTerms = true,
        string? idempotencyKey = null,
        Guid? organizationId = null,
        string title = "Help Ada travel home") =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Communities}/campaigns", UriKind.Relative),
            new
            {
                title,
                summary = "A short summary of why.",
                story = "The whole story, at length.",
                category = "Repatriation",
                targetAmount = 100m,
                currency = "GBP",

                // A real deadline, a week out. The aggregate refuses one in the past, so a test that
                // wants an expired campaign sweeps forward rather than back-dating the row.
                deadline = DateTimeOffset.UtcNow.AddDays(7),
                beneficiaryType = "Member",
                beneficiaryName = "Ada Nwosu",
                organizationId,
                countryCode = "GB",
                acceptedTerms,
                idempotencyKey
            });

    /// <summary>
    /// One pass of the campaign lifecycle sweep, as at a month from now.
    /// <para>
    /// The moment is passed in rather than a clock being faked host-wide — a mutable clock would move
    /// token expiry and every other deadline in the host with it.
    /// </para>
    /// </summary>
    protected Task<int> RunLifecycleSweepAsync() =>
        Factory.Services
            .GetServices<IHostedService>()
            .OfType<GivingLifecycleSweep>()
            .Single()
            .RunOnceAsync(DateTimeOffset.UtcNow.AddDays(30));

    protected async Task<CampaignShape> CreateCampaignAsync(string title = "Help Ada travel home") =>
        await ReadDataAsync<CampaignShape>(await PostCampaignAsync(title: title));

    protected async Task<CampaignShape> PublishedCampaignAsync()
    {
        var campaign = await CreateCampaignAsync();

        (await TransitionAsync(campaign.CampaignId, "submit")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        return await ReadDataAsync<CampaignShape>(
            await TransitionAsync(campaign.CampaignId, "publish"));
    }

    protected Task<HttpResponseMessage> TransitionAsync(Guid campaignId, string verb) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Communities}/campaigns/{campaignId}/{verb}", UriKind.Relative),
            new { });

    protected async Task<CampaignShape> GetCampaignAsync(Guid campaignId) =>
        await ReadDataAsync<CampaignShape>(
            await HttpClient.GetAsync(
                new Uri($"{Communities}/campaigns/{campaignId}", UriKind.Relative)));

    protected Task<HttpResponseMessage> PostDonationAsync(
        string causeCode, decimal amount, bool isAnonymous = false, int? recurring = null) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/donations", UriKind.Relative),
            new
            {
                amount,
                currency = "GBP",
                causeCode,
                isAnonymous,
                message = isAnonymous ? "Please do not name me." : "Good luck!",
                methodKind = "Card",
                idempotencyKey = Guid.NewGuid().ToString("N"),
                recurringIntervalMonths = recurring
            });

    /// <summary>
    /// A donation that actually completes: pledged, authorised and captured through B15's own routes.
    /// <para>
    /// The capture is what moves the cause's total, so a test that stopped at the pledge would be
    /// asserting on money that had not arrived.
    /// </para>
    /// </summary>
    protected async Task<DonationShape> DonateAsync(
        string causeCode, decimal amount, bool isAnonymous = false)
    {
        var response = await PostDonationAsync(causeCode, amount, isAnonymous);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var donation = await ReadDataAsync<DonationShape>(response);

        donation.Payment.Should().NotBeNull("a donation opens the payment for itself");

        var intentId = donation.Payment!.PaymentIntentId;

        (await HttpClient.PostAsJsonAsync(
                new Uri($"{Financial}/payment-intents/{intentId}/authorize", UriKind.Relative),
                new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.PostAsJsonAsync(
                new Uri($"{Financial}/payment-intents/{intentId}/capture", UriKind.Relative),
                new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        return donation;
    }

    protected static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    protected sealed record MoneyShape(decimal Amount, string Currency, long MinorUnits);

    protected sealed record ProgressShape(
        MoneyShape? Target, MoneyShape Raised, int? Percentage, int SupporterCount, int? DaysRemaining);

    protected sealed record CampaignShape(
        Guid CampaignId,
        string Title,
        string Status,
        string Category,
        string? CauseCode,
        Guid? OrganizationId,
        ProgressShape Progress,
        bool CanManage,
        bool CanDonate,
        List<string> AvailableTransitions);

    protected sealed record DonationShape(
        Guid DonationId, string Status, PaymentShape? Payment);

    protected sealed record PaymentShape(Guid PaymentIntentId, string Status);

    protected sealed record UpdateShape(
        Guid UpdateId,
        Guid CampaignId,
        string Title,
        string Body,
        List<string> MediaReferences,
        DateTimeOffset PublishedAt,
        string AuthorName,
        bool IsWithdrawn);

    protected sealed record MilestoneShape(
        Guid MilestoneId, string Label, MoneyShape Target, bool IsReached, DateTimeOffset? ReachedAt);

    protected sealed record SupporterShape(
        Guid? UserId,
        string? DisplayName,
        bool IsAnonymous,
        MoneyShape? Amount,
        string? Message,
        DateTimeOffset GivenAt);

    protected sealed record FundUsageLineShape(
        Guid Id,
        string Label,
        MoneyShape Allocated,
        MoneyShape? Spent,
        string? Note,
        bool IsPlatformVerified);

    protected sealed record FundUsageShape(
        Guid CampaignId,
        string? CauseCode,
        MoneyShape Raised,
        MoneyShape Allocated,
        MoneyShape Disbursed,
        MoneyShape DisbursedVerified,
        MoneyShape Remaining,
        List<FundUsageLineShape> Lines,
        bool IsOverReported);

    protected sealed record TimeSeriesPointShape(
        DateTimeOffset PeriodStart, MoneyShape Amount, int DonationCount);

    protected sealed record TimeSeriesShape(
        string CauseCode,
        string Currency,
        DateTimeOffset From,
        DateTimeOffset To,
        string TimeZone,
        string Granularity,
        List<TimeSeriesPointShape> Points,
        MoneyShape Total,
        MoneyShape Refunded,
        int DonationCount,
        int RefundedCount,
        int FailedCount,
        int SupporterCount);

    protected sealed record PageShape<T>(List<T> Items, string? NextCursor, bool HasMore);
}

/// <summary>
/// COMMUNITY-LIFE-GAP-007: updates, milestones, supporters, fund usage and the giving time series.
/// <para>
/// <b>The privacy test is the one that matters most.</b> The register asks for anonymity at the
/// source — "sending an identity with a 'please hide this' flag would mean the identity had already
/// reached the device" — and this file asserts the response body itself, not a rendering of it.
/// </para>
/// </summary>
public sealed class CampaignTransparencyTests : CampaignTestBase<CustomWebApplicationFactory>
{
    public CampaignTransparencyTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ── Updates ──────────────────────────────────────────────────────────────────────────────

    /// <summary>An organizer publishes to the people supporting them, newest first.</summary>
    [Fact]
    public async Task An_organizer_can_publish_updates()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await PublishUpdateAsync(campaign.CampaignId, "We booked the flight");
        await PublishUpdateAsync(campaign.CampaignId, "Thank you");

        var updates = await ListUpdatesAsync(campaign.CampaignId);

        updates.Items.Should().HaveCount(2);
        updates.Items[0].Title.Should().Be("Thank you", "newest first");
        updates.Items.Should().OnlyContain(update => update.IsWithdrawn == false);
    }

    /// <summary>An unpublished campaign has no supporters to update.</summary>
    [Fact]
    public async Task A_draft_campaign_cannot_publish_an_update()
    {
        await MemberAsync();

        var campaign = await CreateCampaignAsync();

        var response = await PostUpdateAsync(campaign.CampaignId, "Too early");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>Somebody else's campaign is not yours to post to.</summary>
    [Fact]
    public async Task A_stranger_cannot_publish_an_update()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await MemberAsync();

        (await PostUpdateAsync(campaign.CampaignId, "Mine now")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A retried publication makes one update.</summary>
    [Fact]
    public async Task Publishing_an_update_twice_with_one_key_makes_one_update()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();
        var key = Guid.NewGuid().ToString("N");

        var first = await ReadDataAsync<UpdateShape>(
            await PostUpdateAsync(campaign.CampaignId, "We booked the flight", key));

        var second = await ReadDataAsync<UpdateShape>(
            await PostUpdateAsync(campaign.CampaignId, "We booked the flight", key));

        second.UpdateId.Should().Be(first.UpdateId);
        (await ListUpdatesAsync(campaign.CampaignId)).Items.Should().HaveCount(1);
    }

    // ── Milestones ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A milestone is reached because the money arrived, and the server says so.</b>
    /// <para>
    /// The client is never given the two numbers to divide. <c>isReached</c> is a stamp taken from
    /// the cause's own authoritative total at the moment a donation settled.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_milestone_is_reached_by_a_real_donation()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await SetMilestonesAsync(campaign.CampaignId, ("Halfway", 50m), ("All of it", 100m));

        var before = await ListMilestonesAsync(campaign.CampaignId);

        before.Should().HaveCount(2);
        before.Should().OnlyContain(milestone => milestone.IsReached == false);

        await DonateAsync(campaign.CauseCode!, 60m);

        var after = await ListMilestonesAsync(campaign.CampaignId);

        after.Single(milestone => milestone.Label == "Halfway").IsReached.Should().BeTrue();
        after.Single(milestone => milestone.Label == "Halfway").ReachedAt.Should().NotBeNull();
        after.Single(milestone => milestone.Label == "All of it").IsReached.Should().BeFalse();
    }

    /// <summary>
    /// A milestone declared below a total the cause already passed is reached immediately.
    /// <para>
    /// Otherwise it would sit unreached until the next donation — or for ever, if there is not one.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_milestone_declared_below_the_current_total_is_reached_at_once()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await DonateAsync(campaign.CauseCode!, 60m);
        await SetMilestonesAsync(campaign.CampaignId, ("Halfway", 50m));

        (await ListMilestonesAsync(campaign.CampaignId)).Single().IsReached.Should().BeTrue();
    }

    /// <summary>Declaring milestones is the organizer's, and reached-ness is never the caller's.</summary>
    [Fact]
    public async Task A_stranger_cannot_declare_milestones()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await MemberAsync();

        (await HttpClient.PutAsJsonAsync(
                new Uri($"{Communities}/campaigns/{campaign.CampaignId}/milestones", UriKind.Relative),
                new { milestones = new[] { new { label = "Mine", targetAmount = 1m } } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Supporters and the privacy rule ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>An anonymous supporter carries no identifier at all — not one behind a flag.</b>
    /// <para>
    /// The register's own words: sending an identity with a "please hide this" flag would mean the
    /// identity had already reached the device. This asserts on the response body, and it asserts on
    /// the message too, because free text somebody wrote is one of the commonest ways a name travels.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_anonymous_supporter_arrives_with_no_identity()
    {
        var organizer = await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        var donor = await MemberAsync();

        await DonateAsync(campaign.CauseCode!, 20m, isAnonymous: true);

        var supporters = await ListSupportersAsync(campaign.CampaignId);

        supporters.Items.Should().HaveCount(1);

        var supporter = supporters.Items[0];

        supporter.IsAnonymous.Should().BeTrue();
        supporter.UserId.Should().BeNull("B15 stored no donor identifier at all");
        supporter.DisplayName.Should().Be("Anonymous");
        supporter.Message.Should().BeNull("free text is where a name travels");
        supporter.Amount!.MinorUnits.Should().Be(2_000);

        // Belt and braces: the donor's identifier appears nowhere in the raw body either.
        var raw = await HttpClient.GetStringAsync(
            new Uri($"{Communities}/campaigns/{campaign.CampaignId}/supporters", UriKind.Relative));

        raw.Should().NotContain(donor.ToString());
        raw.Should().NotContain(organizer.ToString());
    }

    /// <summary>A named supporter is named, and their identifier is the client's handle to them.</summary>
    [Fact]
    public async Task A_named_supporter_carries_their_identifier()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        var donor = await MemberAsync();

        await DonateAsync(campaign.CauseCode!, 20m);

        var supporters = await ListSupportersAsync(campaign.CampaignId);

        supporters.Items.Should().HaveCount(1);
        supporters.Items[0].IsAnonymous.Should().BeFalse();
        supporters.Items[0].UserId.Should().Be(donor);
        supporters.Items[0].Message.Should().Be("Good luck!");
    }

    /// <summary>
    /// A pledge that never captured is not a supporter.
    /// <para>
    /// Listing money that has not arrived would publish a figure that can still fail, which is the
    /// whole objection to a client accumulating its own totals.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_pending_donation_is_not_a_supporter()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await MemberAsync();

        // Pledged and never captured.
        (await PostDonationAsync(campaign.CauseCode!, 20m)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await ListSupportersAsync(campaign.CampaignId)).Items.Should().BeEmpty();
    }

    /// <summary>Every supporter is served exactly once across a page boundary they all share.</summary>
    [Fact]
    public async Task Paging_supporters_serves_each_exactly_once()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        for (var index = 0; index < 5; index++)
        {
            await MemberAsync();
            await DonateAsync(campaign.CauseCode!, 10m + index);
        }

        var seen = new List<DateTimeOffset>();
        var amounts = new List<long>();
        string? cursor = null;

        for (var page = 0; page < 10; page++)
        {
            var result = await ListSupportersAsync(
                campaign.CampaignId, cursor is null ? "take=2" : $"take=2&cursor={cursor}");

            seen.AddRange(result.Items.Select(item => item.GivenAt));
            amounts.AddRange(result.Items.Select(item => item.Amount!.MinorUnits));

            if (!result.HasMore)
            {
                break;
            }

            cursor = result.NextCursor;
            cursor.Should().NotBeNull();
        }

        amounts.Should().HaveCount(5, "every supporter is served, and none twice");
        amounts.Should().OnlyHaveUniqueItems();
        seen.Should().HaveCount(5);
    }

    // ── Fund usage ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Four figures, four authorities, and the response says which is which.</b>
    /// <para>
    /// <c>raised</c> is B15's. <c>allocated</c> is what the organizer says they will do.
    /// <c>disbursed</c> is what they report having done, and only the part a settled payout backs is
    /// marked verified.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Fund_usage_separates_what_the_platform_knows_from_what_was_declared()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await DonateAsync(campaign.CauseCode!, 80m);

        await SetFundUsageAsync(
            campaign.CampaignId,
            ("Flights", 50m, 45m),
            ("Documents", 30m, null));

        var usage = await GetFundUsageAsync(campaign.CampaignId);

        usage.Raised.MinorUnits.Should().Be(8_000, "the cause's own figure");
        usage.Allocated.MinorUnits.Should().Be(8_000);
        usage.Disbursed.MinorUnits.Should().Be(4_500);
        usage.DisbursedVerified.MinorUnits.Should().Be(0, "no payout backs it");
        usage.Remaining.MinorUnits.Should().Be(3_500);
        usage.IsOverReported.Should().BeFalse();
        usage.Lines.Should().OnlyContain(line => line.IsPlatformVerified == false);
    }

    /// <summary>
    /// Reporting more spent than was ever raised is surfaced rather than hidden.
    /// <para>
    /// The figures are the organizer's and the platform will not quietly adjust them — but a
    /// supporter reading the page is entitled to know they do not add up. <c>remaining</c> is clamped
    /// at zero rather than published as a negative amount of money.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Reporting_more_spent_than_raised_is_flagged()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await DonateAsync(campaign.CauseCode!, 10m);

        await SetFundUsageAsync(campaign.CampaignId, ("Flights", 50m, 40m));

        var usage = await GetFundUsageAsync(campaign.CampaignId);

        usage.IsOverReported.Should().BeTrue();
        usage.Remaining.MinorUnits.Should().Be(0, "a negative remaining is not a fact about money");
    }

    /// <summary>A payout reference the caller does not own is refused, not recorded as verification.</summary>
    [Fact]
    public async Task A_payout_reference_that_is_not_the_callers_is_refused()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        var response = await HttpClient.PutAsJsonAsync(
            new Uri($"{Communities}/campaigns/{campaign.CampaignId}/fund-usage", UriKind.Relative),
            new
            {
                lines = new[]
                {
                    new
                    {
                        label = "Flights",
                        allocated = 50m,
                        spent = 45m,
                        withdrawalId = Guid.NewGuid()
                    }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── The time series ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The series is the server's arithmetic, bucketed by UTC day, with no gaps.</b>
    /// <para>
    /// Every day in the window is present including the empty ones — a series that omitted them would
    /// let a client draw a line from Monday to Friday and call the middle of it Wednesday's giving.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_time_series_totals_real_donations_and_fills_every_day()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await DonateAsync(campaign.CauseCode!, 30m);
        await DonateAsync(campaign.CauseCode!, 20m);

        var series = await TimeSeriesAsync(campaign.CauseCode!);

        series.Currency.Should().Be("GBP");
        series.TimeZone.Should().Be("UTC");
        series.Granularity.Should().Be("Day");
        series.Total.MinorUnits.Should().Be(5_000);
        series.DonationCount.Should().Be(2);
        series.RefundedCount.Should().Be(0);
        series.SupporterCount.Should().Be(1, "one donor gave twice");

        series.Points.Should().HaveCount(30, "a thirty-day window has thirty days in it");
        series.Points.Sum(point => point.Amount.MinorUnits).Should().Be(5_000);
        series.Points.Count(point => point.Amount.MinorUnits > 0).Should().Be(1);
    }

    /// <summary>A cause with nothing given to it answers a full window of zeroes, not an error.</summary>
    [Fact]
    public async Task The_time_series_of_an_empty_cause_is_a_window_of_zeroes()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        var series = await TimeSeriesAsync(campaign.CauseCode!);

        series.Total.MinorUnits.Should().Be(0);
        series.DonationCount.Should().Be(0);
        series.Points.Should().HaveCount(30);
        series.Points.Should().OnlyContain(point => point.Amount.MinorUnits == 0);
    }

    /// <summary>A cause nobody has is a 404, not an empty chart.</summary>
    [Fact]
    public async Task The_time_series_of_an_unknown_cause_is_not_found()
    {
        await MemberAsync();

        (await HttpClient.GetAsync(
                new Uri(
                    $"{Financial}/donations/causes/no-such-cause/timeseries", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── The deadline sweep ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A campaign past its deadline stops collecting, whichever collecting state it was in.</b>
    /// <para>
    /// Only <c>Active</c> reaches <c>DeadlineReached</c> directly, and the sweep's input is wider
    /// than that — a campaign that was published and never separately marked active is the ordinary
    /// case, because publication is what the organizer does and nothing moves it on afterwards. The
    /// first version of the sweep tried the single move, the table refused it, and the row was
    /// skipped: the campaign kept collecting past the date it promised to stop, and nothing said so.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_published_campaign_past_its_deadline_stops_collecting()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        campaign.Status.Should().Be("Published", "publication is where an organizer leaves it");
        campaign.CanDonate.Should().BeTrue();

        await RunLifecycleSweepAsync();

        var ended = await GetCampaignAsync(campaign.CampaignId);

        ended.Status.Should().Be("DeadlineReached");
        ended.CanDonate.Should().BeFalse();

        (await PostDonationAsync(campaign.CauseCode!, 10m)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "the cause was closed with the campaign");
    }

    /// <summary>A campaign with no deadline is left alone however long it runs.</summary>
    [Fact]
    public async Task A_campaign_with_no_deadline_is_never_swept()
    {
        await MemberAsync();

        var campaign = await ReadDataAsync<CampaignShape>(
            await HttpClient.PostAsJsonAsync(
                new Uri($"{Communities}/campaigns", UriKind.Relative),
                new
                {
                    title = "An open-ended appeal",
                    summary = "A short summary of why.",
                    story = "The whole story, at length.",
                    category = "Community Project",
                    targetAmount = 100m,
                    currency = "GBP",
                    beneficiaryType = "Community",
                    beneficiaryName = "The hall committee",
                    acceptedTerms = true
                }));

        await TransitionAsync(campaign.CampaignId, "submit");
        await TransitionAsync(campaign.CampaignId, "publish");

        await RunLifecycleSweepAsync();

        (await GetCampaignAsync(campaign.CampaignId)).Status.Should().Be("Published");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> PostUpdateAsync(
        Guid campaignId, string title, string? idempotencyKey = null) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Communities}/campaigns/{campaignId}/updates", UriKind.Relative),
            new
            {
                title,
                body = "What happened this week, at some length.",
                mediaReferences = new[] { "media:abc" },
                idempotencyKey
            });

    private async Task PublishUpdateAsync(Guid campaignId, string title) =>
        (await PostUpdateAsync(campaignId, title)).StatusCode.Should().Be(HttpStatusCode.OK);

    private async Task<PageShape<UpdateShape>> ListUpdatesAsync(Guid campaignId) =>
        await ReadDataAsync<PageShape<UpdateShape>>(
            await HttpClient.GetAsync(
                new Uri($"{Communities}/campaigns/{campaignId}/updates", UriKind.Relative)));

    private async Task SetMilestonesAsync(
        Guid campaignId, params (string Label, decimal Target)[] milestones) =>
        (await HttpClient.PutAsJsonAsync(
                new Uri($"{Communities}/campaigns/{campaignId}/milestones", UriKind.Relative),
                new
                {
                    milestones = milestones
                        .Select(milestone => new
                        {
                            label = milestone.Label,
                            targetAmount = milestone.Target
                        })
                        .ToArray()
                }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    private async Task<List<MilestoneShape>> ListMilestonesAsync(Guid campaignId) =>
        await ReadDataAsync<List<MilestoneShape>>(
            await HttpClient.GetAsync(
                new Uri($"{Communities}/campaigns/{campaignId}/milestones", UriKind.Relative)));

    private async Task<PageShape<SupporterShape>> ListSupportersAsync(
        Guid campaignId, string query = "") =>
        await ReadDataAsync<PageShape<SupporterShape>>(
            await HttpClient.GetAsync(
                new Uri(
                    $"{Communities}/campaigns/{campaignId}/supporters?{query}", UriKind.Relative)));

    private async Task SetFundUsageAsync(
        Guid campaignId, params (string Label, decimal Allocated, decimal? Spent)[] lines) =>
        (await HttpClient.PutAsJsonAsync(
                new Uri($"{Communities}/campaigns/{campaignId}/fund-usage", UriKind.Relative),
                new
                {
                    lines = lines
                        .Select(line => new
                        {
                            label = line.Label,
                            allocated = line.Allocated,
                            spent = line.Spent
                        })
                        .ToArray()
                }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    private async Task<FundUsageShape> GetFundUsageAsync(Guid campaignId) =>
        await ReadDataAsync<FundUsageShape>(
            await HttpClient.GetAsync(
                new Uri($"{Communities}/campaigns/{campaignId}/fund-usage", UriKind.Relative)));

    private async Task<TimeSeriesShape> TimeSeriesAsync(string causeCode) =>
        await ReadDataAsync<TimeSeriesShape>(
            await HttpClient.GetAsync(
                new Uri(
                    $"{Financial}/donations/causes/{causeCode}/timeseries", UriKind.Relative)));
}

/// <summary>
/// The review gate, against a host that leaves it on.
/// <para>
/// The default host turns it off so everything downstream of publication can be tested at all; this
/// is what stops that convenience becoming a switch nobody checks.
/// </para>
/// </summary>
public sealed class CampaignReviewGateTests : CampaignTestBase<CampaignReviewWebApplicationFactory>
{
    public CampaignReviewGateTests(CampaignReviewWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// <b>An organizer submits and waits.</b> Publishing their own submission would make the review a
    /// formality it costs a round trip to skip — and a campaign is a request for strangers' money in
    /// somebody's name.
    /// </summary>
    [Fact]
    public async Task An_organizer_cannot_publish_their_own_campaign_when_review_is_required()
    {
        await MemberAsync();

        var campaign = await CreateCampaignAsync();

        (await TransitionAsync(campaign.CampaignId, "submit")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await TransitionAsync(campaign.CampaignId, "publish")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        var pending = await GetCampaignAsync(campaign.CampaignId);

        pending.Status.Should().Be("PendingReview");
        pending.CauseCode.Should().BeNull("nothing was published, so no cause was created");
        pending.CanDonate.Should().BeFalse();
    }

    /// <summary>Neither can they reject it, which is the other half of the same decision.</summary>
    [Fact]
    public async Task An_organizer_cannot_reject_their_own_campaign_when_review_is_required()
    {
        await MemberAsync();

        var campaign = await CreateCampaignAsync();

        await TransitionAsync(campaign.CampaignId, "submit");

        (await HttpClient.PostAsJsonAsync(
                new Uri(
                    $"{Communities}/campaigns/{campaign.CampaignId}/cancel", UriKind.Relative),
                new { reason = "Changed my mind" }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "cancelling their own campaign is theirs");
    }

    /// <summary>
    /// <b>The review decision is the one thing staff still do through the transition handler</b> —
    /// and it is exactly two moves on a submitted campaign. Once published, the campaign is its
    /// organiser's: a moderator who could pause or cancel it here would be acting in their name.
    /// </summary>
    [Fact]
    public async Task A_moderator_publishes_a_submitted_campaign_and_can_do_nothing_else_to_it()
    {
        var (_, organiser) = await SignedInMemberAsync();

        var campaign = await CreateCampaignAsync();

        (await TransitionAsync(campaign.CampaignId, "submit")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");

        var published = await TransitionAsync(campaign.CampaignId, "publish");

        published.StatusCode.Should().Be(HttpStatusCode.OK);

        var decided = await ReadDataAsync<CampaignShape>(published);

        decided.Status.Should().Be("Published");
        decided.CauseCode.Should().NotBeNull("publishing creates the cause, whoever decides it");
        decided.CanManage.Should().BeFalse("the reviewer is not the organiser");
        decided.AvailableTransitions.Should().BeEmpty();

        foreach (var verb in new[] { "pause", "close", "cancel" })
        {
            (await TransitionAsync(campaign.CampaignId, verb)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, "a reviewer cannot {0} a member's campaign", verb);
        }

        ActAs(organiser);

        (await TransitionAsync(campaign.CampaignId, "pause")).StatusCode
            .Should().Be(HttpStatusCode.OK, "running it is the organiser's");
    }

    /// <summary>A reviewer does not reach a draft, which nobody has submitted for a decision.</summary>
    [Fact]
    public async Task A_moderator_cannot_publish_a_draft_nobody_submitted()
    {
        await MemberAsync();

        var campaign = await CreateCampaignAsync();

        await SignInAsRoleAsync("Moderator");

        (await TransitionAsync(campaign.CampaignId, "publish")).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>A moderator who runs a campaign is its organiser, and waits for somebody else.</summary>
    [Fact]
    public async Task A_moderator_cannot_publish_their_own_campaign_when_review_is_required()
    {
        await SignInAsRoleAsync("Moderator");

        var campaign = await CreateCampaignAsync();

        (await TransitionAsync(campaign.CampaignId, "submit")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await TransitionAsync(campaign.CampaignId, "publish")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await GetCampaignAsync(campaign.CampaignId)).AvailableTransitions
            .Should().NotContain("Published");
    }
}

/// <summary>
/// Two identical campaign creations arriving together.
/// <para>
/// Its own host, because the shared single SQLite connection cannot serve two requests at once.
/// </para>
/// </summary>
public sealed class CampaignConcurrencyTests : CampaignTestBase<ConcurrentWebApplicationFactory>
{
    public CampaignConcurrencyTests(ConcurrentWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// One idempotency key is one campaign, however many callers send it at once.
    /// <para>
    /// The unique index decides it, not the lookup before the write — the window between the two is
    /// exactly where a double-tapped submit button lands.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Two_simultaneous_creations_with_one_key_make_one_campaign()
    {
        await MemberAsync();

        var key = Guid.NewGuid().ToString("N");

        var attempts = await Task.WhenAll(
            PostCampaignAsync(idempotencyKey: key),
            PostCampaignAsync(idempotencyKey: key));

        attempts.Should().Contain(response => response.IsSuccessStatusCode);

        var identifiers = new List<Guid>();

        foreach (var attempt in attempts.Where(response => response.IsSuccessStatusCode))
        {
            identifiers.Add((await ReadDataAsync<CampaignShape>(attempt)).CampaignId);
        }

        identifiers.Distinct().Should().HaveCount(1, "one key is one campaign");
    }

    /// <summary>
    /// Two simultaneous lifecycle moves leave the campaign in exactly one of them.
    /// <para>
    /// <c>Revision</c> is the concurrency token, so the loser's write is refused rather than
    /// overwriting the winner's — and the campaign never ends up in a state neither caller asked for.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Two_simultaneous_transitions_leave_one_consistent_state()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await Task.WhenAll(
            TransitionAsync(campaign.CampaignId, "pause"),
            TransitionAsync(campaign.CampaignId, "close"));

        var settled = await GetCampaignAsync(campaign.CampaignId);

        settled.Status.Should().BeOneOf("Paused", "Closed");
    }
}
