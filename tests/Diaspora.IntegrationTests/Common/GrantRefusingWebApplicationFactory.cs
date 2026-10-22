using Diaspora.Contracts.Events;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Platform.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// A host whose outbox can be told to refuse a points grant, for testing what a claim does when the
/// payment behind it cannot be announced.
/// <para>
/// <b>Its own factory, not <c>WithWebHostBuilder</c> on a shared one.</b> The base factory opens its
/// in-memory database inside <c>ConfigureServices</c> and assigns it to a field, so deriving a host
/// from a shared fixture re-runs that, swaps the field for a new empty database, and every later
/// test in the collection fails reading a schema that is no longer there. A subclass gets its own
/// instance, its own field and its own database.
/// </para>
/// <para>
/// The refusal is a switch rather than permanent so one test can fail a claim and then retry it
/// through the same host and data.
/// </para>
/// </summary>
public sealed class GrantRefusingWebApplicationFactory : CustomWebApplicationFactory
{
    /// <summary>While true, publishing a <see cref="RewardPointsGrantedIntegrationEvent"/> throws.</summary>
    public bool RefuseGrants { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            var original = services.Last(
                descriptor => descriptor.ServiceType == typeof(IIntegrationEventPublisher));

            services.Remove(original);
            services.AddScoped<IIntegrationEventPublisher>(provider =>
                new GrantRefusingPublisher(
                    this,
                    (IIntegrationEventPublisher)(original.ImplementationFactory?.Invoke(provider)
                        ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!))));
        });
    }

    private sealed class GrantRefusingPublisher(
        GrantRefusingWebApplicationFactory factory, IIntegrationEventPublisher inner)
        : IIntegrationEventPublisher
    {
        public Task PublishAsync(
            IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) =>
            factory.RefuseGrants && integrationEvent is RewardPointsGrantedIntegrationEvent
                ? throw new InvalidOperationException("The outbox refused the points grant.")
                : inner.PublishAsync(integrationEvent, cancellationToken);
    }
}
