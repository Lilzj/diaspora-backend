namespace Diaspora.Domain.Abstractions;

/// <summary>
/// <b>Superseded by B1.3 and retired in PSR-1 Final Certification. Do not implement it.</b>
/// <para>
/// A B1-era generic read repository. Every method on it takes or implies a bare <see cref="Guid"/>
/// key, and B1.3 made that an analyzer error: <c>DSP3001</c> reports a repository or handler that
/// keys on <see cref="Guid"/> rather than a strongly-typed identifier. <b>A repository written
/// against this interface would take exactly the parameter type the platform refuses</b>, so the
/// author would meet a build error whose cause is the interface they were told to implement.
/// </para>
/// <para>
/// <b>The rule it contradicts, and why the rule won.</b> <c>MemberId</c>, <c>GuideId</c> and
/// <c>ListingId</c> are all <see cref="Guid"/> underneath, so a signature taking three of them takes
/// three interchangeable arguments the compiler will happily let a caller transpose. The platform
/// paid for that lesson three separate times before B1.3 made it a rule with an analyzer behind it.
/// </para>
/// <para>
/// <b>Also unused because it was never the shape the platform wanted.</b> <c>ListAsync()</c> with no
/// filter and no paging is an unbounded table scan as a public API; B1.3's <c>CursorBoundary</c> and
/// ADR-017 settled how this platform reads collections, and every repository written since takes a
/// cursor and a limit.
/// </para>
/// <para>
/// <b>Why obsoleted rather than deleted</b>, following the precedent B1.4.1 set for
/// <c>INotificationPublisher</c> and PSR-1.2 recorded as DEBT-072: an empty namespace is an
/// invitation to declare the interface again, and the next author would have no way to know it had
/// been considered and refused. Obsoletion is a compiler diagnostic, not a change to the assembly's
/// surface.
/// </para>
/// <para>
/// <b>What to use instead.</b> Declare a repository interface in the owning module, keyed on that
/// aggregate's strongly-typed identifier, with the reads it actually needs. See the
/// [Persistence Rule Guide](../../../docs/platform/b1.3-persistence-rule-guide.md) and the
/// [Cursor Guide](../../../docs/platform/b1.3-cursor-guide.md).
/// </para>
/// </summary>
/// <seealso href="../../../docs/adrs/adr-017-cursor-paging-excludes-by-identity.md"/>
[Obsolete(
    "IReadRepository<T> is superseded by B1.3 and must not be implemented. It keys on Guid, which " +
    "DSP3001 reports as an error, and its unfiltered ListAsync predates the platform's cursor " +
    "paging model. Declare a repository in the owning module keyed on the aggregate's " +
    "strongly-typed identifier, taking a CursorBoundary where it returns a collection. See " +
    "docs/platform/b1.3-persistence-rule-guide.md and ADR-017.",
    error: true,
    DiagnosticId = "DSP3005")]
public interface IReadRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
