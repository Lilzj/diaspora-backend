using Diaspora.Domain.Abstractions;
using Diaspora.Persistence.Atomic;
using Diaspora.Persistence.Auditing;
using Diaspora.Persistence.Identity;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.Persistence.Modules;
using Diaspora.Persistence.Outbox;
using Diaspora.Persistence.Transactions;
using Diaspora.Persistence.Usage;
using Diaspora.Platform.Usage;
using Diaspora.Platform.Transactions;
using Diaspora.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DiasporaDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(DiasporaDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(3);
            });
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<DiasporaDbContext>());

        // The outbox's storage ports. Registered here rather than by the platform kernel
        // because these four are the only parts of reliable messaging that need EF Core, and
        // the kernel deliberately does not reference it — the same split the database
        // contributor makes, for the same reason.
        services.AddOutboxPersistence();

        // The verification receipt's housekeeping port. The insert and the lookup that make a
        // commit verifiable live inside the unit of work itself — they are steps in a transaction,
        // not services anything else may call — so this is the only one that needs registering.
        services.AddScoped<ITransactionReceiptStore, EfTransactionReceiptStore>();

        // The audit trail is stored, not just logged (7 Nov 2026). Replaces the log-only default
        // the infrastructure layer registers, which runs first; Replace rather than Add so the
        // order of the two registrations cannot decide which one answers.
        services.Replace(ServiceDescriptor.Scoped<IAuditService, EfAuditService>());
        services.AddScoped<Diaspora.Platform.Auditing.IAuditTrailReader, EfAuditTrailReader>();

        // The monthly allowance on paid vendor calls. In the database rather than in memory
        // because every Fly machine has to draw on the same allowance.
        services.AddScoped<IProviderUsageMeter, EfProviderUsageMeter>();

        // The two subject-rights providers for the identity schema. Registered here, with the identity
        // mappings they operate on, and scoped because they read and write through the request's own
        // DbContext — which is what lets an erasure participate in the caller's unit of work rather
        // than committing a half-finished one of its own.
        services.AddScoped<IDataErasureProvider, IdentityDataErasureProvider>();
        services.AddScoped<IDataExportProvider, IdentityDataExportProvider>();

        // The guarded atomic update primitive B10 and B11 each closed by recommending. Scoped
        // because both run on the request's own context and transaction — a singleton executor
        // would be a captive dependency over a scoped unit of work, which the host's provider
        // validation refuses to start on.
        services.AddAtomicPersistence();

        RegisterDatabaseContributors(services);

        return services;
    }

    /// <summary>
    /// Discovers every module's <see cref="IDatabaseContributor"/> and registers it, so the
    /// shared <c>DiasporaDbContext</c> is handed the full set when EF constructs it.
    /// <para>
    /// Done here rather than in the platform kernel because <see cref="IDatabaseContributor"/>
    /// is the one contributor that needs EF Core, and the kernel deliberately does not. A
    /// module that stores nothing contributes nothing here.
    /// </para>
    /// </summary>
    private static void RegisterDatabaseContributors(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies();

        foreach (var type in ModuleAssemblyScanner.FindImplementations<IDatabaseContributor>(assemblies))
        {
            if (type.GetConstructor(Type.EmptyTypes) is not null)
            {
                services.AddSingleton(typeof(IDatabaseContributor), type);
            }
        }
    }
}
