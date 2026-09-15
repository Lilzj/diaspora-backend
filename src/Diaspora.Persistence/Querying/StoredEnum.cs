using System.Linq.Expressions;

namespace Diaspora.Persistence.Querying;

/// <summary>
/// "At least", "at most" and "in order of" for an enum the model stores by its name.
/// <para>
/// <b>An ordering operator on a name-stored enum compares words.</b> Nearly every enum on this
/// platform is mapped with <c>HasConversion&lt;string&gt;()</c>, and EF translates
/// <c>row.Priority &gt;= floor</c> into a comparison of the stored text — on Postgres and on the
/// SQLite test host alike, so nothing fails. On the live platform, 15 Sep 2026: "at least
/// part-furnished" returned no furnished home, "at least Excellent" returned a phone in Good
/// condition, and the same shape sat in job search, the moderation queue (High sorted after Low),
/// community-post urgency (Emergency never matched "at least Urgent"), the organisation owner count
/// (every Volunteer counted as an Owner) and donation causes (Urgent listed last).
/// </para>
/// <para>
/// The range is therefore taken here, over the enum's declared order, and handed to the query as the
/// values it admits — <c>admitted.Contains(row.Priority)</c>, which translates to set membership
/// over the stored names. An ordering goes through <see cref="Rank{TSource, TEnum}"/>, which becomes
/// a <c>CASE</c> over the names. <c>PersistenceRuleArchitectureTests</c> refuses the raw operators.
/// </para>
/// </summary>
public static class StoredEnum
{
    /// <summary>Every declared value at or above <paramref name="floor"/>, in declared order.</summary>
    public static IReadOnlyList<TEnum> AtLeast<TEnum>(TEnum floor)
        where TEnum : struct, Enum =>
        [.. Declared<TEnum>().Where(value => Comparer<TEnum>.Default.Compare(value, floor) >= 0)];

    /// <summary>Every declared value at or below <paramref name="ceiling"/>, in declared order.</summary>
    public static IReadOnlyList<TEnum> AtMost<TEnum>(TEnum ceiling)
        where TEnum : struct, Enum =>
        [.. Declared<TEnum>().Where(value => Comparer<TEnum>.Default.Compare(value, ceiling) <= 0)];

    /// <summary>
    /// A row's position in the enum's declared order, for <c>OrderBy</c> and <c>ThenBy</c>.
    /// <para>
    /// Built as <c>value == A ? 0 : value == B ? 1 : …</c>, the shape the compiler itself emits for
    /// an enum equality, so EF maps each constant through the property's converter and the database
    /// sorts by meaning. A stored name the enum no longer declares ranks after every known value
    /// rather than failing the query.
    /// </para>
    /// </summary>
    public static Expression<Func<TSource, int>> Rank<TSource, TEnum>(
        Expression<Func<TSource, TEnum>> selector)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(selector);

        var values = Declared<TEnum>();
        var underlying = Enum.GetUnderlyingType(typeof(TEnum));
        var stored = Expression.Convert(selector.Body, underlying);

        Expression rank = Expression.Constant(values.Count);

        for (var index = values.Count - 1; index >= 0; index--)
        {
            rank = Expression.Condition(
                Expression.Equal(
                    stored,
                    Expression.Convert(Expression.Constant(values[index], typeof(TEnum)), underlying)),
                Expression.Constant(index),
                rank);
        }

        return Expression.Lambda<Func<TSource, int>>(rank, selector.Parameters);
    }

    private static List<TEnum> Declared<TEnum>()
        where TEnum : struct, Enum =>
        [.. Enum.GetValues<TEnum>().Distinct().Order(Comparer<TEnum>.Default)];
}
