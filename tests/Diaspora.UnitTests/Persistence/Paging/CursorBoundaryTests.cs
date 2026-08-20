using Diaspora.Persistence.Paging;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Persistence.Paging;

/// <summary>
/// The tie-break, and the guarantee it makes: page through anything and you see every row once.
/// <para>
/// <b>The walk at the bottom of this file is the test that matters.</b> The individual assertions
/// pin the helper's parts; the walk models the predicate every repository writes and pages a whole
/// table through it, asserting that the sequence terminates and that no row is served twice or
/// missed. That is the property the helper exists for, and it is the property that was quietly
/// false past the identifier cap before B1.3.
/// </para>
/// </summary>
public sealed class CursorBoundaryTests
{
    private sealed record Row(long SortKey, Guid Id);

    // ── Building the key ─────────────────────────────────────────────────

    [Fact]
    public void An_empty_page_has_no_boundary()
    {
        CursorBoundary.Key<Row, long>([], row => row.SortKey, row => row.Id)
            .Should().BeEmpty();
    }

    [Fact]
    public void Only_the_rows_sharing_the_last_sort_key_are_carried()
    {
        var boundary = Guid.NewGuid();
        var alsoBoundary = Guid.NewGuid();

        var rows = new List<Row>
        {
            new(30, Guid.NewGuid()),
            new(20, boundary),
            new(20, alsoBoundary)
        };

        var key = CursorBoundary.Key(rows, row => row.SortKey, row => row.Id);

        CursorBoundary.Exclusion(key).Ids
            .Should().BeEquivalentTo([boundary, alsoBoundary]);
    }

    [Fact]
    public void Identifiers_are_carried_forward_while_the_sort_key_stands_still()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var page = CursorBoundary.Key(
            new List<Row> { new(20, first) }, row => row.SortKey, row => row.Id);

        var next = CursorBoundary.Key(
            new List<Row> { new(20, second) },
            row => row.SortKey,
            row => row.Id,
            page,
            previousSortKey: 20L);

