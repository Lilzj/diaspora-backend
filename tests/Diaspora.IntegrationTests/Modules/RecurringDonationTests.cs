using System.Net;
using System.Net.Http.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Infrastructure.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// COMMUNITY-LIFE-GAP-008: <c>recurringIntervalMonths</c> becomes a schedule, and the schedule
/// charges.
/// <para>
/// <b>The worker is driven by hand, never by a timer.</b> Every sweep on this platform exposes a
/// public <c>RunOnceAsync</c> for the reason B15 recorded when it wrote the first one: a test that
/// asserts on a side effect after a timer happens to have fired is a flaky test wearing a passing
/// badge.
/// </para>
/// <para>
/// <b>Nothing here marks a donation paid.</b> The worker builds a donation and a payment intent
/// through the ordinary path and the ordinary gateway completes them — which is what makes the
/// assertion "the cause's total went up" mean the money moved rather than that a column was written.
/// </para>
/// </summary>
public sealed class RecurringDonationTests : CampaignTestBase<CustomWebApplicationFactory>
{
    public RecurringDonationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ── The declaration becomes a schedule ───────────────────────────────────────────────────

    /// <summary>
    /// <b>The gap, closed.</b> The field the client has always sent now produces a schedule with a
    /// real next-charge date.
    /// </summary>
    [Fact]
    public async Task A_recurring_donation_creates_a_schedule()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await PostDonationAsync(campaign.CauseCode!, 10m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var schedules = await MyRecurringAsync();

        schedules.Should().HaveCount(1);
        schedules[0].CauseCode.Should().Be(campaign.CauseCode);
        schedules[0].Amount.MinorUnits.Should().Be(1_000);
        schedules[0].IntervalMonths.Should().Be(1);
        schedules[0].Status.Should().Be("Active");
        schedules[0].NextChargeAt.Should().NotBeNull();
        schedules[0].ChargeCount.Should().Be(0);
    }

    /// <summary>A one-off donation creates no schedule at all.</summary>
    [Fact]
    public async Task A_one_off_donation_creates_no_schedule()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await DonateAsync(campaign.CauseCode!, 10m);

