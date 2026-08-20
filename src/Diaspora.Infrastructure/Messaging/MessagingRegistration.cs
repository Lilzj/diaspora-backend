using Diaspora.Infrastructure.Services;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diaspora.Infrastructure.Messaging;

/// <summary>
/// Wires reliable messaging into the host.
/// <para>
/// This is where B1.1's one behavioural substitution happens:
/// <c>MediatRIntegrationEventPublisher</c> — which handed every integration event straight to
/// the in-process dispatcher and lost it if anything threw — is replaced by
/// <see cref="OutboxIntegrationEventPublisher"/>, which writes a durable row inside the
/// producer's transaction. Nothing else in the platform is aware the substitution happened.
/// </para>
/// <para>
/// <c>Replace</c> rather than <c>Add</c> is deliberate. The identity module registers the old
/// publisher during its own composition, and adding a second implementation of a
/// single-resolution interface would leave which one wins to registration order — a coin flip
/// that decides whether the platform loses events.
/// </para>
/// <para>
/// <b>B1.2 removed the second substitution.</b> B1.1 also had to replace
/// <c>IDomainEventDispatcher</c> with a routing wrapper, because Trust and Community published
/// their integration events through it — legal only because <c>IntegrationEvent</c> derived from
/// <c>DomainEvent</c>. That wrapper was a compensating control for a hole in the type system,
/// and it has been deleted along with the hole: the domain dispatcher is once again the plain
/// in-process one, and the compiler is what keeps integration events out of it. The publisher
/// substitution below is now the whole of the wiring, which is what B1.1 intended in the first
/// place.
/// </para>
/// </summary>
public static class MessagingRegistration
{
    public static IServiceCollection AddReliableMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Serialization, partitioning, retry, telemetry and the operator surface: everything
        // that does not depend on where messages are stored or how they are delivered.
        services.AddPlatformMessaging(configuration);

        // The transport. One line, and the only line that changes the day a module moves out of
        // this process and onto a broker.
        services.TryAddSingleton<IOutboxTransport, InProcessOutboxTransport>();

        services.TryAddSingleton<IOutboxWorkerIdentity, MachineOutboxWorkerIdentity>();

        // The substitution, and since B1.2 the only one. See the class remarks.
        services.Replace(
            ServiceDescriptor.Scoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>());

        // MediatRIntegrationEventDispatcher — which the in-process transport and the
        // outbox-disabled fallback both need — is registered by AddInfrastructure alongside its
        // domain twin. It is a pipeline, not an outbox component, and a host that composes the
        // pipelines without the outbox should still get both.

        services.AddHostedService<OutboxDispatcherHostedService>();
        services.AddHostedService<OutboxRetentionHostedService>();

        return services;
    }
}
