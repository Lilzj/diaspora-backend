using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Persistence.Transactions;

/// <summary>
/// A dispatcher that records what it was given and does nothing with it.
/// <para>
/// Supplying one at all is the point: <c>DiasporaDbContext.SaveChangesAsync</c> only takes its
/// transactional, multi-round path when a dispatcher is present. A test host without one exercises
/// the single-save path and would miss every interaction between the dispatch loop and the
/// execution strategy — which is where the change tracker becomes hollow, and therefore where
/// B1.0.2's guard matters.
/// </para>
/// </summary>
public sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly List<IDomainEvent> _dispatched = [];

    public IReadOnlyList<IDomainEvent> Dispatched => _dispatched;

    public Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        _dispatched.AddRange(domainEvents);

        return Task.CompletedTask;
    }
}
