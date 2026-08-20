using System.Linq.Expressions;

namespace Diaspora.SharedKernel.Specifications;

[Obsolete(
    "Retired with Specification<T> — B2.5 DDD-02. See docs/platform/b1.3-persistence-rule-guide.md.",
    error: true,
    DiagnosticId = "DSP3006")]
internal sealed class NotSpecification<T> : Specification<T>
{
    private readonly Specification<T> _spec;

    public NotSpecification(Specification<T> spec)
    {
        _spec = spec;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expr = _spec.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");
        var body = Expression.Not(Expression.Invoke(expr, parameter));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}
