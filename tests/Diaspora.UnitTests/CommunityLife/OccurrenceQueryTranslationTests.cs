using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Persistence;
using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// Proof that B12.1's hot reads reach the database as SQL.
/// <para>
/// <b>Client evaluation is the failure this file exists to catch, and it is silent.</b> A predicate
/// EF cannot translate is not an error — it is a table load followed by an in-memory filter, correct
/// on ten rows and ruinous on a hundred thousand. `ToQueryString` is the only way to see the
/// difference without a profiler, and asserting on it turns "we checked once" into "it stays true".
/// </para>
/// <para>
/// The queries here are written to mirror the repository's own predicates. Where they drift, the
/// repository is what to trust — but they are short enough, and the columns named specific enough,
/// that drift shows up as a failing assertion rather than as a quietly weaker test.
/// </para>
/// </summary>
public sealed class OccurrenceQueryTranslationTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<IDatabaseContributor> Contributors =
        [new CommunityLifeDatabaseContributor()];

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DiasporaDbContext> _options;
    private readonly ITestOutputHelper _output;

    public OccurrenceQueryTranslationTests(ITestOutputHelper output)
    {
        _output = output;

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DiasporaDbContext>()
            .UseSqlite(_connection)

            // See CommunityLifeModelCacheKeyFactory — the same key as the paging tests, because
            // both build the context with the same single contributor.
            .ReplaceService<IModelCacheKeyFactory, CommunityLifeModelCacheKeyFactory>()
            .Options;

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// The occurrence page: series predicate, window, sequence cursor and ordering, all in SQL.
    /// </summary>
    [Fact]
    public void The_occurrence_page_translates_entirely()
    {
        using var context = NewContext();

        var seriesId = EventSeriesId.New();
        var from = Now;
        var to = Now.AddDays(90);
        const int after = 3;

        var sql = context.Set<CommunityEvent>()
            .AsNoTracking()
            .Where(communityEvent => communityEvent.SeriesId == seriesId
                && communityEvent.Status != EventStatus.Draft
                && communityEvent.ModerationState != CommunityLifeModerationState.Hidden
                && communityEvent.Schedule.EndsAt >= from
                && communityEvent.Schedule.StartsAt <= to
                && communityEvent.SeriesSequence > after)
            .OrderBy(communityEvent => communityEvent.SeriesSequence)
            .Take(21)
            .ToQueryString();

        _output.WriteLine(sql);

        // Every clause in the predicate reached the WHERE, and the ordering reached the ORDER BY.
        sql.Should().Contain("WHERE");
        sql.Should().Contain("\"SeriesId\"");
        sql.Should().Contain("\"SeriesSequence\"");
        sql.Should().Contain("\"ends_at\"");
        sql.Should().Contain("\"starts_at\"");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("LIMIT");

        // The cursor is an integer comparison, not a string one. That is what makes the occurrence
        // page immune to the value-converted-identifier problem every other page works around.
        sql.Should().Contain("\"SeriesSequence\" > ");
    }

    /// <summary>The rewrite list a series edit walks. Three conditions, all indexed, all in SQL.</summary>
    [Fact]
    public void The_rewrite_list_translates_entirely()
    {
        using var context = NewContext();

        var seriesId = EventSeriesId.New();

        var sql = context.Set<CommunityEvent>()
            .Where(communityEvent => communityEvent.SeriesId == seriesId
                && communityEvent.SeriesSequence >= 1
                && communityEvent.Schedule.StartsAt >= Now
                && communityEvent.Status != EventStatus.Cancelled
                && !communityEvent.IsSeriesException)
            .OrderBy(communityEvent => communityEvent.SeriesSequence)
            .ToQueryString();

        _output.WriteLine(sql);

        sql.Should().Contain("\"IsSeriesException\"");
        sql.Should().Contain("\"SeriesSequence\"");
        sql.Should().NotContain("(SELECT", "the rewrite list is one scan of one index, not a join");
    }

    /// <summary>
    /// The expansion idempotency read projects to integers and never materialises an aggregate.
    /// <para>
    /// It runs before every write pass, so a version of it that loaded rows would load a series'
    /// whole history to decide whether to add one date to it.
    /// </para>
    /// </summary>
    [Fact]
    public void The_idempotency_read_projects_to_integers()
    {
        using var context = NewContext();

        var seriesId = EventSeriesId.New();

        var sql = context.Set<CommunityEvent>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(communityEvent => communityEvent.SeriesId == seriesId
                && communityEvent.SeriesSequence != null)
            .Select(communityEvent => communityEvent.SeriesSequence!.Value)
            .ToQueryString();

        _output.WriteLine(sql);

        // Contain rather than StartWith: ToQueryString prefixes the parameter declarations.
        sql.Should().Contain("SELECT \"c\".\"SeriesSequence\"");
        sql.Should().NotContain("\"title\"", "nothing but the ordinal is read");
    }

    /// <summary>
    /// The batched next-date read groups in SQL.
    /// <para>
    /// A group-by that fell back to the client would load every future occurrence of every series on
    /// the page to find twenty minimums.
    /// </para>
    /// </summary>
    [Fact]
    public void The_batched_next_date_read_groups_in_sql()
    {
        using var context = NewContext();

        var wanted = new List<EventSeriesId> { EventSeriesId.New(), EventSeriesId.New() };

        var sql = context.Set<CommunityEvent>()
            .AsNoTracking()
            .Where(communityEvent => communityEvent.SeriesId != null
                && wanted.Contains(communityEvent.SeriesId)
                && communityEvent.Schedule.EndsAt >= Now)
            .GroupBy(communityEvent => communityEvent.SeriesId)
            .Select(group => new
            {
                SeriesId = group.Key,
                StartsAt = group.Min(communityEvent => communityEvent.Schedule.StartsAt)
            })
            .ToQueryString();

        _output.WriteLine(sql);

        sql.Should().Contain("GROUP BY");
        sql.Should().Contain("MIN(");
    }

    /// <summary>
    /// The sweep's work list narrows on status and the materialisation mark, both indexed.
    /// <para>
    /// Asked every quarter of an hour; a scan here is a scan of every series on the platform
    /// ninety-six times a day.
    /// </para>
    /// </summary>
    [Fact]
    public void The_expansion_work_list_translates_entirely()
    {
        using var context = NewContext();

        var horizon = new DateOnly(2027, 2, 1);

        var sql = context.Set<EventSeries>()
            .Where(series =>
                (series.Status == EventSeriesStatus.Draft
                    || series.Status == EventSeriesStatus.Published)
                && (series.MaterialisedThrough == null || series.MaterialisedThrough < horizon))
            .OrderBy(series => series.MaterialisedThrough)
            .Take(200)
            .ToQueryString();

        _output.WriteLine(sql);

        sql.Should().Contain("\"MaterialisedThrough\"");
        sql.Should().Contain("\"Status\"");
        sql.Should().Contain("LIMIT");
    }

    /// <summary>
    /// The waitlist-position count is a count, not a page.
    /// <para>
    /// It runs per waitlisted row, so a version that loaded the queue to measure its length would be
    /// the guest list read an attendee is not allowed to make.
    /// </para>
    /// </summary>
    [Fact]
    public void The_waitlist_position_read_is_a_count()
    {
        using var context = NewContext();

        var eventId = CommunityEventId.New();

        var sql = context.Set<Diaspora.Modules.CommunityLife.Domain.Rsvps.EventRsvp>()
            .AsNoTracking()
            .Where(rsvp => rsvp.EventId == eventId
                && rsvp.Status == RsvpStatus.Waitlisted
                && rsvp.CreatedAt < Now)
            .ToQueryString();

        _output.WriteLine(sql);

        sql.Should().Contain("\"Status\"");
        sql.Should().Contain("\"CreatedAt\"");
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private DiasporaDbContext NewContext() =>
        new(
            _options,
            FakeCurrentUser.Authenticated(UserId.New()),
            new FakeClock(Now),
            eventDispatcher: null,
            Contributors);
}
