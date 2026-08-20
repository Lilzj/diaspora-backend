using Diaspora.Infrastructure.Services;
using Diaspora.Infrastructure.Transactions;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IGuidGenerator, GuidGenerator>();
        services.AddSingleton<ICorrelationContext, DefaultCorrelationContext>();

        services.AddScoped<ICurrentUser, DefaultCurrentUser>();
        services.AddScoped<IRequestContext, DefaultRequestContext>();
        services.AddScoped<IAuditService, DefaultAuditService>();

        // The two message pipelines, registered together because they are peers (B1.2). The
        // domain one is a port — a DbContext dispatches through it after every save. The
        // integration one is not: a producer publishes through IIntegrationEventPublisher and
        // never resolves this, which is reached only from the far side of the outbox, by the
        // in-process transport delivering a claimed message.
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        services.AddScoped<MediatRIntegrationEventDispatcher>();

        // Transaction reliability (B1.0.2). Only the retention sweep for the commit-verification
        // receipts and the options that pace it: execution itself is owned by the unit of work,
        // with nothing for a caller to resolve or configure.
        services.AddTransactionReliability(configuration);

        services.AddScoped<IFeatureGateService, DefaultFeatureGateService>();
        services.AddScoped<IPermissionService, DefaultPermissionService>();
        services.AddScoped<IEligibilityService, DefaultEligibilityService>();
        services.AddScoped<ITrustService, DefaultTrustService>();

        // Subject rate limiter. Singleton because its whole job is to hold state across
        // requests. The in-memory implementation is correct on one instance and approximate
        // across several; swapping it for the Redis-backed one is a single line here and
        // nothing above the ISubjectRateLimiter contract changes.
        services.AddSingleton<ISubjectRateLimiter, InMemorySubjectRateLimiter>();

        return services;
    }
}
