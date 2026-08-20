using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Persistence.Outbox;

/// <summary>
/// Binds the outbox's storage ports to EF Core.
/// <para>
/// Four registrations, all scoped, all over the same <c>DiasporaDbContext</c>. That the writer
/// shares the caller's context is not an implementation detail to be optimised away later — it
/// is the mechanism by which the outbox row joins the aggregate's transaction, and any change
/// that gives the writer its own context silently removes the atomicity guarantee while every
/// test still passes.
/// </para>
/// <para>
/// The store, the queries and the maintenance operations also share the request's context. They
/// never call it during a business transaction: the dispatcher runs in its own scope, and the
/// in-process transport gives each consumer a scope of its own so a consumer's half-written
/// state can never be flushed by the outbox's bookkeeping.
/// </para>
/// </summary>
public static class OutboxPersistenceRegistration
{
    public static IServiceCollection AddOutboxPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IOutboxWriter, EfOutboxWriter>();
        services.AddScoped<IOutboxStore, EfOutboxStore>();
        services.AddScoped<IOutboxQueries, EfOutboxQueries>();
        services.AddScoped<IOutboxMaintenance, EfOutboxMaintenance>();

        return services;
    }
}
