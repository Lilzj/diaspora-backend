using Diaspora.Persistence.Atomic.Reservations;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Persistence.Atomic;

/// <summary>
/// The reservation shape: which operations may refuse, which must never, and what the counts do.
/// </summary>
public sealed class AtomicReservationStoreTests : IDisposable
{
    private readonly AtomicTestHost _host = new();

    [Fact]
    public async Task Reserving_within_capacity_reports_the_new_state()
    {
        var id = _host.Seed(total: 5);

        await using var context = _host.NewContext();

        var outcome = await AtomicTestHost.Store(context).ReserveAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 2));

        outcome.Applied.Should().BeTrue();
        outcome.State.Should().NotBeNull();
        outcome.State!.Held.Should().Be(2);
        outcome.State.Available.Should().Be(3);
    }

    [Fact]
    public async Task Reserving_beyond_capacity_is_refused_and_reports_no_state()
    {
        var id = _host.Seed(total: 2);

        await using var context = _host.NewContext();
        var store = AtomicTestHost.Store(context);

        await store.ReserveAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 2));

        var refused = await store.ReserveAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 1));

        refused.Applied.Should().BeFalse();
        refused.State.Should().BeNull();

        _host.Load(id).Capacity.Held.Should().Be(2);
    }

    /// <summary>
    /// <b>The asymmetry the interface is built around.</b> A release that could be refused is a room
    /// held forever, which is the one outcome worse than a double booking.
    /// </summary>
    [Fact]
    public async Task Releasing_more_than_is_held_still_applies_and_clamps_at_zero()
    {
        var id = _host.Seed(total: 5);

        await using var context = _host.NewContext();
        var store = AtomicTestHost.Store(context);

        await store.ReserveAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 1));

        var outcome = await store.ReleaseAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 4));

        outcome.Applied.Should().BeTrue();
        outcome.State!.Held.Should().Be(0);
    }

    [Fact]
    public async Task Consuming_moves_units_from_held_to_consumed()
    {
        var id = _host.Seed(total: 5);

        await using var context = _host.NewContext();
        var store = AtomicTestHost.Store(context);

        await store.ReserveAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 3));

        var outcome = await store.ConsumeAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 2));

        outcome.State!.Held.Should().Be(1);
        outcome.State.Consumed.Should().Be(2);
        outcome.State.Available.Should().Be(2);
    }

    /// <summary>
    /// Neither half of the move may run without the other, however far the counters have drifted.
    /// Consuming three against one held moves one, not three.
    /// </summary>
    [Fact]
    public async Task Consuming_more_than_is_held_moves_only_what_is_there()
    {
        var id = _host.Seed(total: 5);

        await using var context = _host.NewContext();
        var store = AtomicTestHost.Store(context);

        await store.ReserveAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 1));

        var outcome = await store.ConsumeAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 3));

        outcome.State!.Held.Should().Be(0);
        outcome.State.Consumed.Should().Be(1, "a unit cannot be consumed twice or invented");
    }

    [Fact]
    public async Task Consuming_directly_is_guarded_like_a_reservation()
    {
        var id = _host.Seed(total: 2);

        await using var context = _host.NewContext();
        var store = AtomicTestHost.Store(context);

        var taken = await store.ConsumeDirectlyAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 2));

        taken.Applied.Should().BeTrue();
        taken.State!.Consumed.Should().Be(2);

        var refused = await store.ConsumeDirectlyAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 1));

        refused.Applied.Should().BeFalse();
    }

    [Fact]
    public async Task Restoring_returns_consumed_units_to_the_pool()
    {
        var id = _host.Seed(total: 4);

        await using var context = _host.NewContext();
        var store = AtomicTestHost.Store(context);

        await store.ConsumeDirectlyAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 3));

        var outcome = await store.RestoreAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, units: 2));

        outcome.State!.Consumed.Should().Be(1);
        outcome.State.Available.Should().Be(3);
    }

    /// <summary>
    /// A capacity that only holds and releases — a rate limit, a concurrent-session count — is a
    /// legitimate shape, and asking it to consume is a programming error rather than a refusal.
    /// </summary>
    [Fact]
    public async Task A_capacity_with_no_consumed_state_refuses_to_consume()
    {
        var id = _host.Seed(total: 3);

        await using var context = _host.NewContext();

        var capacity = AtomicCapacity<AtomicTestResource>.Of(
            resource => resource.Capacity.Total,
            resource => resource.Capacity.Held);

        var consume = async () => await AtomicTestHost.Store(context).ConsumeAsync(
            new AtomicReservation<AtomicTestResource>(capacity, id, units: 1));

        await consume.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void A_reservation_of_no_units_is_refused_at_construction()
    {
        var build = () => new AtomicReservation<AtomicTestResource>(
            AtomicTestHost.Capacity, AtomicTestResourceId.New(), units: 0);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    public void Dispose() => _host.Dispose();
}