        CursorBoundary.Exclusion(next).Ids.Should().BeEquivalentTo([first, second]);
    }

    /// <summary>
    /// A tie group wider than a page is precisely the case that made the first attempt at this
    /// return page one's rows again on page three.
    /// </summary>
    [Fact]
    public void Identifiers_are_dropped_the_moment_the_sort_key_moves()
    {
        var stale = Guid.NewGuid();
        var current = Guid.NewGuid();

        var page = CursorBoundary.Key(
            new List<Row> { new(20, stale) }, row => row.SortKey, row => row.Id);

        var next = CursorBoundary.Key(
            new List<Row> { new(10, current) },
            row => row.SortKey,
            row => row.Id,
            page,
            previousSortKey: 20L);

        CursorBoundary.Exclusion(next).Ids.Should().BeEquivalentTo([current]);
    }

    // ── Overflow ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The behaviour B1.3 corrected.</b> The helper used to truncate the list, which excluded
    /// the first five hundred rows of the tie group and served the rest again on every subsequent
    /// page — from the same boundary, forever. It now abandons the group, which is lossy and
    /// terminating, and is what the documentation always claimed.
    /// </summary>
    [Fact]
    public void A_tie_group_past_the_cap_is_abandoned_rather_than_truncated()
    {
        var rows = Enumerable.Range(0, CursorBoundary.MaxBoundaryIds + 1)
            .Select(_ => new Row(20, Guid.NewGuid()))
            .ToList();

        var key = CursorBoundary.Key(rows, row => row.SortKey, row => row.Id);

        key.Should().Be(CursorBoundary.ExhaustedMarker);

        var exclusion = CursorBoundary.Exclusion(key);

        exclusion.IsExhausted.Should().BeTrue();
        exclusion.Ids.Should().BeEmpty("an abandoned group excludes by comparison, not by list");
    }

    [Fact]
    public void An_abandoned_group_stays_abandoned_while_the_boundary_stands_still()
    {
        var next = CursorBoundary.Key(
            new List<Row> { new(20, Guid.NewGuid()) },
            row => row.SortKey,
            row => row.Id,
            CursorBoundary.ExhaustedMarker,
            previousSortKey: 20L);

        next.Should().Be(
            CursorBoundary.ExhaustedMarker,
            "collecting identifiers again mid-group would exclude one page out of a group whose " +
            "earlier pages were never excluded");
    }

    [Fact]
    public void An_abandoned_group_is_forgotten_as_soon_as_the_boundary_moves()
    {
        var resumed = Guid.NewGuid();

        var next = CursorBoundary.Key(
            new List<Row> { new(10, resumed) },
            row => row.SortKey,
            row => row.Id,
            CursorBoundary.ExhaustedMarker,
            previousSortKey: 20L);

        CursorBoundary.Exclusion(next).Ids.Should().BeEquivalentTo([resumed]);
    }

    // ── Reading a cursor back ────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("not-a-guid,also-not")]
    public void A_malformed_boundary_excludes_nothing_and_is_not_exhausted(string? key)
    {
        var exclusion = CursorBoundary.Exclusion(key);

        exclusion.Ids.Should().BeEmpty();
        exclusion.IsExhausted.Should().BeFalse(
            "excluding nothing repeats a few rows; skipping the group loses them, and a stale " +
            "cursor is an ordinary event rather than a reason to lose anybody's row");
    }

    [Fact]
    public void A_boundary_projects_into_the_callers_identifier_type()
    {
        var id = Guid.NewGuid();

        var key = CursorBoundary.Key(
            new List<Row> { new(20, id) }, row => row.SortKey, row => row.Id);

        CursorBoundary.Exclusion(key, value => value.ToString("N"))
            .Ids.Should().BeEquivalentTo([id.ToString("N")]);
    }

    // ── The property the whole helper exists for ─────────────────────────

    /// <summary>
    /// Pages a table through the exact predicate every repository writes, and asserts the three
    /// things a client cares about: it ends, nothing is served twice, and nothing is missed.
    /// </summary>
    [Theory]
    [InlineData(100, 1, 10)]     // every row ties with nine others
    [InlineData(100, 100, 10)]   // no ties at all
    [InlineData(50, 3, 7)]       // page size that does not divide the tie groups
    [InlineData(40, 2, 40)]      // one page holds everything
    public void Paging_a_table_serves_every_row_exactly_once(
        int rowCount, int distinctKeys, int pageSize)
    {
        var rows = Enumerable.Range(0, rowCount)
            .Select(index => new Row(distinctKeys - (index % distinctKeys), Guid.NewGuid()))
            .OrderByDescending(row => row.SortKey)
            .ToList();

        var served = Walk(rows, pageSize, out var pages);

        served.Should().HaveCount(
            rowCount, "every row is served, and none of them twice");

        served.Distinct().Should().HaveCount(rowCount);
        served.Should().BeEquivalentTo(rows.Select(row => row.Id));

        pages.Should().BeLessThan(
            rowCount + 2, "the walk terminates in a sane number of pages");
    }

    /// <summary>
    /// A tie group large enough to reach the cap, but not to cross it before the rows run out. The
    /// cap costs nothing here, and it is worth pinning that it costs nothing — the cheap way to
    /// "fix" the overflow would be to abandon groups long before anybody is at risk of one.
    /// </summary>
    [Fact]
    public void A_tie_group_that_reaches_the_cap_without_crossing_it_loses_nothing()
    {
        const int Size = CursorBoundary.MaxBoundaryIds + 50;

        var rows = Enumerable.Range(0, Size)
            .Select(_ => new Row(1, Guid.NewGuid()))
            .ToList();

        var served = Walk(rows, pageSize: 100, out _);

        served.Should().BeEquivalentTo(rows.Select(row => row.Id));
    }

    /// <summary>
    /// The pathological case: one tie group so large that paging through it accumulates more
    /// identifiers than a cursor may carry. The contract says this loses the tail of the group and
    /// terminates; the old implementation did neither, serving the rows past the cap again on every
    /// subsequent page, from an unchanging boundary, forever.
    /// </summary>
    [Fact]
    public void An_oversized_tie_group_loses_its_tail_and_still_terminates()
    {
        const int Size = CursorBoundary.MaxBoundaryIds + 200;

        var rows = Enumerable.Range(0, Size)
            .Select(_ => new Row(1, Guid.NewGuid()))
            .ToList();

        var served = Walk(rows, pageSize: 100, out var pages);

        pages.Should().BeLessThan(
            20, "the walk terminates rather than serving the same page forever");

        served.Distinct().Should().HaveCount(
            served.Count, "no row is served twice on the way");

        served.Should().HaveCountLessThan(
            Size, "the tail of a group this size is what the contract gives up");

        served.Should().HaveCountGreaterThanOrEqualTo(
            CursorBoundary.MaxBoundaryIds, "and it gives up only the tail");
    }

    /// <summary>
    /// The predicate, in C#, exactly as the repositories write it in LINQ:
    /// <c>key &lt; boundary OR (NOT exhausted AND key = boundary AND id NOT IN (seen))</c>.
    /// </summary>
    private static List<Guid> Walk(List<Row> rows, int pageSize, out int pages)
    {
        var served = new List<Guid>();

        string? cursorKey = null;
        long? cursorSortKey = null;

        pages = 0;

        while (pages < 1_000)
        {
            pages++;

            var query = rows.AsEnumerable();

            if (cursorSortKey is { } boundary)
            {
                var tie = CursorBoundary.Exclusion(cursorKey);
                var seen = tie.Ids;
                var exhausted = tie.IsExhausted;

                query = query.Where(row =>
                    row.SortKey < boundary
                    || (!exhausted && row.SortKey == boundary && !seen.Contains(row.Id)));
            }

            var page = query
                .OrderByDescending(row => row.SortKey)
                .ThenBy(row => row.Id)
                .Take(pageSize + 1)
                .ToList();

            var hasMore = page.Count > pageSize;

            if (hasMore)
            {
                page.RemoveAt(page.Count - 1);
            }

            if (page.Count == 0)
            {
                break;
            }

            served.AddRange(page.Select(row => row.Id));

            if (!hasMore)
            {
                break;
            }

            cursorKey = CursorBoundary.Key(
                page, row => row.SortKey, row => row.Id, cursorKey, cursorSortKey);

            cursorSortKey = page[^1].SortKey;
        }

        return served;
    }
}
