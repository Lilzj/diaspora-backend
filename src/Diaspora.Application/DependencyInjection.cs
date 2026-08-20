using Diaspora.Application.Behaviors;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Order is the contract. Correlation first, so everything below logs under the
            // right scope. Telemetry next, so the span covers validation and authorisation
            // failures as well as handler time. Validation and authorisation before the
            // transaction, so a rejected request never opens one. Audit last, so it
            // observes the committed outcome.
            //
            // B2 registered eight behaviors here; two of them — transactions and
            // idempotency — were empty pass-throughs, and two more ran duplicate stopwatches
            // over the same delegate. This is six that all do something.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CorrelationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(RequestTelemetryBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        });

        // FluentValidation — auto-register all validators
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Mapster
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(assembly);
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
