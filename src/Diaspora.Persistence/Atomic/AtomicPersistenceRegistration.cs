using Diaspora.Persistence.Atomic.Reservations;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Persistence.Atomic;

/// <summary>
/// Registers the two atomic ports.
/// <para>
/// Kept beside the primitive rather than inline in <c>AddPersistence</c> so that a test host — or a
/// module's own persistence registration — can compose the same two lines without pulling in the
/// production connection string. The outbox's registration makes the same split for the same
/// reason.
/// </para>
/// </summary>
public static class AtomicPersistenceRegistration
{
    public static IServiceCollection AddAtomicPersistence(this IServiceCollection services)
    {
        services.AddScoped<IAtomicUpdateExecutor, EfAtomicUpdateExecutor>();
        services.AddScoped<IAtomicReservationStore, AtomicReservationStore>();

        return services;
    }
}
