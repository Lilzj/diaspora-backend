using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Diaspora.UnitTests.Persistence.Atomic;

/// <summary>
/// Keys EF's model cache on the database as well as the context type.
/// <para>
/// <b>Without this, one test host's model silently becomes every test host's model.</b> EF caches
/// a compiled model per <c>DbContext</c> CLR type, and every unit-test host in this suite builds
/// the same <c>DiasporaDbContext</c> with a different set of <see cref="IDatabaseContributor"/>s.
/// Until B1.3 they all happened to pass the same one, so nothing noticed; the moment a second
/// contributor set appeared, whichever host ran first decided what the other host's tables were,
/// and a dozen unrelated tests failed on missing tables while passing in isolation.
/// </para>
/// <para>
/// <b>Registering any replacement service is most of the fix.</b> It changes EF's internal
/// service-provider cache key, so this host gets its own provider and its own model cache; the
/// shared in-memory hosts keep the single model they want and this one builds its own from its own
/// contributor. The discriminator below only has to be a constant the default factory would never
/// produce.
/// </para>
/// <para>
/// It must also not read anything off the context. Asking for the connection string here resolves
/// the database facade, which resolves the model — inside the call that decides how to cache the
/// model — and EF refuses with "an attempt was made to use the model while it was being created".
/// </para>
/// </summary>
public sealed class AtomicTestModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        ArgumentNullException.ThrowIfNull(context);

        return (context.GetType(), "atomic-test", designTime);
    }
}
