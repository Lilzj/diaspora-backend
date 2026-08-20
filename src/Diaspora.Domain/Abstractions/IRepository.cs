namespace Diaspora.Domain.Abstractions;

/// <summary>
/// <b>Superseded by B1.3 and retired in PSR-1 Final Certification. Do not implement it.</b>
/// <para>
/// The write half of the B1-era generic repository pair, retired with
/// <see cref="IReadRepository{T}"/> under the same diagnostic and for the same reason: every method
/// keys on a bare <see cref="Guid"/>, and <c>DSP3001</c> reports that as an error. A repository
/// written against this interface would take exactly the parameter type the platform refuses.
/// </para>
/// <para>
/// <b>The platform had already rejected it in prose, twice.</b> <c>IUserRepository</c> says so in
/// its own summary — <em>"Deliberately not derived from the kernel's <c>IRepository&lt;T&gt;</c>,
/// whose <c>GetByIdAsync(Guid)</c> would reintroduce the primitive obsession the strongly typed
/// identifiers exist to prevent"</em> — and every repository written since B1 has followed that
/// lead without anyone recording that the interface it declines is still declared.
/// </para>
/// <para>
/// <b>Why PSR-1.2's audit of unimplemented seams could not find this.</b> That audit enumerated
/// interfaces with no concrete type naming them; this one *has* an implementation,
/// <c>RepositoryBase&lt;T&gt;</c>, so it was counted as live and passed over. What the count could
/// not see is that <c>RepositoryBase&lt;T&gt;</c> is abstract with zero subclasses — so the pair is
/// a complete, compiling, fully-implemented abstraction that nothing on the platform uses. This is
/// the same shape as the <c>IEligibilityService</c> finding and the same lesson: <b>a stub is worse
/// than an absence, because every rule written to catch an absence passes over it.</b>
/// </para>
/// <para>
/// <b>Why obsoleted rather than deleted</b>, following the precedent B1.4.1 set for
/// <c>INotificationPublisher</c>: an empty namespace is an invitation to declare the interface
/// again, and the next author would have no way to know it had been considered and refused.
/// </para>
/// <para>
/// <b>What to use instead.</b> Declare a repository interface in the owning module, keyed on that
/// aggregate's strongly-typed identifier, exposing the reads and writes it actually needs. See the
/// [Persistence Rule Guide](../../../docs/platform/b1.3-persistence-rule-guide.md).
/// </para>
/// </summary>
[Obsolete(
    "IRepository<T> is superseded by B1.3 and must not be implemented. It keys on Guid, which " +
    "DSP3001 reports as an error and which IUserRepository's own documentation already refuses. " +
    "Declare a repository in the owning module keyed on the aggregate's strongly-typed " +
    "identifier. See docs/platform/b1.3-persistence-rule-guide.md.",
    error: true,
    DiagnosticId = "DSP3005")]
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
}
