using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Platform.Services.Abstractions;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
