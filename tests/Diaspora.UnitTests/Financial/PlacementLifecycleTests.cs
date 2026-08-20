using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Pausing, resuming, renewing and warning — the four things a member could not do to a promotion
/// they had paid for.
/// <para>
/// <b>The resume test is the one that matters.</b> Cancelling and re-buying loses whatever remains
/// of a paid window; a pause banks it and a resume hands it back, and the arithmetic that makes that
/// true is on the aggregate rather than in a handler.
/// </para>
/// </summary>
public class PlacementLifecycleTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pausing_stops_delivery_and_announces_the_withdrawal()
    {
        var placement = Purchase();

        placement.Activate(Start);
        placement.Pause(Start.AddDays(2));

        placement.Status.Should().Be(PlacementStatus.Paused);
        placement.PausedAt.Should().Be(Start.AddDays(2));
        placement.IsInForce(Start.AddDays(3)).Should().BeFalse();

        // A ranking consumer has no vocabulary for "temporarily", so the pause is announced as an
        // ending and the resume as an activation.
        placement.DomainEvents.OfType<Modules.Financial.Domain.Events.PlacementEndedDomainEvent>()
            .Should().ContainSingle(raised => raised.Status == PlacementStatus.Paused);
    }

    /// <summary>
    /// <b>The member gets back exactly the days they paid for.</b> A resume that restarted the clock
    /// would give them more than they bought; one that ignored the pause would give them less.
    /// </summary>
    [Fact]
    public void Resuming_extends_the_window_by_exactly_the_paused_duration()
    {
        var placement = Purchase(days: 30);

        placement.Activate(Start);

        var originalEnd = placement.Window.EndsAt;

        placement.Pause(Start.AddDays(2));
        placement.Resume(Start.AddDays(9));

        placement.Status.Should().Be(PlacementStatus.Active);
        placement.PausedAt.Should().BeNull();
        placement.PausedDuration.Should().Be(TimeSpan.FromDays(7));
        placement.Window.EndsAt.Should().Be(originalEnd!.Value.AddDays(7));
    }

    [Fact]
    public void Pausing_twice_accumulates_the_banked_time()
    {
        var placement = Purchase(days: 30);

        placement.Activate(Start);
        placement.Pause(Start.AddDays(1));
        placement.Resume(Start.AddDays(3));
        placement.Pause(Start.AddDays(5));
        placement.Resume(Start.AddDays(6));

        placement.PausedDuration.Should().Be(TimeSpan.FromDays(3));
        placement.Window.EndsAt.Should().Be(Start.AddDays(30).AddDays(3));
    }

    /// <summary>
    /// Activating a paused placement would skip the paused-duration credit and quietly take back days
    /// the member paid for, so it is refused and <c>Resume</c> is the only way back.
    /// </summary>
    [Fact]
    public void A_paused_placement_resumes_rather_than_activating()
    {
        var placement = Purchase();

        placement.Activate(Start);
        placement.Pause(Start.AddDays(1));

        var attempt = () => placement.Activate(Start.AddDays(2));

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.PlacementNotActive);
    }

    [Fact]
    public void A_cancelled_placement_cannot_be_paused()
    {
        var placement = Purchase();

        placement.Activate(Start);
        placement.Cancel("Changed my mind.", Start.AddDays(1));

        var attempt = () => placement.Pause(Start.AddDays(2));

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.PlacementNotPausable);
    }

    /// <summary>
    /// Renewing early keeps the days that remain; renewing late does not silently backdate.
    /// </summary>
    [Fact]
    public void Extending_measures_from_the_current_end_when_the_window_is_still_open()
    {
        var placement = Purchase(days: 30);

        placement.Activate(Start);
        placement.Extend(30, Start.AddDays(20));

        placement.Window.EndsAt.Should().Be(Start.AddDays(60));
    }

    [Fact]
    public void Extending_measures_from_now_when_the_window_has_already_closed()
    {
        var placement = Purchase(days: 30);

        placement.Activate(Start);
        placement.Expire(Start.AddDays(30));
        placement.Extend(30, Start.AddDays(45));

        placement.Window.EndsAt.Should().Be(Start.AddDays(75));
        placement.Status.Should().Be(PlacementStatus.Active, "renewing an expired placement revives it");
    }

    /// <summary>
    /// Announced once per window, so a member warned every sweep pass does not learn to ignore the
    /// warning — and a renewal that moved the date makes it announceable again.
    /// </summary>
    [Fact]
    public void An_expiry_is_announced_once_per_window()
    {
        var placement = Purchase(days: 30);

        placement.Activate(Start);

        placement.IsExpiringWithin(TimeSpan.FromDays(3), Start.AddDays(28)).Should().BeTrue();
        placement.MarkExpiryAnnounced(Start.AddDays(28)).Should().BeTrue();
        placement.MarkExpiryAnnounced(Start.AddDays(28)).Should().BeFalse();
        placement.IsExpiringWithin(TimeSpan.FromDays(3), Start.AddDays(29)).Should().BeFalse();

        placement.Extend(30, Start.AddDays(29));

        placement.IsExpiringWithin(TimeSpan.FromDays(3), Start.AddDays(58)).Should().BeTrue(
            "a window that moved is a window worth warning about again");
    }

    [Fact]
    public void Auto_renew_is_a_stored_preference_that_changes_nothing_else()
    {
        var placement = Purchase(days: 30);

        placement.Activate(Start);

        var endsAt = placement.Window.EndsAt;

        placement.SetAutoRenew(enabled: true, Start.AddDays(1));

        placement.AutoRenewEnabled.Should().BeTrue();
        placement.Window.EndsAt.Should().Be(endsAt);
        placement.Status.Should().Be(PlacementStatus.Active);
    }

    private static Placement Purchase(int days = 30) =>
        Placement.Purchase(
            PlacementId.New(),
            SubjectReference.Create(MonetizationSubjectType.MarketplaceListing, Guid.NewGuid()),
            PlacementKind.Featured,
            DateWindow.ForDays(Start, days),
            2.0m,
            Guid.NewGuid(),
            "featured-listing-30",
            Start);
}
