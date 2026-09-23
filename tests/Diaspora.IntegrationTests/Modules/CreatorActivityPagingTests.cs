using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Creator.Domain.Activity;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Paging a creator's history without losing any of it.
/// <para>
/// <b>The order has two components and the cursor had one.</b> Entries are read newest first by
/// <c>OccurredAt</c> and then by <c>Id</c>, because one publish raises several recognised actions
/// and they share a timestamp to the tick. The next page asked for everything strictly older than
/// the last row's instant, so every sibling of that row was skipped — not shown on the page that
/// ended there, and gone from the one after it.
/// </para>
/// <para>
/// The repository's own comment said the tiebreak existed "so the cursor is stable, an unstable
/// order would drop or repeat rows across pages". It described a cursor the route never sent.
/// </para>
/// <para>
/// <b>These run against a database, which is the point.</b> The first repair was the textbook
/// keyset — <c>key &lt; @after OR (key == @after AND EF.Property&lt;Guid&gt;(entry, "Id") &lt;
/// @lastId)</c> — copied from the sibling earnings repository, which had carried it since it
/// shipped. It compiles, it sorts correctly, and it throws
/// <c>InvalidCastException: Object must implement IConvertible</c> on the first cursor, because
/// these identifiers are value-converted and EF hands the raw Guid parameter to the converter.
/// Nothing had caught it there, because nothing paged it against a database.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class CreatorActivityPagingTests : PooledIdentityTestBase
{
    private static readonly DateTimeOffset Moment =
        new(2026, 9, 20, 14, 45, 1, TimeSpan.Zero);

    public CreatorActivityPagingTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    /// <summary>
    /// Six entries sharing one instant, read two at a time. Every one of them has to appear, once.
    /// </summary>
    [Fact]
    public async Task Entries_sharing_an_instant_are_not_lost_across_a_page_boundary()
    {
        var creatorId = CreatorId.New();

        var written = await SeedAsync(creatorId, atTheSameInstant: 6);

        var seen = await PageThroughAsync(creatorId, pageSize: 2);

        seen.Should().BeEquivalentTo(
            written, "nothing may be skipped because it shared a timestamp");
        seen.Should().OnlyHaveUniqueItems("and nothing may be shown twice");
    }

    /// <summary>
    /// The ordinary case, still ordinary. A fix for ties that stalled on distinct instants would
    /// page for ever, and a fix that repeated the boundary row would never terminate.
    /// </summary>
    [Fact]
    public async Task Entries_at_distinct_instants_page_exactly_once_each()
    {
        var creatorId = CreatorId.New();

        var written = await SeedAsync(creatorId, atDistinctInstants: 5);

        var seen = await PageThroughAsync(creatorId, pageSize: 2);

        seen.Should().BeEquivalentTo(written);
        seen.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// A tie group longer than the page, so the boundary falls inside it twice over — which is the
    /// case a cursor carrying only the last page's identifiers gets wrong, handing page one's rows
    /// back on page three.
    /// </summary>
    [Fact]
    public async Task A_tie_group_longer_than_a_page_is_paged_through_completely()
    {
        var creatorId = CreatorId.New();

        var written = await SeedAsync(creatorId, atTheSameInstant: 7, atDistinctInstants: 3);

        var seen = await PageThroughAsync(creatorId, pageSize: 3);

        seen.Should().BeEquivalentTo(written);
        seen.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// <b>A token this platform did not issue serves the first page.</b> ADR-017's trade: a stale
    /// or truncated cursor is an ordinary event — an old app version, a shared link — and
    /// answering a browse screen with an error is worse than answering it with rows the client has
    /// already seen.
    /// </summary>
    [Fact]
    public async Task A_cursor_this_platform_did_not_issue_serves_the_first_page()
    {
        var creatorId = CreatorId.New();

        await SeedAsync(creatorId, atDistinctInstants: 3);

        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<ICreatorActivityRepository>()
            .ListForCreatorAsync(creatorId, "not-a-cursor-at-all", 2, CancellationToken.None);

        page.Entries.Should().HaveCount(2);
        page.HasMore.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Walks every page the way a client does — handing back the token it was given, and nothing
    /// else — and returns what it was shown.
    /// <para>
    /// Bounded: a cursor that failed to advance would otherwise spin here rather than fail, and a
    /// test that hangs says less than one that fails.
    /// </para>
    /// </summary>
    private async Task<List<Guid>> PageThroughAsync(CreatorId creatorId, int pageSize)
    {
        var seen = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 20; page++)
        {
            using var scope = Factory.Services.CreateScope();

            var read = await scope.ServiceProvider
                .GetRequiredService<ICreatorActivityRepository>()
                .ListForCreatorAsync(creatorId, cursor, pageSize, CancellationToken.None);

            seen.AddRange(read.Entries.Select(entry => entry.Id.Value));

            if (!read.HasMore)
            {
                return seen;
            }

            cursor = read.NextCursor;
        }

        throw new InvalidOperationException(
            "The cursor never reached the end, which means it stopped advancing.");
    }

    private async Task<List<Guid>> SeedAsync(
        CreatorId creatorId, int atTheSameInstant = 0, int atDistinctInstants = 0)
    {
        using var scope = Factory.Services.CreateScope();

        var entries = scope.ServiceProvider.GetRequiredService<ICreatorActivityRepository>();
        var userId = Guid.NewGuid();
        var written = new List<Guid>();

        for (var index = 0; index < atTheSameInstant; index++)
        {
            var entry = CreatorActivityEntry.Record(
                CreatorActivityEntryId.New(),
                creatorId,
                userId,
                "guide.published",
                weight: 1,
                sourceReference: $"same-{creatorId.Value:N}-{index}",
                occurredAt: Moment);

            entries.Add(entry);
            written.Add(entry.Id.Value);
        }

        for (var index = 0; index < atDistinctInstants; index++)
        {
            var entry = CreatorActivityEntry.Record(
                CreatorActivityEntryId.New(),
                creatorId,
                userId,
                "guide.published",
                weight: 1,
                sourceReference: $"apart-{creatorId.Value:N}-{index}",
                occurredAt: Moment.AddMinutes(-(index + 1)));

            entries.Add(entry);
            written.Add(entry.Id.Value);
        }

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .SaveChangesAsync(CancellationToken.None);

        return written;
    }
}
