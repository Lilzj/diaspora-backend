using System.Linq.Expressions;

namespace Diaspora.SharedKernel.Specifications;

[Obsolete(
    "Retired with Specification<T> — B2.5 DDD-02. See docs/platform/b1.3-persistence-rule-guide.md.",
    error: true,
    DiagnosticId = "DSP3006")]
internal sealed class OrSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = _left.ToExpression();
        var rightExpr = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");
        var body = Expression.OrElse(
            Expression.Invoke(leftExpr, parameter),
            Expression.Invoke(rightExpr, parameter));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}
