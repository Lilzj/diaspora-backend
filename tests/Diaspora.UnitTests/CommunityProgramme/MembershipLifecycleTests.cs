using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Persistence.Repositories;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// The fourteen-state membership machine.
/// <para>
/// The transition table is the server's authority and the client renders a copy of it; these tests
/// are what stop the two diverging silently. Every assertion here is a sentence from the register's
/// state model, checked against the code rather than against a comment.
/// </para>
/// </summary>
public sealed class MembershipLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    // ── Opening ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_application_can_only_open_in_a_state_a_membership_can_start_in()
    {
        foreach (var status in Enum.GetValues<ProgrammeMembershipStatus>())
        {
            var opened = Apply(status);

            var canStart = status is ProgrammeMembershipStatus.Draft
                or ProgrammeMembershipStatus.Pending
                or ProgrammeMembershipStatus.Approved;

            opened.IsSuccess.Should().Be(
                canStart, $"a membership {(canStart ? "may" : "may not")} open at {status}");
        }
    }

    [Fact]
    public void An_application_records_its_first_history_entry_and_raises_one_event()
    {
        var membership = Apply(ProgrammeMembershipStatus.Pending).Value;

        membership.History.Should().ContainSingle()
            .Which.Status.Should().Be(ProgrammeMembershipStatus.Pending);

        membership.DomainEvents.Should().ContainSingle();
    }

    // ── Transitions ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ProgrammeMembershipStatus.Pending, ProgrammeMembershipStatus.Approved, true)]
    [InlineData(ProgrammeMembershipStatus.Pending, ProgrammeMembershipStatus.Rejected, true)]
    [InlineData(ProgrammeMembershipStatus.Pending, ProgrammeMembershipStatus.Active, false)]
    [InlineData(ProgrammeMembershipStatus.Approved, ProgrammeMembershipStatus.Active, true)]
    [InlineData(ProgrammeMembershipStatus.Active, ProgrammeMembershipStatus.RenewalDue, true)]
    [InlineData(ProgrammeMembershipStatus.RenewalDue, ProgrammeMembershipStatus.Expired, true)]
    [InlineData(ProgrammeMembershipStatus.Suspended, ProgrammeMembershipStatus.Active, true)]
    [InlineData(ProgrammeMembershipStatus.Rejected, ProgrammeMembershipStatus.Active, false)]
    public void The_transition_table_is_the_state_model(
        ProgrammeMembershipStatus from, ProgrammeMembershipStatus to, bool allowed) =>
        ProgrammeMembershipTransitions.CanTransition(from, to).Should().Be(allowed);

    /// <summary>
    /// <b>A move to the state it is already in succeeds and does nothing.</b> That is what makes the
    /// lifecycle sweep idempotent: a second pass over a membership it already expired produces one
    /// history entry, not two.
    /// </summary>
    [Fact]
    public void Moving_to_the_current_state_is_a_no_op()
    {
        var membership = Active();
        var entries = membership.History.Count;

        var moved = membership.MoveTo(
            ProgrammeMembershipStatus.Active, Guid.NewGuid(), Now, MembershipActor.System);

        moved.IsSuccess.Should().BeTrue();
        membership.History.Should().HaveCount(entries, "a no-op writes no history");
    }

    [Fact]
    public void An_illegal_transition_is_refused_and_changes_nothing()
    {
        var membership = Apply(ProgrammeMembershipStatus.Pending).Value;

        var moved = membership.MoveTo(
            ProgrammeMembershipStatus.Active, Guid.NewGuid(), Now, MembershipActor.Organization);

        moved.IsFailure.Should().BeTrue();
        membership.Status.Should().Be(ProgrammeMembershipStatus.Pending);
    }

    // ── Benefits and the grace period ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>RenewalDue grants benefits.</b> Somebody one day past their renewal date has not lost their
    /// membership, and taking it away early is a product decision stated once here rather than
    /// twenty times across the screens.
    /// </summary>
    [Fact]
    public void Renewal_due_still_grants_benefits()
    {
        ProgrammeMembershipTransitions.GrantsBenefits(ProgrammeMembershipStatus.RenewalDue)
            .Should().BeTrue();

        ProgrammeMembershipTransitions.GrantsBenefits(ProgrammeMembershipStatus.Expired)
            .Should().BeFalse();
    }

    [Fact]
    public void Activation_sets_the_renewal_window_a_grace_period_before_the_expiry()
    {
        var membership = Active(renewalMonths: 12);

        membership.ExpiresAt.Should().Be(Now.AddMonths(12));
        membership.RenewalDueAt.Should().Be(
            Now.AddMonths(12).AddDays(-CommunityProgrammeLimits.RenewalGraceDays));
    }

    /// <summary>A lifetime or one-off tier never expires, so nothing is ever due.</summary>
    [Fact]
    public void A_membership_with_no_billing_period_never_expires()
    {
        var membership = Active(renewalMonths: null);

        membership.ExpiresAt.Should().BeNull();
        membership.RenewalDueAt.Should().BeNull();
        membership.IsRenewable.Should().BeFalse("there is nothing to renew");
    }

    // ── Renewal ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Renewing early does not cost the member the days they had left.</b> The extension runs from
    /// the existing expiry rather than from now.
    /// </summary>
    [Fact]
    public void Renewing_early_extends_from_the_existing_expiry()
    {
        var membership = Active(renewalMonths: 12);
        var originalExpiry = membership.ExpiresAt!.Value;

        membership.Renew(Guid.NewGuid(), Now.AddMonths(6), 12, "key");

        membership.ExpiresAt.Should().Be(originalExpiry.AddMonths(12));
    }

    [Fact]
    public void Renewing_records_its_own_key_separately_from_the_application_key()
    {
        var membership = Active(renewalMonths: 12);

        membership.ApplicationIdempotencyKey.Should().Be("application-key");

        membership.Renew(Guid.NewGuid(), Now, 12, "renewal-key");

        membership.RenewalIdempotencyKey.Should().Be("renewal-key");
        membership.ApplicationIdempotencyKey.Should().Be(
            "application-key", "a renewal must not look like a replayed application");
    }

    [Fact]
    public void A_membership_that_cannot_renew_refuses()
    {
        var membership = Apply(ProgrammeMembershipStatus.Pending).Value;

        membership.Renew(Guid.NewGuid(), Now, 12, null).IsFailure.Should().BeTrue();
    }

    // ── Rejoining ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Rejoining reuses the row and keeps the history.</b> The unique index on (organization,
    /// member) is what makes "you already hold one of these" a database fact, and a second row would
    /// violate it — so a member coming back moves the same row and the record of the first attempt
    /// survives for the administrator deciding.
    /// </summary>
    [Fact]
    public void Rejoining_reuses_the_row_and_keeps_the_history()
    {
        var membership = Active();

        membership.MoveTo(
            ProgrammeMembershipStatus.Cancelled,
            Guid.NewGuid(),
            Now,
            MembershipActor.Member).IsSuccess.Should().BeTrue();

        var entriesBefore = membership.History.Count;
        var newTier = MembershipTierId.New();

        var rejoined = membership.Rejoin(
            newTier, ProgrammeMembershipStatus.Pending, Guid.NewGuid(), Now.AddDays(1), "second");

        rejoined.IsSuccess.Should().BeTrue();
        membership.Status.Should().Be(ProgrammeMembershipStatus.Pending);
        membership.TierId.Should().Be(newTier, "somebody may come back at a different tier");
        membership.History.Should().HaveCountGreaterThan(entriesBefore, "both attempts are kept");
        membership.ApplicationIdempotencyKey.Should().Be("second");
    }

    [Fact]
    public void A_live_membership_cannot_be_rejoined()
    {
        var membership = Active();

        membership.Rejoin(
            MembershipTierId.New(),
            ProgrammeMembershipStatus.Pending,
            Guid.NewGuid(),
            Now,
            null)
            .IsFailure.Should().BeTrue();
    }

    // ── The duplication the repository has to make ──────────────────────────────────────────

    /// <summary>
    /// <b>The repository's capacity filter and the domain's rule must agree for all fourteen
    /// states.</b> <c>OccupiesCapacity</c> is a computed property EF cannot translate, so the
    /// repository lists the statuses instead — and a duplication like that stays honest only if
    /// something asserts it. This is that something.
    /// </summary>
    [Fact]
    public void The_repositorys_capacity_filter_matches_the_domain_rule()
    {
        var listed = ProgrammeMembershipRepository.OccupyingStatuses().ToHashSet();

        foreach (var status in Enum.GetValues<ProgrammeMembershipStatus>())
        {
            listed.Contains(status).Should().Be(
                ProgrammeMembershipTransitions.OccupiesCapacity(status),
                $"{status} must be filtered the same way it is decided");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static Result<ProgrammeMembership> Apply(
        ProgrammeMembershipStatus opening) =>
        ProgrammeMembership.Apply(
            ProgrammeMembershipId.New(),
            Guid.NewGuid(),
            MembershipTierId.New(),
            UserId.From(Guid.NewGuid()),
            opening,
            Guid.NewGuid(),
            Now,
            "application-key");

    private static ProgrammeMembership Active(int? renewalMonths = 12)
    {
        var membership = Apply(ProgrammeMembershipStatus.Approved).Value;

        membership.MoveTo(
            ProgrammeMembershipStatus.Active,
            Guid.NewGuid(),
            Now,
            MembershipActor.System,
            renewalMonths: renewalMonths);

        return membership;
    }
}
