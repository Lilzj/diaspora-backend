using System.Linq.Expressions;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Repositories;

/// <summary>
/// The one place this module turns a query into a page.
/// <para>
/// <b>Written once because six repositories page the same way, and a boundary rule copied six times
/// is a boundary rule that is wrong in one of them.</b> Every page in this module sorts newest-first
/// on a timestamp and ties on the identifier, so the applying of the cursor, the trim, the
/// has-more test and the next-token construction are all identical — and every one of them is a
/// place the tie group could be dropped, repeated or served for ever.
/// </para>
/// <para>
/// The caller supplies the two projections the boundary needs — the sort key and the identifier's
/// Guid — because those are the only parts that differ, and passing them as expressions keeps the
/// comparison inside the query rather than pulling the page into memory to sort it.
/// </para>
/// </summary>
internal static class CursorPage
{
    /// <summary>
    /// Applies an incoming cursor to a timestamp-ordered query.
    /// <para>
    /// <b>Strictly before the boundary instant, plus whatever remains of the tie group at it.</b> A
    /// plain <c>&lt; boundary</c> drops every row sharing the instant, and the instant is shared
    /// constantly — <c>CreatedAt</c> is stamped by the shared interceptor, so a whole unit of work
    /// carries one value. The identifier cannot be compared in SQL because it is value-converted, so
    /// the cursor carries the ones already served and the query excludes them by <c>Contains</c>,
    /// which every provider translates.
    /// </para>
    /// </summary>
    public static IQueryable<T> Apply<T, TId>(
        IQueryable<T> query,
        CommunityProgrammeCursor? cursor,
        Func<Guid, TId> idFactory,
        Expression<Func<T, DateTimeOffset>> sortKey,
        Expression<Func<T, TId>> identifier)
    {
        if (cursor?.AsTimestamp() is not { } boundary)
        {
            return query;
        }

        var exclusion = CursorBoundary.Exclusion(cursor.BoundaryKey, idFactory);
        var seen = exclusion.Ids;
        var exhausted = exclusion.IsExhausted;

        // Composed as an expression rather than written inline, so the same predicate serves every
        // aggregate without each repository restating the three-part boundary condition.
        var parameter = Expression.Parameter(typeof(T), "row");
        var sort = Replace(sortKey, parameter);
        var id = Replace(identifier, parameter);

        var boundaryConstant = Expression.Constant(boundary, typeof(DateTimeOffset));
        var before = Expression.LessThan(sort, boundaryConstant);
        var atBoundary = Expression.Equal(sort, boundaryConstant);

        var notExhausted = Expression.Not(Expression.Constant(exhausted, typeof(bool)));

        var containsMethod = typeof(Enumerable)
            .GetMethods()
            .First(method => method.Name == nameof(Enumerable.Contains) && method.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TId));

        var notSeen = Expression.Not(
            Expression.Call(
                containsMethod,
                Expression.Constant(seen, typeof(IEnumerable<TId>)),
                id));

        var tail = Expression.AndAlso(Expression.AndAlso(atBoundary, notExhausted), notSeen);
        var predicate = Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(before, tail), parameter);

        return query.Where(predicate);
    }

    /// <summary>
    /// Takes one more row than asked for, trims it, and builds the next token from what is left.
    /// <para>
    /// One extra row rather than a second <c>COUNT</c>: the count is a second scan of the same
    /// predicate, and the answer it gives — "are there more" — is exactly what the extra row already
    /// says.
    /// </para>
    /// </summary>
    public static async Task<CursorPagedResult<T>> MaterialiseAsync<T>(
        IQueryable<T> query,
        CommunityProgrammeCursor? cursor,
        int limit,
        Func<T, DateTimeOffset> sortKey,
        Func<T, Guid> identifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sortKey);
        ArgumentNullException.ThrowIfNull(identifier);

        var rows = await query.Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? CommunityProgrammeCursor.FromTimestamp(
                    sortKey(rows[^1]),
                    CursorBoundary.Key(
                        rows,
                        sortKey,
                        identifier,
                        cursor?.BoundaryKey,
                        cursor?.AsTimestamp()))
                .Encode()
            : null;

        return CursorPagedResult<T>.Create(rows, next, hasMore);
    }

    private static Expression Replace<T, TResult>(
        Expression<Func<T, TResult>> source, ParameterExpression parameter) =>
        new ParameterReplacer(source.Parameters[0], parameter).Visit(source.Body)!;

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterReplacer(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
