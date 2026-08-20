using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Modules.CommunityLife.Persistence;
using Diaspora.Modules.CommunityLife.Persistence.Repositories;
using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// The occurrence page, against a real database.
/// <para>
/// <b>These are the cases the platform has been bitten by before.</b> B9 lost nineteen applicants to
/// a timestamp cursor with no tie-break; B1.3 found a tie group that served the same page forever.
/// The occurrence page sidesteps both by paging on the sequence number — unique within a series, an
/// integer, and ordered identically to the start time — and these tests are what prove it, including
/// the case that breaks a timestamp cursor outright: two dates moved onto the same evening.
/// </para>
/// </summary>
public sealed class OccurrencePagingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<IDatabaseContributor> Contributors =
        [new CommunityLifeDatabaseContributor()];

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DiasporaDbContext> _options;
    private readonly EventSeriesId _seriesId = EventSeriesId.New();

    public OccurrencePagingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DiasporaDbContext>()
            .UseSqlite(_connection)

            // A distinct model cache key, or this class and the soft-delete host share one model
            // and whichever runs first decides what the other sees. See
            // CommunityLifeModelCacheKeyFactory.
            .ReplaceService<IModelCacheKeyFactory, CommunityLifeModelCacheKeyFactory>()
            .Options;

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    /// <summary>An empty series answers an empty page, with no cursor and no more.</summary>
    [Fact]
    public async Task An_empty_series_answers_an_empty_page()
    {
        var page = await PageAsync(limit: 10);

        page.Items.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
        page.NextCursor.Should().BeNull();
    }

    /// <summary>One row is a complete page and carries no continuation.</summary>
    [Fact]
    public async Task One_row_is_a_complete_page()
    {
        await SeedAsync(count: 1);

        var page = await PageAsync(limit: 10);

        page.Items.Should().ContainSingle();
        page.HasMore.Should().BeFalse();
        page.NextCursor.Should().BeNull();
    }

    /// <summary>
    /// Walking the whole series returns every row exactly once, in sequence order.
    /// </summary>
    [Fact]
    public async Task Walking_every_page_returns_every_row_once()
    {
        await SeedAsync(count: 11);

        var seen = new List<int>();
        string? cursor = null;

        for (var guard = 0; guard < 20; guard++)
        {
            var page = await PageAsync(limit: 3, cursor);

            seen.AddRange(page.Items.Select(item => item.SeriesSequence!.Value));
            cursor = page.NextCursor;

            if (!page.HasMore)
            {
                break;
            }
        }

        seen.Should().HaveCount(11);
        seen.Should().OnlyHaveUniqueItems();
        seen.Should().BeInAscendingOrder();
        seen.Should().Equal(Enumerable.Range(1, 11));
    }

    /// <summary>
    /// <b>Two dates moved onto the same instant still page correctly.</b> The case a timestamp
    /// cursor gets wrong: with identical start times and a strict comparison, one of the two would
    /// never be served. The sequence number is unique whatever the clock says.
    /// </summary>
    [Fact]
    public async Task Occurrences_sharing_one_instant_are_all_served()
    {
        var shared = Now.AddDays(30);

        await SeedAsync(count: 4, startFor: _ => shared);

        var seen = new List<int>();
        string? cursor = null;

        for (var guard = 0; guard < 10; guard++)
        {
            var page = await PageAsync(limit: 2, cursor);

            seen.AddRange(page.Items.Select(item => item.SeriesSequence!.Value));
            cursor = page.NextCursor;

            if (!page.HasMore)
            {
                break;
            }
        }

        // Every tied row served exactly once, in order.
        seen.Should().Equal([1, 2, 3, 4]);
    }

    /// <summary>
    /// A malformed cursor answers the first page rather than an error.
    /// <para>
    /// A stale or hand-edited token is an ordinary event — an old app version, a shared link — and
    /// answering an infinite scroll with a 400 is far worse than answering it with the first page.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_malformed_cursor_answers_the_first_page()
    {
        await SeedAsync(count: 5);

        var page = await PageAsync(limit: 3, cursor: "not-a-real-cursor");

        page.Items.Should().HaveCount(3);
        page.Items[0].SeriesSequence.Should().Be(1);
    }

    /// <summary>The window's left edge keeps an occurrence that is running right now.</summary>
    [Fact]
    public async Task The_window_keeps_an_occurrence_already_running()
    {
        await SeedAsync(count: 3, startFor: index => Now.AddDays(index - 1));

        // A window opening now: the first occurrence started yesterday and has not ended.
        var page = await PageAsync(limit: 10, from: Now);

        page.Items.Select(item => item.SeriesSequence).Should().Contain(1);
    }

    /// <summary>The window's right edge excludes anything starting after it.</summary>
    [Fact]
    public async Task The_window_excludes_dates_beyond_its_right_edge()
    {
        await SeedAsync(count: 5);

        var page = await PageAsync(limit: 10, to: Now.AddDays(15));

        page.Items.Should().OnlyContain(item => item.Schedule.StartsAt <= Now.AddDays(15));
        page.Items.Should().NotBeEmpty();
    }

    /// <summary>
    /// A public read hides drafts; a manager's read shows them. The parameter is honoured only for
    /// the caller the handler decided may see them.
    /// </summary>
    [Fact]
    public async Task Drafts_are_hidden_from_a_public_read()
    {
        await SeedAsync(count: 3, publish: false);

        (await PageAsync(limit: 10, includeNonPublic: false)).Items.Should().BeEmpty();
        (await PageAsync(limit: 10, includeNonPublic: true)).Items.Should().HaveCount(3);
    }

    /// <summary>Sequences already written are reported, so expansion never writes a date twice.</summary>
    [Fact]
    public async Task The_written_sequences_are_reported_for_idempotency()
    {
        await SeedAsync(count: 4);

        await using var context = NewContext();
        var repository = new CommunityEventRepository(context);

        var sequences = await repository.FindOccurrenceSequencesAsync(_seriesId);

        sequences.Should().BeEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>The next date is the soonest one that has not finished and is not called off.</summary>
    [Fact]
    public async Task The_next_occurrence_skips_cancelled_dates()
    {
        await SeedAsync(count: 3);

        await using (var write = NewContext())
        {
            var first = await write.Set<CommunityEvent>()
                .FirstAsync(row => row.SeriesSequence == 1);

            first.Cancel(Now, null, "Called off");
            await write.SaveChangesAsync();
        }

        await using var context = NewContext();
        var repository = new CommunityEventRepository(context);

        var next = await repository.FindNextOccurrenceAsync(_seriesId, Now);

        next!.SeriesSequence.Should().Be(2);
    }

    /// <summary>
    /// The batched next-date read answers for several series at once.
    /// <para>
    /// The N+1 guard for the series page: twenty cards must not be twenty round trips. Two series
    /// are seeded, one of them with its soonest date cancelled, so the test also proves the batched
    /// form applies the same skip the single one does.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_batched_next_read_answers_for_several_series()
    {
        var second = EventSeriesId.New();

        await SeedAsync(count: 3);
        await SeedAsync(count: 2, seriesId: second, slugPrefix: "other");

        await using (var write = NewContext())
        {
            var first = await write.Set<CommunityEvent>()
                .FirstAsync(row => row.SeriesId == _seriesId && row.SeriesSequence == 1);

            first.Cancel(Now, null, "Called off");
            await write.SaveChangesAsync();
        }

        await using var context = NewContext();
        var repository = new CommunityEventRepository(context);

        var next = await repository.FindNextOccurrencesAsync([_seriesId, second], Now);

        next.Should().HaveCount(2);
        next[_seriesId].SeriesSequence.Should().Be(2, "the soonest was cancelled");
        next[second].SeriesSequence.Should().Be(1);
    }

    /// <summary>An empty request costs no query and answers empty.</summary>
    [Fact]
    public async Task The_batched_next_read_answers_empty_for_no_series()
    {
        await using var context = NewContext();
        var repository = new CommunityEventRepository(context);

        (await repository.FindNextOccurrencesAsync([], Now)).Should().BeEmpty();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private DiasporaDbContext NewContext() =>
        new(
            _options,
            FakeCurrentUser.Authenticated(UserId.New()),
            new FakeClock(Now),
            eventDispatcher: null,
            Contributors);

    private async Task<Diaspora.SharedKernel.Pagination.CursorPagedResult<CommunityEvent>> PageAsync(
        int limit,
        string? cursor = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        bool includeNonPublic = true)
    {
        await using var context = NewContext();
        var repository = new CommunityEventRepository(context);

        return await repository.FindOccurrencesAsync(
            _seriesId, from, to, includeNonPublic, cursor, limit);
    }

    private async Task SeedAsync(
        int count,
        Func<int, DateTimeOffset>? startFor = null,
        bool publish = true,
        EventSeriesId? seriesId = null,
        string slugPrefix = "weekly-meetup")
    {
        await using var context = NewContext();

        for (var index = 1; index <= count; index++)
        {
            var startsAt = startFor?.Invoke(index) ?? Now.AddDays(7 * index);

            var occurrence = CommunityEvent.Draft(
                CommunityEventId.New(),
                organizationId: null,
                UserId.From(Guid.NewGuid()),
                CommunityLifeTitle.Create("Weekly Meetup").Value,
                CommunityLifeSlug.FromText($"{slugPrefix}-{index}"),
                CommunityLifeDescription.Create("A weekly gathering.", null).Value,
                "community-meeting",
                EventSchedule.Create(startsAt, startsAt.AddHours(2), "Europe/London").Value,
                CommunityLifeLocationContext.Create("GB", null, "London").Value,
                EventVenue.Create(name: "Community Hall").Value,
                EventAttendanceMode.InPerson,
                Now).Value;

            occurrence.AttachToSeries(seriesId ?? _seriesId, index);

            if (publish)
            {
                occurrence.Publish(Now);
            }

            context.Add(occurrence);
        }

        await context.SaveChangesAsync();
    }
}
