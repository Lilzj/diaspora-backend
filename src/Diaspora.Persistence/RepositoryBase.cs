using Diaspora.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence;

/// <summary>
/// <b>Retired with <see cref="IRepository{T}"/> in PSR-1 Final Certification. Do not derive from
/// it.</b>
/// <para>
/// The B1-era generic repository base. <b>Abstract, fully implemented, and with zero subclasses on
/// the entire platform</b> — every one of the fifty-odd repositories written since B1 declares its
/// own interface in its owning module and keys on that aggregate's strongly-typed identifier, which
/// is what B1.3 made a rule and <c>DSP3001</c> enforces.
/// </para>
/// <para>
/// <b>Its existence is why the interface it implements went unnoticed.</b> PSR-1.2's seam audit
/// counted interfaces with no implementing type; <see cref="IRepository{T}"/> had this one, so it
/// was recorded as live. An abstract base with no subclasses satisfies every such count while
/// providing exactly nothing — the <c>IEligibilityService</c> lesson at one remove.
/// </para>
/// <para>
/// Beyond the <see cref="Guid"/> key, <c>DbSet.FindAsync</c> here would also bypass the platform's
/// soft-delete query filter behaviour that <c>SoftDeleteConverter</c> and every real repository
/// account for, and <c>Delete</c> would issue a hard delete on an aggregate the platform expects to
/// tombstone.
/// </para>
/// <para>
/// Kept rather than deleted for the reason B1.4.1 gave when it retired <c>INotificationPublisher</c>:
/// an empty namespace is an invitation to declare the type again, and the next author would have no
/// way to know it had been considered and refused.
/// </para>
/// </summary>
[Obsolete(
    "RepositoryBase<T> is retired with IRepository<T> and must not be derived from. It keys on " +
    "Guid, which DSP3001 reports as an error, and its FindAsync and Delete bypass the platform's " +
    "soft-delete model. Declare a repository in the owning module keyed on the aggregate's " +
    "strongly-typed identifier. See docs/platform/b1.3-persistence-rule-guide.md.",
    error: true,
    DiagnosticId = "DSP3005")]
public abstract class RepositoryBase<T> : IRepository<T> where T : class
{
    protected DiasporaDbContext DbContext { get; }
    protected DbSet<T> DbSet { get; }

    protected RepositoryBase(DiasporaDbContext dbContext)
    {
        DbContext = dbContext;
        DbSet = dbContext.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbSet.FindAsync([id], cancellationToken);

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(entity, cancellationToken);

    public virtual void Update(T entity) =>
        DbSet.Update(entity);

    public virtual void Delete(T entity) =>
        DbSet.Remove(entity);
}
