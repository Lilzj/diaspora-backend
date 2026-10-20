using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Analytics.Application.Projection;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Platform.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Cohort retention, the campaign dataset, the premium summary, boards and insights over real HTTP.
/// <para>
/// <b>Shared database.</b> This class runs in the Finance collection, so every assertion is about rows
/// this test made — its own members, its own job, its own campaign, its own seeded content ids — and
/// never about the first page of anything shared.
/// </para>
/// <para>
/// <b>Every refusal beside every answer.</b> A stranger, a donor, a member without the entitlement and
/// a member asking for the platform are each refused the way the route promises.
/// </para>
/// </summary>
[Collection(FinanceCollection.Name)]
public sealed class AnalyticsRetentionCampaignPremiumTests : PooledIdentityTestBase
{
    private const string Analytics = "/api/v1/analytics";
    private const string Telemetry = "/api/v1/telemetry";
    private const string Communities = "/api/v1/communities";
    private const string Financial = "/api/v1/financial";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AnalyticsRetentionCampaignPremiumTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Campaign dataset
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A campaign's dataset is its completed giving, for whoever runs it.</b> Two gifts arrive (one
    /// anonymous) and one pledge never completes; the organiser reads 50 raised from two gifts by two
    /// supporters, half of a 100 target. A donor, a stranger's id and a malformed id are all absent.
    /// </summary>
    [Fact]
    public async Task A_campaign_dataset_counts_completed_gifts_for_its_organiser_only()
    {
        var organiser = await FinancialMemberAsync();
        var campaign = await PublishedCampaignAsync();

        var donor = await FinancialMemberAsync();
        await DonateAsync(campaign.CauseCode!, 20m);

        await FinancialMemberAsync();
        await DonateAsync(campaign.CauseCode!, 30m, isAnonymous: true);

        await FinancialMemberAsync();
        (await PostDonationAsync(campaign.CauseCode!, 40m)).StatusCode.Should().Be(HttpStatusCode.OK);

        Authorize(organiser.Token);

        var dataset = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/campaigns/{campaign.CampaignId}?period=30d"));

        dataset.Subject.Type.Should().Be("campaign");
        dataset.Subject.Id.Should().Be(campaign.CampaignId.ToString());
        dataset.Subject.Label.Should().Be(campaign.Title);

        var raised = Metric(dataset, "wallet.donations");
        raised.Value.Value.Should().Be(50);
        raised.Format.Should().Be("currency");
        raised.Money!.MinorUnits.Should().Be(5_000);
        raised.Money.Currency.Should().Be("GBP");

        Metric(dataset, "conversion.count").Value.Value.Should().Be(2, "the pledge never completed");
        Metric(dataset, "campaign.supporters").Value.Value.Should().Be(2);
        Metric(dataset, "revenue.refunded").Value.Value.Should().Be(0);
        Metric(dataset, "campaign.raisedToDate").Money!.MinorUnits.Should().Be(5_000);
        Metric(dataset, "campaign.progress").Value.Value.Should().BeApproximately(0.5, 1e-9);

        dataset.UnavailableMetrics.Should().Contain(["content.views", "conversion.rate"]);
        dataset.Freshness.Limitations.Should().NotBeEmpty();

        var series = dataset.Series.Single(entry => entry.Key == "wallet.donations");
        series.Currency.Should().Be("GBP");
        series.Points.Sum(point => point.Value).Should().Be(50);

        var access = await ReadDataAsync<AccessDto>(
            await HttpClient.GetAsync($"{Analytics}/access?subjectType=campaign&subjectId={campaign.CampaignId}"));
        access.CanView.Should().BeTrue();
        access.CanViewFinancial.Should().BeTrue();

        // A donor gave to it and does not run it.
        Authorize(donor.Token);

        (await HttpClient.GetAsync($"{Analytics}/campaigns/{campaign.CampaignId}?period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var refused = await ReadDataAsync<AccessDto>(
            await HttpClient.GetAsync($"{Analytics}/access?subjectType=campaign&subjectId={campaign.CampaignId}"));
        refused.CanView.Should().BeFalse();
        refused.CanViewFinancial.Should().BeFalse();

        (await HttpClient.GetAsync($"{Analytics}/campaigns/{Guid.NewGuid()}?period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.GetAsync($"{Analytics}/campaigns/not-a-campaign?period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Staff may read it.
        await SignInAsRoleAsync("Admin");

        (await HttpClient.GetAsync($"{Analytics}/campaigns/{campaign.CampaignId}?period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A draft has no cause and so has received nothing, and says so rather than showing a zero as giving.</summary>
    [Fact]
    public async Task A_draft_campaign_reads_as_nothing_given_with_the_reason()
    {
        await FinancialMemberAsync();

        var draft = await ReadDataAsync<CampaignDto>(await PostCampaignAsync());

        var dataset = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/campaigns/{draft.CampaignId}?period=7d"));

        Metric(dataset, "wallet.donations").Value.Value.Should().Be(0);
        dataset.Freshness.Limitations.Should().Contain(line => line.Contains("not been published", StringComparison.Ordinal));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Premium summary
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The summary is refused without <c>analytics.advanced</c> and served with it.</b> A member on
    /// no plan is refused before anything is computed; the same member on the Premium trial reads their
    /// own job's two views, the job as their top item, and the audience breakdowns nothing records
    /// listed as unavailable.
    /// </summary>
    [Fact]
    public async Task The_premium_summary_needs_the_entitlement_and_then_counts_the_callers_work()
    {
        var owner = await FinancialMemberAsync();

        var refused = await HttpClient.GetAsync($"{Analytics}/premium/summary?period=30d");
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("analytics.premium.not-entitled");

        var subscribed = await HttpClient.PostAsJsonAsync(
            $"{Financial}/subscriptions", new { planCode = "premium.monthly" }, JsonOptions);
        subscribed.IsSuccessStatusCode.Should().BeTrue(await subscribed.Content.ReadAsStringAsync());

        var jobId = await PublishJobAsync(owner.UserId, "Night porter, city centre");

        await ViewerViewsAsync(jobId, "job", surface: "search");
        await ViewerViewsAsync(jobId, "job", surface: null);

        await Factory.DrainOutboxAsync();
        await ProjectAsync();

        Authorize(owner.Token);

        var summary = await ReadDataAsync<PremiumDto>(
            await HttpClient.GetAsync($"{Analytics}/premium/summary?period=30d"));

        summary.Headline.Single(metric => metric.Key == "views").Value.Should().Be(2);
        summary.Headline.Select(metric => metric.Key)
            .Should().Equal("views", "engagements", "saves", "enquiries", "profileVisits");

        var top = summary.TopSubjects.Should().ContainSingle().Subject;
        top.SubjectId.Should().Be(jobId.ToString());
        top.Title.Should().Be("Night porter, city centre");
        top.Views.Should().Be(2);

        summary.Breakdowns.Single(breakdown => breakdown.Dimension == "contentType")
            .Buckets.Should().ContainSingle().Which.Ratio.Should().Be(1);

        summary.Breakdowns.Single(breakdown => breakdown.Dimension == "source")
            .Buckets.Select(bucket => bucket.Label).Should().BeEquivalentTo(["search", "Not reported"]);

        summary.Series.Should().HaveCount(30);
        summary.Series.Sum(point => point.Views).Should().Be(2);
        summary.Unavailable.Select(part => part.Key).Should().Contain(["country", "audienceType"]);
        summary.ReportingLagHours.Should().BeGreaterThan(0);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Retention
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A cohort is the people first recorded on the caller's work this week.</b> Two people open an
    /// employer's job; the employer's own publication is not audience. The job as an item agrees, a
    /// stranger cannot ask, a member cannot ask for the platform, and staff can.
    /// </summary>
    [Fact]
    public async Task Retention_counts_the_audience_of_the_callers_own_work_and_refuses_everybody_else()
    {
        var employer = await NewMemberAsync();
        var jobId = await PublishJobAsync(employer.UserId, "Kitchen assistant");

        var viewer = await ViewerViewsAsync(jobId, "job", surface: null);
        await ViewerViewsAsync(jobId, "job", surface: null);

        await Factory.DrainOutboxAsync();
        await ProjectAsync();

        Authorize(employer.Token);

        var table = await ReadDataAsync<RetentionDto>(await HttpClient.GetAsync(
            $"{Analytics}/retention?subjectType=employer&cohortGranularity=week&cohortCount=4"));

        table.Granularity.Should().Be("week");
        table.OffsetLabels.Should().HaveCount(7);
        table.Cohorts.Should().HaveCount(4);
        table.UnavailableCohorts.Should().BeEmpty("four weeks is inside the kept history");
        table.Source.Should().Be("backend");
        table.MeasurementWindow.Should().Contain("Monday");

        var latest = table.Cohorts[^1];
        latest.CohortSize.Should().Be(2, "two people opened the job and the employer is not their own audience");
        latest.Values[0].Should().Be(1);
        latest.IsIncomplete.Should().BeTrue();

        var asItem = await ReadDataAsync<RetentionDto>(await HttpClient.GetAsync(
            $"{Analytics}/retention?subjectType=job&subjectId={jobId}&cohortGranularity=day&cohortCount=1"));
        asItem.Cohorts.Should().ContainSingle().Which.CohortSize.Should().Be(2);

        var monthly = await ReadDataAsync<RetentionDto>(await HttpClient.GetAsync(
            $"{Analytics}/retention?subjectType=employer&cohortGranularity=month&cohortCount=6"));
        monthly.UnavailableCohorts.Should().NotBeEmpty("six months reaches past the ninety days kept");
        monthly.UnavailableCohorts.Should().OnlyContain(gap => !string.IsNullOrWhiteSpace(gap.Reason));
        monthly.UnavailableCohorts.Select(gap => gap.CohortDate)
            .Should().OnlyContain(date => string.CompareOrdinal(date, monthly.Cohorts[0].CohortDate) < 0);

        (await HttpClient.GetAsync($"{Analytics}/retention?cohortGranularity=year"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await HttpClient.GetAsync($"{Analytics}/retention?subjectType=employer&cohortCount=25"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var community = await HttpClient.GetAsync($"{Analytics}/retention?subjectType=community&subjectId=anything");
        community.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await community.Content.ReadAsStringAsync()).Should().Contain("analytics.subject.unsupported");

        // The viewer owns neither the job nor the platform.
        Authorize(viewer.Token);

        (await HttpClient.GetAsync($"{Analytics}/retention?subjectType=job&subjectId={jobId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync($"{Analytics}/retention?subjectType=employer&subjectId={employer.UserId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var platform = await HttpClient.GetAsync($"{Analytics}/retention?subjectType=platform");
        platform.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await platform.Content.ReadAsStringAsync()).Should().Contain("analytics.platform.forbidden");

        await SignInAsRoleAsync("Admin");

        var platformTable = await ReadDataAsync<RetentionDto>(
            await HttpClient.GetAsync($"{Analytics}/retention?subjectType=platform&cohortCount=2"));
        platformTable.Cohorts[^1].CohortSize.Should().BeGreaterThanOrEqualTo(2);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Leaderboards and insights
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A board ranks the caller's items by one counted figure, pages, and compares each row with the
    /// period before.</b> The figures are seeded rollup rows for two of the member's own items, so the
    /// ordering and the comparison are exact.
    /// </summary>
    [Fact]
    public async Task A_board_ranks_the_callers_items_pages_and_refuses_what_it_cannot_rank()
    {
        var creator = await NewMemberAsync();
        var (leading, trailing) = await SeedTwoItemsAsync(creator.UserId);

        var board = await ReadDataAsync<BoardDto>(await HttpClient.GetAsync(
            $"{Analytics}/leaderboards/creator-content?period=30d"));

        board.Source.Should().Be("backend");
        board.MetricKey.Should().Be("content.views");
        board.Rows.Select(row => row.Entity.EntityId).Should().Equal(leading.ToString(), trailing.ToString());
        board.Rows.Select(row => row.Position).Should().Equal(1, 2);
        board.Rows[0].Value.Should().Be(60);
        board.Rows[0].Comparison!.BaselineValue.Should().Be(40);
        board.Rows[0].Comparison!.ChangePercent.Should().BeApproximately(0.5, 1e-9);
        board.HasMore.Should().BeFalse();

        var first = await ReadDataAsync<BoardDto>(await HttpClient.GetAsync(
            $"{Analytics}/leaderboards/top-content?subjectType=creator&period=30d&take=1"));
        first.Rows.Should().ContainSingle().Which.Entity.EntityId.Should().Be(leading.ToString());
        first.HasMore.Should().BeTrue();

        var second = await ReadDataAsync<BoardDto>(await HttpClient.GetAsync(
            $"{Analytics}/leaderboards/top-content?subjectType=creator&period=30d&take=1&cursor={first.NextCursor}"));
        second.Rows.Should().ContainSingle().Which.Position.Should().Be(2);
        second.HasMore.Should().BeFalse();

        var byActions = await ReadDataAsync<BoardDto>(await HttpClient.GetAsync(
            $"{Analytics}/leaderboards/creator-content?period=30d&metric=conversion.count"));
        byActions.Rows.Should().ContainSingle().Which.Entity.EntityId.Should().Be(trailing.ToString());

        var unique = await HttpClient.GetAsync(
            $"{Analytics}/leaderboards/creator-content?period=30d&metric=content.uniqueViewers");
        unique.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unique.Content.ReadAsStringAsync()).Should().Contain("analytics.leaderboard.metric-unsupported");

        (await HttpClient.GetAsync($"{Analytics}/leaderboards/most-loved?period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync($"{Analytics}/leaderboards/seller-listings?subjectType=employer&period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await HttpClient.GetAsync($"{Analytics}/leaderboards/top-content?subjectType=job&subjectId={leading}&period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await HttpClient.GetAsync($"{Analytics}/leaderboards/platform-content?period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Another member cannot read this member's board.
        await NewMemberAsync();

        (await HttpClient.GetAsync(
                $"{Analytics}/leaderboards/top-content?subjectType=creator&subjectId={creator.UserId}&period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Staff read the platform board, summed in the database across every owner.
        await SignInAsRoleAsync("Admin");

        var platform = await ReadDataAsync<BoardDto>(await HttpClient.GetAsync(
            $"{Analytics}/leaderboards/platform-content?period=30d&take=50"));
        platform.Rows.Should().Contain(row => row.Entity.EntityId == leading.ToString() && row.Value == 60);
    }

    /// <summary>
    /// <b>Insights are statements of counted figures, and none is made over too few.</b> The seeded
    /// member's views rose from 40 to 65, one item drew 92% of them, and five saves came from 65 views.
    /// A member with two views receives no statement at all.
    /// </summary>
    [Fact]
    public async Task Insights_state_counted_changes_and_say_nothing_over_too_few_views()
    {
        var creator = await NewMemberAsync();
        await SeedTwoItemsAsync(creator.UserId);

        var insights = await ReadDataAsync<List<InsightDto>>(
            await HttpClient.GetAsync($"{Analytics}/insights?subjectType=creator&period=30d"));

        insights.Select(insight => insight.Key).Should().Equal("views-rose", "one-item-leads", "saves-per-view");
        insights[0].Headline.Should().Be("Views rose 63% against the previous 30 days");
        insights[0].Tone.Should().Be("positive");
        insights[1].Headline.Should().Contain("92%");
        insights[2].Headline.Should().Be("About one save for every 13 views");
        insights.Should().OnlyContain(insight => insight.Source == "backend" && !string.IsNullOrWhiteSpace(insight.Detail));

        var employer = await NewMemberAsync();
        var jobId = await PublishJobAsync(employer.UserId, "Warehouse operative");
        await ViewerViewsAsync(jobId, "job", surface: null);
        await Factory.DrainOutboxAsync();
        await ProjectAsync();

        Authorize(employer.Token);

        (await ReadDataAsync<List<InsightDto>>(
                await HttpClient.GetAsync($"{Analytics}/insights?subjectType=job&subjectId={jobId}&period=30d")))
            .Should().BeEmpty("one view is too few to state anything about");

        await NewMemberAsync();

        (await HttpClient.GetAsync($"{Analytics}/insights?subjectType=job&subjectId={jobId}&period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync($"{Analytics}/insights?subjectType=platform&period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static MetricDto Metric(DatasetDto dataset, string key) =>
        dataset.Metrics.Single(metric => metric.Key == key);

    private void Authorize(string token)
    {
        Api.Authenticate(token);
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<Member> NewMemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Authorize(auth.Tokens.AccessToken);

        return new Member(auth.Tokens.AccessToken, auth.User.Id);
    }

    /// <summary>A verified member whose financial identity is active, so they can give and subscribe.</summary>
    private async Task<Member> FinancialMemberAsync()
    {
        var member = await NewMemberAsync();

        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = await accounts.FindByOwnerAsync(member.UserId, FinancialAccountType.Personal)
            ?? await identities.GetOrCreateAsync(member.UserId, FinancialAccountType.Personal, "GBP");

        await identities.ActivateAsync(account);
        await unitOfWork.SaveChangesAsync();

        return member;
    }

    private Task<HttpResponseMessage> PostCampaignAsync() =>
        HttpClient.PostAsJsonAsync(
            $"{Communities}/campaigns",
            new
            {
                title = $"Help Ada travel home {Guid.NewGuid():N}",
                summary = "A short summary of why.",
                story = "The whole story, at length.",
                category = "Repatriation",
                targetAmount = 100m,
                currency = "GBP",
                deadline = DateTimeOffset.UtcNow.AddDays(7),
                beneficiaryType = "Member",
                beneficiaryName = "Ada Nwosu",
                countryCode = "GB",
                acceptedTerms = true
            },
            JsonOptions);

    private async Task<CampaignDto> PublishedCampaignAsync()
    {
        var campaign = await ReadDataAsync<CampaignDto>(await PostCampaignAsync());

        (await HttpClient.PostAsJsonAsync($"{Communities}/campaigns/{campaign.CampaignId}/submit", new { }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        return await ReadDataAsync<CampaignDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Communities}/campaigns/{campaign.CampaignId}/publish", new { }, JsonOptions));
    }

    private Task<HttpResponseMessage> PostDonationAsync(string causeCode, decimal amount, bool isAnonymous = false) =>
        HttpClient.PostAsJsonAsync(
            $"{Financial}/donations",
            new
            {
                amount,
                currency = "GBP",
                causeCode,
                isAnonymous,
                message = isAnonymous ? "Please do not name me." : "Good luck!",
                methodKind = "Card",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

    /// <summary>A donation pledged, authorised and captured through B15's own routes.</summary>
    private async Task DonateAsync(string causeCode, decimal amount, bool isAnonymous = false)
    {
        var response = await PostDonationAsync(causeCode, amount, isAnonymous);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var donation = await ReadDataAsync<DonationDto>(response);
        var intentId = donation.Payment!.PaymentIntentId;

        (await HttpClient.PostAsJsonAsync($"{Financial}/payment-intents/{intentId}/authorize", new { }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await HttpClient.PostAsJsonAsync($"{Financial}/payment-intents/{intentId}/capture", new { }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> PublishJobAsync(Guid employerUserId, string title)
    {
        var jobId = Guid.NewGuid();

        using (var scope = Factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await publisher.PublishAsync(new JobPublishedIntegrationEvent
            {
                JobId = jobId,
                EmployerId = Guid.NewGuid(),
                EmployerUserId = employerUserId,
                EmployerName = "Harbour Care",
                IsEmployerVerified = true,
                Title = title,
                Slug = $"job-{jobId:N}",
                CategoryKey = "hospitality",
                Tags = [],
                SkillKeys = [],
                CountryCode = "GB",
                WorkplaceType = "OnSite",
                JobType = "FullTime",
                ExperienceLevel = "Entry",
                OffersVisaSponsorship = false,
                OffersRelocationAssistance = false,
                IsSalaryDisclosed = false,
                VacancyCount = 1,
                IsFirstPublication = true,
                PublishedAt = DateTimeOffset.UtcNow
            });

            await unitOfWork.SaveChangesAsync();
        }

        await Factory.DrainOutboxAsync();

        return jobId;
    }

    /// <summary>A new member opens the item, reported by the app the way it reports every opening.</summary>
    private async Task<Member> ViewerViewsAsync(Guid contentId, string contentType, string? surface)
    {
        var viewer = await NewMemberAsync();

        object properties = surface is null
            ? new { contentId = contentId.ToString(), contentType }
            : new { contentId = contentId.ToString(), contentType, surface };

        var response = await HttpClient.PostAsJsonAsync(
            $"{Telemetry}/events",
            new
            {
                events = new[]
                {
                    new { @event = "content.viewed", occurredAt = DateTimeOffset.UtcNow, properties }
                }
            },
            JsonOptions);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());

        return viewer;
    }

    private async Task ProjectAsync()
    {
        for (var pass = 0; pass < 10; pass++)
        {
            using var scope = Factory.Services.CreateScope();

            if (await scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>().RunOnceAsync() == 0)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Two items of the member's, as daily rollup rows: the leader with 60 views and 5 saves yesterday
    /// and 40 views forty days ago, the other with 5 views and 2 actions taken yesterday.
    /// </summary>
    private async Task<(Guid Leading, Guid Trailing)> SeedTwoItemsAsync(Guid ownerUserId)
    {
        var leading = Guid.NewGuid();
        var trailing = Guid.NewGuid();

        using var scope = Factory.Services.CreateScope();

        var rollups = scope.ServiceProvider.GetRequiredService<IAnalyticsRollupRepository>();
        var calendar = scope.ServiceProvider.GetRequiredService<AnalyticsReportingCalendar>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var yesterday = calendar.Today.AddDays(-1);

        void Seed(Guid contentId, DateOnly day, long views, long saves, long conversions)
        {
            var row = ContentDailyRollup.Create(
                ContentDailyRollupId.New(), day, AnalyticsContentType.Guide, contentId, ownerUserId);

            row.Replace(
                new AnalyticsCounts(views, saves, 0, 0, 0, 0, conversions, 0, 0, 0, 0),
                views,
                ownerUserId,
                "GB",
                DateTimeOffset.UtcNow);

            rollups.Add(row);
        }

        Seed(leading, yesterday, 60, 5, 0);
        Seed(leading, calendar.Today.AddDays(-40), 40, 0, 0);
        Seed(trailing, yesterday, 5, 0, 2);

        await unitOfWork.SaveChangesAsync();

        return (leading, trailing);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, but answered {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record Member(string Token, Guid UserId);

    private sealed record CampaignDto(Guid CampaignId, string Title, string Status, string? CauseCode);

    private sealed record PaymentDto(Guid PaymentIntentId, string Status);

    private sealed record DonationDto(Guid DonationId, string Status, PaymentDto? Payment);

    private sealed record AccessDto(bool CanView, bool CanViewFinancial);

    private sealed record SubjectDto(string Type, string? Id, string? Label);

    private sealed record SourcedDto(double Value, string Source);

    private sealed record MoneyDto(decimal Amount, string Currency, long MinorUnits, int Exponent);

    private sealed record MetricDto(string Key, string Label, SourcedDto Value, string Format, MoneyDto? Money);

    private sealed record PointDto(string At, double Value);

    private sealed record SeriesDto(string Key, string? Currency, IReadOnlyList<PointDto> Points);

    private sealed record FreshnessDto(IReadOnlyList<string> Limitations);

    private sealed record DatasetDto(
        SubjectDto Subject,
        IReadOnlyList<MetricDto> Metrics,
        IReadOnlyList<SeriesDto> Series,
        IReadOnlyList<string> UnavailableMetrics,
        FreshnessDto Freshness);

    private sealed record CohortDto(string CohortDate, long CohortSize, IReadOnlyList<double?> Values, bool IsIncomplete);

    private sealed record GapDto(string CohortDate, string Reason);

    private sealed record RetentionDto(
        string Granularity,
        IReadOnlyList<string> OffsetLabels,
        string MeasurementWindow,
        IReadOnlyList<CohortDto> Cohorts,
        IReadOnlyList<GapDto> UnavailableCohorts,
        string Source);

    private sealed record EntityDto(string EntityType, string EntityId, string Title);

    private sealed record ComparisonDto(double? BaselineValue, double? ChangePercent);

    private sealed record RowDto(int Position, EntityDto Entity, double Value, ComparisonDto? Comparison);

    private sealed record BoardDto(
        string Key, string MetricKey, IReadOnlyList<RowDto> Rows, string? NextCursor, bool HasMore, string Source);

    private sealed record InsightDto(string Key, string Headline, string? Detail, string Tone, string Source);

    private sealed record PremiumMetricDto(string Key, double Value, double? ChangeRatio);

    private sealed record BucketDto(string Label, double Value, double Ratio);

    private sealed record BreakdownDto(string Dimension, IReadOnlyList<BucketDto> Buckets);

    private sealed record PremiumPointDto(string Date, long Views, long Engagements);

    private sealed record PremiumSubjectDto(string SubjectType, string SubjectId, string Title, long Views);

    private sealed record UnavailablePartDto(string Key, string Reason);

    private sealed record PremiumDto(
        IReadOnlyList<PremiumMetricDto> Headline,
        IReadOnlyList<BreakdownDto> Breakdowns,
        IReadOnlyList<PremiumPointDto> Series,
        IReadOnlyList<PremiumSubjectDto> TopSubjects,
        double ReportingLagHours,
        IReadOnlyList<UnavailablePartDto> Unavailable);
}
