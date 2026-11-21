using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Persistence.Model;

/// <summary>
/// The conventions every Diaspora model is built under: strongly typed identifiers, and the
/// <see cref="DateTimeOffset"/> storage form SQLite needs.
/// <para>
/// <b>A function rather than a contributor, deliberately.</b> BSOLID-002 recommended an
/// <c>IModelConventionContributor</c> on the grounds that the platform's contributor idiom already
/// fits. It fits the shape and not the problem: EF caches one model per <c>DbContext</c> type, so
/// two hosts composing different sets of convention contributors would share a single cached
/// model and silently get whichever was built first. The platform has already been bitten by that
/// with <c>IDatabaseContributor</c>; a second discovered set feeding the model would widen it.
/// Conventions are a pure function of the builder and the provider name, so that is what this is —
/// nothing to register, nothing to discover, nothing to cache wrongly.
/// </para>
/// </summary>
internal static class ModelConventions
{
    /// <summary>
    /// Applies every convention, in the order the context used to apply them inline.
    /// <para>
    /// <paramref name="providerName"/> is <c>Database.ProviderName</c>, passed in rather than read
    /// here so this type needs no context and no provider package.
    /// </para>
    /// </summary>
    internal static void Apply(ModelConfigurationBuilder configurationBuilder, string? providerName)
    {
        RegisterStronglyTypedIdConverters(configurationBuilder);
        RegisterSqliteDateTimeOffsetConverters(configurationBuilder, providerName);
    }

    private static void RegisterStronglyTypedIdConverters(ModelConfigurationBuilder configurationBuilder)
    {
        var idTypes = typeof(SharedKernel.Identity.UserId)
            .Assembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(StronglyTypedId)) && !t.IsAbstract);

        foreach (var idType in idTypes)
        {
            var converterType = typeof(StronglyTypedIdValueConverter<>).MakeGenericType(idType);
            configurationBuilder.Properties(idType).HaveConversion(converterType);
        }
    }

    /// <summary>
    /// Stores <see cref="DateTimeOffset"/> in an order-preserving binary form when running
    /// on SQLite.
    /// <para>
    /// SQLite has no native offset-aware date type, so EF cannot translate any comparison or
    /// ordering on a <c>DateTimeOffset</c> column — which is most of the identity module's
    /// queries: token expiry, session ceilings, "codes issued since". Without this, the
    /// integration suite could not run on SQLite at all.
    /// </para>
    /// <para>
    /// Matched on the provider name rather than a call to <c>Database.IsSqlite()</c>, which
    /// would drag the SQLite provider package into the production persistence assembly.
    /// PostgreSQL keeps its native <c>timestamptz</c> mapping and is unaffected.
    /// </para>
    /// </summary>
    private static void RegisterSqliteDateTimeOffsetConverters(
        ModelConfigurationBuilder configurationBuilder, string? providerName)
    {
        if (providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();

        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}
