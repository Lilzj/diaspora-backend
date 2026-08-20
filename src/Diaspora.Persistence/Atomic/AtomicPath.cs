using System.Linq.Expressions;

namespace Diaspora.Persistence.Atomic;

/// <summary>
/// Turns a property expression into the dotted path the executor resolves against EF's model.
/// <para>
/// <b>Expressions rather than strings, and the reason is the rule this phase also enforces.</b>
/// A guarded update names columns, and naming them with literals — <c>"Availability.ReservedUnits"</c>
/// — puts the mapping beyond the reach of rename refactoring and beyond the compiler entirely. A
/// lambda costs nothing at the call site and breaks the build the day somebody renames the property.
/// </para>
/// <para>
/// <b>These lambdas are never handed to EF.</b> They are parsed here, by walking the member chain,
/// and only the resulting path reaches the query pipeline — so none of the translation limits that
/// govern expression trees apply to them. That distinction matters enough that
/// <c>PersistenceRuleArchitectureTests</c> asserts it: an update descriptor is not a query.
/// </para>
/// </summary>
public static class AtomicPath
{
    /// <summary>
    /// The dotted path a property expression names, relative to the aggregate root.
    /// <para>
    /// <c>listing =&gt; listing.Availability.ReservedUnits</c> becomes
    /// <c>Availability.ReservedUnits</c>. An owned value object contributes one segment, which is
    /// exactly what EF's metadata walk expects on the other side.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The expression is anything other than a chain of property accesses rooted at the parameter —
    /// a method call, an index, a computed value. There is no column behind those, and failing here
    /// names the offending expression while somebody is still writing it.
    /// </exception>
    public static string Of<TEntity, TValue>(Expression<Func<TEntity, TValue>> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var segments = new List<string>();
        var node = Unwrap(property.Body);

        while (node is MemberExpression member)
        {
            segments.Add(member.Member.Name);
            node = Unwrap(member.Expression);
        }

        if (node is not ParameterExpression || segments.Count == 0)
        {
            throw new ArgumentException(
                $"'{property}' does not name a property of {typeof(TEntity).Name}. An atomic update " +
                "addresses mapped columns, so the expression must be a chain of property accesses " +
                "rooted at the parameter.",
                nameof(property));
        }

        segments.Reverse();

        return string.Join('.', segments);
    }

    /// <summary>
    /// Strips the conversions the compiler inserts around a value-typed member access, which is
    /// every <c>int</c> property reached through an <c>Expression&lt;Func&lt;T, object&gt;&gt;</c>.
    /// </summary>
    private static Expression? Unwrap(Expression? node) =>
        node is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? unary.Operand
            : node;
}
