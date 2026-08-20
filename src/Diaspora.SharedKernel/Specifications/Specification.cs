using System.Linq.Expressions;

namespace Diaspora.SharedKernel.Specifications;

/// <summary>
/// <b>Retired in PSR-1 Final Certification. Do not derive from it.</b>
/// <para>
/// The specification pattern, declared in B1 and adopted by nothing. <b>B2.5's production-readiness
/// audit listed it among the SharedKernel's dead machinery</b> (DDD-02) with the risk it carries:
/// <em>"New module authors will copy whichever they find first."</em> Every module since has
/// composed its queries directly against EF, and B1.3's <c>CursorBoundary</c> plus ADR-017 settled
/// how this platform reads collections.
/// </para>
/// <para>
/// <b>It also carries a performance trap that no reviewer would see at the call site.</b>
/// <see cref="IsSatisfiedBy"/> calls <c>ToExpression().Compile()</c> — building and JIT-compiling a
/// delegate from an expression tree — on <em>every single invocation</em>. Used the obvious way,
/// inside a loop over a page of results, that is one compilation per row.
/// </para>
/// <para>
/// Kept rather than deleted, per B1.4.1's precedent: an empty namespace is an invitation to declare
/// the type again. A future phase that wants the pattern removes this attribute deliberately, in a
/// commit that says so, having decided what <see cref="IsSatisfiedBy"/> should do instead.
/// </para>
/// </summary>
[Obsolete(
    "Specification<T> is retired: nothing on the platform has ever derived from it, and " +
    "IsSatisfiedBy compiles an expression tree on every call. Compose queries directly and page " +
    "them with CursorBoundary — B2.5 DDD-02, ADR-017. See " +
    "docs/platform/b1.3-persistence-rule-guide.md.",
    error: true,
    DiagnosticId = "DSP3006")]
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity)
    {
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }

    public Specification<T> And(Specification<T> other) =>
        new AndSpecification<T>(this, other);

    public Specification<T> Or(Specification<T> other) =>
        new OrSpecification<T>(this, other);

    public Specification<T> Not() =>
        new NotSpecification<T>(this);

    public static Specification<T> operator &(Specification<T> left, Specification<T> right) =>
        left.And(right);

    public static Specification<T> operator |(Specification<T> left, Specification<T> right) =>
        left.Or(right);

    public static Specification<T> operator !(Specification<T> spec) =>
        spec.Not();
}
