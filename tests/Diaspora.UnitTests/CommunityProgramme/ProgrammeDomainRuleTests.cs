using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Modules.CommunityProgramme.Domain.ValueObjects;
using Diaspora.Modules.CommunityProgramme.Domain.Volunteering;
using Diaspora.Modules.CommunityProgramme.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// The rules that are not a state machine: prices, capacity, confirmed hours, poll options and the
/// cursor.
/// </summary>
public sealed class ProgrammeDomainRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    // ── Price ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Minor units, and rounded away from zero at the currency's own precision.</b> A member is
    /// never charged more than the number they were shown, because the number they were shown is
    /// rendered back from this value.
    /// </summary>
    [Theory]
    [InlineData(12.50, "GBP", 2, 1250)]
    [InlineData(12.345, "GBP", 2, 1235)]
    [InlineData(1000, "JPY", 0, 1000)]
    [InlineData(0.005, "GBP", 2, 1)]
    public void A_price_is_stored_in_minor_units(
        decimal amount, string currency, int exponent, long expected)
    {
        var price = ProgrammePrice.FromAmount(amount, currency, exponent);

        price.MinorUnits.Should().Be(expected);
        price.Currency.Should().Be(currency);
    }

    [Fact]
    public void A_price_renders_back_to_the_amount_it_was_made_from()
    {
        ProgrammePrice.FromAmount(12.50m, "gbp", 2).Amount.Should().Be(12.50m);
        ProgrammePrice.FromAmount(12.50m, "gbp", 2).Currency.Should().Be("GBP");
    }

    // ── Tier capacity ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>An uncapped tier is never full.</b> The client does not compare filled to capacity, which
    /// is only possible because a null capacity answers here rather than there.
    /// </summary>
    [Fact]
    public void An_uncapped_tier_is_never_full()
    {
        var tier = Tier(capacity: null);

        tier.IsFull.Should().BeFalse();
    }

    [Fact]
    public void A_capped_tier_fills()
    {
        var programme = Programme();
        var tier = Tier(capacity: 2);

        programme.ReplaceTiers([tier], Now);
        programme.Reconcile(2, new Dictionary<MembershipTierId, int> { [tier.Id] = 2 }, Now);

        programme.Tiers[0].IsFull.Should().BeTrue();
    }

    /// <summary>
    /// <b>A tier that survives a wholesale replacement keeps its members.</b> Re-posting the editor's
    /// form must not empty every tier.
    /// </summary>
    [Fact]
    public void Replacing_the_tier_list_carries_the_filled_counts()
    {
        var programme = Programme();
        var tier = Tier(capacity: 10);

        programme.ReplaceTiers([tier], Now);
        programme.Reconcile(4, new Dictionary<MembershipTierId, int> { [tier.Id] = 4 }, Now);

        var renamed = MembershipTier.Create(
            tier.Id, "Gold (renamed)", null, null, BillingPeriod.Yearly, 10, false, 0);

        programme.ReplaceTiers([renamed], Now);

        programme.Tiers[0].FilledCount.Should().Be(4);
        programme.Tiers[0].Name.Should().Be("Gold (renamed)");
    }

    /// <summary>A one-off and a lifetime tier both answer null to "when does this lapse".</summary>
    [Theory]
    [InlineData(BillingPeriod.OneOff, null)]
    [InlineData(BillingPeriod.Lifetime, null)]
    [InlineData(BillingPeriod.Monthly, 1)]
    [InlineData(BillingPeriod.Quarterly, 3)]
    [InlineData(BillingPeriod.Yearly, 12)]
    public void A_tiers_renewal_window_follows_its_billing_period(
        BillingPeriod period, int? months)
    {
        var tier = MembershipTier.Create(
            MembershipTierId.New(), "Tier", null, null, period, null, false, 0);

        tier.RenewalMonths.Should().Be(months);
    }

    // ── Volunteer hours ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Hours can only be confirmed for somebody who checked in.</b> Confirming for somebody who
    /// never arrived would make the check-in optional, and a record of hours nobody witnessed is what
    /// the whole aggregate exists to avoid.
    /// </summary>
    [Fact]
    public void Hours_cannot_be_confirmed_before_a_check_in()
    {
        var assignment = Assignment();

        assignment.ConfirmHours(4m, UserId.From(Guid.NewGuid()), Now)
            .IsFailure.Should().BeTrue();

        assignment.ConfirmedHours.Should().BeNull();
    }

    [Fact]
    public void Confirming_hours_completes_the_assignment_and_raises_the_event()
    {
        var assignment = Assignment();

        assignment.CheckIn(Now).IsSuccess.Should().BeTrue();

        var confirmed = assignment.ConfirmHours(3.5m, UserId.From(Guid.NewGuid()), Now.AddHours(4));

        confirmed.IsSuccess.Should().BeTrue();
        assignment.ConfirmedHours.Should().Be(3.5m);
        assignment.Status.Should().Be(VolunteerAssignmentStatus.Completed);
        assignment.CheckedOutAt.Should().NotBeNull("checking out is implied by confirming");
        assignment.DomainEvents.Should().HaveCount(2, "creation and confirmation");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25)]
    public void Impossible_hours_are_refused(decimal hours)
    {
        var assignment = Assignment();
        assignment.CheckIn(Now);

        assignment.ConfirmHours(hours, UserId.From(Guid.NewGuid()), Now).IsFailure.Should().BeTrue();
    }

    // ── Polls ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>An anonymous poll cannot allow changes.</b> There is no stored identity to find a previous
    /// answer by, and accepting the flag would be advertising a capability the storage model cannot
    /// deliver.
    /// </summary>
    [Fact]
    public void An_anonymous_poll_cannot_allow_changes()
    {
        var poll = Poll();

        poll.Describe(
            null, null, null, null, null,
            isAnonymous: true,
            allowsChange: true,
            showResultsBeforeClose: false,
            null, null, Now);

        poll.AllowsChange.Should().BeFalse();
    }

    [Fact]
    public void A_poll_cannot_close_before_it_opens()
    {
        var poll = Poll();

        var described = poll.Describe(
            null, null, null, null, null, false, false, false,
            opensAt: Now.AddDays(2),
            closesAt: Now.AddDays(1),
            Now);

        described.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_members_only_poll_must_name_its_organization()
    {
        var poll = CommunityPoll.Draft(
            CommunityPollId.New(),
            UserId.From(Guid.NewGuid()),
            "Should we?",
            PollVotingType.SingleChoice,
            PollAudience.OrganizationMembers,
            Now).Value;

        poll.Describe(null, null, null, null, null, false, false, false, null, null, Now)
            .IsFailure.Should().BeTrue();
    }

    /// <summary>A poll with choices needs at least two of them before it can open.</summary>
    [Fact]
    public void A_poll_with_one_option_cannot_open()
    {
        var poll = Poll();

        poll.ReplaceOptions([Option("Only one", 0)], Now).IsFailure.Should().BeTrue(
            "a poll with a single choice is not a question");
    }

    /// <summary>
    /// <b>Options cannot be changed once people have voted.</b> Changing them would silently reassign
    /// their votes, and the refusal is the only honest answer.
    /// </summary>
    [Fact]
    public void Options_cannot_change_after_a_poll_opens()
    {
        var poll = Poll();

        poll.ReplaceOptions([Option("Yes", 0), Option("No", 1)], Now);
        poll.Publish(Now).IsSuccess.Should().BeTrue();

        poll.ReplaceOptions([Option("Maybe", 0), Option("Never", 1)], Now)
            .IsFailure.Should().BeTrue();
    }

    /// <summary>Publishing with a future opening moment schedules rather than opens.</summary>
    [Fact]
    public void Publishing_with_a_future_opening_moment_schedules()
    {
        var poll = Poll();

        poll.ReplaceOptions([Option("Yes", 0), Option("No", 1)], Now);
        poll.Describe(null, null, null, null, null, false, false, false, Now.AddDays(1), null, Now);

        poll.Publish(Now).IsSuccess.Should().BeTrue();
        poll.Status.Should().Be(PollStatus.Scheduled);
    }

    /// <summary>A changed vote is one vote that changed. The total must not move.</summary>
    [Fact]
    public void Recasting_a_response_does_not_change_the_total()
    {
        var poll = Poll();
        poll.ReplaceOptions([Option("Yes", 0), Option("No", 1)], Now);
        poll.RecordResponse(1, Now);

        var total = poll.TotalResponses;
        var response = PollResponse.Cast(
            PollResponseId.New(),
            poll.Id,
            UserId.From(Guid.NewGuid()),
            "fingerprint",
            [poll.Options[0].Id],
            null,
            Now);

        response.Recast([poll.Options[1].Id], null, Now.AddMinutes(1));

        response.Selections.Should().ContainSingle()
            .Which.OptionId.Should().Be(poll.Options[1].Id);

        poll.TotalResponses.Should().Be(total);
    }

    // ── The cursor ──────────────────────────────────────────────────────────────────────────

    /// <summary>A cursor round-trips through its own encoding.</summary>
    [Fact]
    public void A_cursor_round_trips()
    {
        var encoded = CommunityProgrammeCursor.FromTimestamp(Now, "abc,def").Encode();
        var parsed = CommunityProgrammeCursor.Parse(encoded);

        parsed.Should().NotBeNull();
        parsed!.AsTimestamp().Should().Be(Now);
        parsed.BoundaryKey.Should().Be("abc,def");
    }

    /// <summary>
    /// <b>A malformed cursor parses to null rather than throwing.</b> A stale or hand-edited token is
    /// an ordinary event, and answering a browse screen with the first page beats answering it with
    /// a 400.
    /// </summary>
    [Theory]
    [InlineData("not-base64")]
    [InlineData("")]
    [InlineData(null)]
    public void A_malformed_cursor_parses_to_null(string? token) =>
        CommunityProgrammeCursor.Parse(token).Should().BeNull();

    /// <summary>
    /// <b>The boundary accumulates while the sort key does not move.</b> Carrying only the last
    /// page's identifiers would hand page one's rows back on page three — the way the platform's
    /// first attempt at this failed.
    /// </summary>
    [Fact]
    public void The_boundary_accumulates_across_a_tie_group()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var rows = new[] { (Key: Now, Id: first) };

        var page1 = CursorBoundary.Key(rows, row => row.Key, row => row.Id);

        var page2 = CursorBoundary.Key(
            new[] { (Key: Now, Id: second) },
            row => row.Key,
            row => row.Id,
            page1,
            Now);

        page2.Should().Contain(first.ToString("N")).And.Contain(second.ToString("N"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static MembershipProgramme Programme() =>
        MembershipProgramme.Open(
            MembershipProgrammeId.New(),
            Guid.NewGuid(),
            "A programme.",
            ProgrammeVisibility.Public,
            isOpen: true,
            requiresApproval: true,
            Now).Value;

    private static MembershipTier Tier(int? capacity) =>
        MembershipTier.Create(
            MembershipTierId.New(),
            "Gold",
            "The good one.",
            ProgrammePrice.FromAmount(20m, "GBP", 2),
            BillingPeriod.Yearly,
            capacity,
            isRecommended: true,
            sortOrder: 0);

    private static VolunteerAssignment Assignment() =>
        VolunteerAssignment.Create(
            VolunteerAssignmentId.New(),
            VolunteerApplicationId.New(),
            VolunteerOpportunityId.New(),
            Guid.NewGuid(),
            UserId.From(Guid.NewGuid()),
            null,
            Now,
            Now.AddHours(4),
            Now).Value;

    private static CommunityPoll Poll() =>
        CommunityPoll.Draft(
            CommunityPollId.New(),
            UserId.From(Guid.NewGuid()),
            "Should we?",
            PollVotingType.SingleChoice,
            PollAudience.Public,
            Now).Value;

    private static PollOption Option(string label, int order) =>
        PollOption.Create(PollOptionId.New(), label, null, order);
}