        (await MyRecurringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// An anonymous donation cannot recur, and is refused rather than silently made one-off.
    /// <para>
    /// B15 stores no donor identifier on an anonymous gift, so there would be nobody to charge next
    /// month and nobody to tell when it failed. Quietly dropping the interval is how a member ends up
    /// believing they set up a monthly gift that does not exist.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_anonymous_recurring_donation_is_refused()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        var response = await PostDonationAsync(
            campaign.CauseCode!, 10m, isAnonymous: true, recurring: 1);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MyRecurringAsync()).Should().BeEmpty();
    }

    /// <summary>A repeating gift needs a cause to repeat against.</summary>
    [Fact]
    public async Task A_recurring_donation_with_no_cause_is_refused()
    {
        await MemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/donations", UriKind.Relative),
            new
            {
                amount = 10m,
                currency = "GBP",
                beneficiaryType = "Platform",
                methodKind = "Card",
                recurringIntervalMonths = 1
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A retried donation with one key produces one schedule, not two.</summary>
    [Fact]
    public async Task A_retried_recurring_donation_produces_one_schedule()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();
        var key = Guid.NewGuid().ToString("N");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            (await HttpClient.PostAsJsonAsync(
                    new Uri($"{Financial}/donations", UriKind.Relative),
                    new
                    {
                        amount = 10m,
                        currency = "GBP",
                        causeCode = campaign.CauseCode,
                        methodKind = "Card",
                        idempotencyKey = key,
                        recurringIntervalMonths = 1
                    }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await MyRecurringAsync()).Should().HaveCount(1);
    }

    // ── The worker ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The schedule charges, through the ordinary payment path, and the cause's total moves.</b>
    /// <para>
    /// The next charge date advances by the interval, the occurrence is counted, and the money is a
    /// real completed donation — visible on the campaign's own supporter list.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_due_schedule_charges_and_the_causes_total_moves()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await DonateAsync(campaign.CauseCode!, 10m);

        (await PostDonationAsync(campaign.CauseCode!, 10m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var schedule = (await MyRecurringAsync()).Single();



        (await RunWorkerAsync(schedule)).Should().BeGreaterThanOrEqualTo(
            1, "the suite shares one database, so an earlier test's schedule may also be due");

        var charged = (await MyRecurringAsync()).Single();

        charged.ChargeCount.Should().Be(1);
        charged.LastChargeAt.Should().NotBeNull();
        charged.Status.Should().Be("Active");
        charged.NextChargeAt.Should().BeAfter(DateTimeOffset.UtcNow);
        charged.LastFailureReason.Should().BeNull();

        var refreshed = await GetCampaignAsync(campaign.CampaignId);

        refreshed.Progress.Raised.MinorUnits.Should().Be(2_000, "£10 given, £10 charged");
    }

    /// <summary>A schedule that is not due yet is left alone.</summary>
    [Fact]
    public async Task A_schedule_that_is_not_due_is_not_charged()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await PostDonationAsync(campaign.CauseCode!, 10m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await RunWorkerAsync()).Should().Be(0, "nothing is due yet");
        (await MyRecurringAsync()).Single().ChargeCount.Should().Be(0);
    }

    /// <summary>
    /// A declined card is recorded as a failure and retried, not silently marked successful.
    /// <para>
    /// The amount ends <c>.01</c>, which is the simulated gateway's decline — a real refusal through
    /// the real payment path rather than a stubbed one.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_declined_charge_is_recorded_and_retried()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await PostDonationAsync(campaign.CauseCode!, 10.01m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var schedule = (await MyRecurringAsync()).Single();



        (await RunWorkerAsync(schedule)).Should().BeGreaterThanOrEqualTo(
            1, "the suite shares one database, so an earlier test's schedule may also be due");

        var failed = (await MyRecurringAsync()).Single();

        failed.Status.Should().Be("Retrying");
        failed.ConsecutiveFailures.Should().Be(1);
        failed.LastFailureReason.Should().NotBeNullOrWhiteSpace();
        failed.ChargeCount.Should().Be(0);
        failed.NextChargeAt.Should().NotBeNull("a retry is scheduled");

        (await GetCampaignAsync(campaign.CampaignId)).Progress.Raised.MinorUnits
            .Should().Be(0, "nothing was taken");
    }

    /// <summary>
    /// A charge that would need the donor to confirm it is a failure, not a pending payment.
    /// <para>
    /// A 3-D Secure challenge is a good outcome when somebody is holding the phone and a dead end at
    /// three in the morning. Leaving the intent pending for ever would be a donation nobody ever
    /// resolves.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_charge_that_needs_the_donor_is_recorded_as_a_failure()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        // .03 is the simulated gateway's challenge.
        (await PostDonationAsync(campaign.CauseCode!, 10.03m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var schedule = (await MyRecurringAsync()).Single();


        await RunWorkerAsync(schedule);

        var failed = (await MyRecurringAsync()).Single();

        failed.Status.Should().Be("Retrying");
        failed.ChargeCount.Should().Be(0);
        failed.LastFailureReason.Should().Contain("confirm");
    }

    /// <summary>
    /// A cause that stopped collecting ends the schedule rather than failing against it for ever.
    /// <para>
    /// Completed rather than cancelled, because the donor did not ask for it — and the two say
    /// materially different things on a screen.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_closed_cause_completes_the_schedule()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await PostDonationAsync(campaign.CauseCode!, 10m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var schedule = (await MyRecurringAsync()).Single();


        await TransitionAsync(campaign.CampaignId, "close");

        (await RunWorkerAsync(schedule)).Should().BeGreaterThanOrEqualTo(
            1, "the suite shares one database, so an earlier test's schedule may also be due");

        var ended = (await MyRecurringAsync()).Single();

        ended.Status.Should().Be("Completed");
        ended.NextChargeAt.Should().BeNull();
        ended.ChargeCount.Should().Be(0);
    }

    /// <summary>
    /// <b>Two workers over one due schedule charge it once.</b>
    /// <para>
    /// The claim moves the due date and bumps the revision before any money is asked for, and the
    /// revision is a concurrency token — so the second pass finds nothing due. Run sequentially here
    /// because the suite's single SQLite connection cannot serve two workers at once; the property
    /// being proved is the claim, not the threading.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Two_passes_over_one_due_schedule_charge_it_once()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await PostDonationAsync(campaign.CauseCode!, 10m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var schedule = (await MyRecurringAsync()).Single();



        await RunWorkerAsync(schedule);
        await RunWorkerAsync(schedule);

        // Asserted on this schedule rather than on the sweep's return, because the suite shares one
        // database and an earlier test's schedule may be due on the same pass. What is under test is
        // that the claim stops the second pass touching this one.
        (await MyRecurringAsync()).Single().ChargeCount
            .Should().Be(1, "the claim moved the date before the money was asked for");

        (await GetCampaignAsync(campaign.CampaignId)).Progress.Raised.MinorUnits
            .Should().Be(1_000, "charged once, not twice");
    }

    // ── Cancellation ─────────────────────────────────────────────────────────────────────────

    /// <summary>The donor stops it, and nothing is charged afterwards.</summary>
    [Fact]
    public async Task Cancelling_stops_the_schedule()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await PostDonationAsync(campaign.CauseCode!, 10m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var schedule = (await MyRecurringAsync()).Single();

        (await CancelAsync(schedule.RecurringDonationId)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var cancelled = (await MyRecurringAsync()).Single();

        cancelled.Status.Should().Be("Cancelled");
        cancelled.NextChargeAt.Should().BeNull();
        cancelled.CancelledAt.Should().NotBeNull();

        await RunWorkerAsync(schedule);

        (await MyRecurringAsync()).Single().ChargeCount
            .Should().Be(0, "a cancelled schedule is never due");

        (await GetCampaignAsync(campaign.CampaignId)).Progress.Raised.MinorUnits
            .Should().Be(0, "nothing was taken after the cancellation");
    }

    /// <summary>Cancelling twice is one instruction, and the second succeeds.</summary>
    [Fact]
    public async Task Cancelling_is_idempotent()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await PostDonationAsync(campaign.CauseCode!, 10m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var schedule = (await MyRecurringAsync()).Single();

        (await CancelAsync(schedule.RecurringDonationId)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await CancelAsync(schedule.RecurringDonationId)).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Only the owner may cancel.
    /// <para>
    /// 404 rather than 403, so a caller cannot use the route to discover which schedules exist.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Only_the_owner_can_cancel_a_schedule()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await PostDonationAsync(campaign.CauseCode!, 10m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var schedule = (await MyRecurringAsync()).Single();

        await MemberAsync();

        (await CancelAsync(schedule.RecurringDonationId)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>A member sees their own schedules and nobody else's.</summary>
    [Fact]
    public async Task A_member_sees_only_their_own_schedules()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await PostDonationAsync(campaign.CauseCode!, 10m, recurring: 1)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        await MemberAsync();

        (await MyRecurringAsync()).Should().BeEmpty();
    }

    /// <summary>An anonymous caller reads nothing.</summary>
    [Fact]
    public async Task Reading_recurring_donations_requires_authentication()
    {
        HttpClient.DefaultRequestHeaders.Authorization = null;

        (await HttpClient.GetAsync(
                new Uri($"{Financial}/donations/recurring/me", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<List<RecurringShape>> MyRecurringAsync() =>
        await ReadDataAsync<List<RecurringShape>>(
            await HttpClient.GetAsync(
                new Uri($"{Financial}/donations/recurring/me", UriKind.Relative)));

    private Task<HttpResponseMessage> CancelAsync(Guid scheduleId) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/donations/recurring/{scheduleId}/cancel", UriKind.Relative),
            new { });

    /// <summary>
    /// One pass of the worker, as at a moment far enough ahead that everything is due.
    /// <para>
    /// <b>The moment is passed in rather than a clock being faked host-wide.</b> A mutable clock
    /// would move token expiry and every other deadline in the host with it; naming the moment says
    /// exactly what the test means — a year has passed — and everything the worker then decides is
    /// the real code on real data.
    /// </para>
    /// </summary>
    /// <summary>
    /// One pass of the worker, as at the moment the given schedule falls due.
    /// <para>
    /// <b>The moment is passed in rather than a clock being faked host-wide.</b> A mutable clock
    /// would move token expiry and every other deadline in the host with it; naming the moment says
    /// exactly what the test means — this schedule's date has come round — and everything the worker
    /// then decides is the real code on real data.
    /// </para>
    /// <para>
    /// <b>One second past the due date, not a year.</b> A schedule swept a year after it fell due
    /// legitimately owes twelve monthly charges, and the worker catches up one cycle per pass — so a
    /// far-future sweep charges again on the next pass, correctly, and would make a test about the
    /// claim assert the opposite of what it means.
    /// </para>
    /// </summary>
    private Task<int> RunWorkerAsync(RecurringShape? schedule = null) =>
        Factory.Services
            .GetServices<IHostedService>()
            .OfType<RecurringDonationSweep>()
            .Single()
            .RunOnceAsync(schedule?.NextChargeAt?.AddSeconds(1));

    private sealed record RecurringShape(
        Guid RecurringDonationId,
        string CauseCode,
        string? CauseTitle,
        MoneyShape Amount,
        int IntervalMonths,
        string Status,
        DateTimeOffset? NextChargeAt,
        DateTimeOffset? LastChargeAt,
        DateTimeOffset? LastAttemptAt,
        string? LastFailureReason,
        int ConsecutiveFailures,
        int ChargeCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CancelledAt);
}
