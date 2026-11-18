using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Persistence;
using Diaspora.Modules.Conversations.Persistence.Repositories;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Diaspora.UnitTests.Performance;

/// <summary>
/// A ceiling on the number of queries the platform's two hottest reads may issue.
/// <para>
/// <b>BBL-005 and BARCH-005 are one finding, and the second one names the remedy this file
/// is.</b> Both cite the same two lines — the feed's trust lookup and the conversation list's last
/// message — and the architecture report adds the part a fix alone does not address: <em>77
/// candidate N+1 sites were found; the budget is what stops the 78th</em>. The loops are gone. A
/// test that only proved they were gone would be satisfied by the code as it is today and say
/// nothing about tomorrow, which is how both of these got written in the first place.
/// </para>
/// <para>
/// <b>Counted against a real <c>DiasporaDbContext</c> over SQLite rather than through the HTTP
/// stack.</b> The finding asks for an endpoint-level assertion, and an endpoint would need fifty
/// registered members to mean anything — several requests each, a slow test, and a count dominated
/// by identity and authorisation queries that have nothing to do with the finding. What the
/// finding is actually about is whether <em>one</em> read scales with the size of the page, and
/// that is exactly what a repository answers. The number here is 1, not 6, because at this level
/// there is nothing else to spend a query on.
/// </para>
/// </summary>
public sealed class HotReadQueryBudgetTests : IDisposable
{
    /// <summary>
    /// The page size the audit reasoned about: fifty posts from mostly distinct authors, which was
    /// fifty sequential round trips to Trust before a single post could be ranked.
    /// </summary>
    private const int APageOfAuthors = 50;

    /// <summary>The bound the conversation list's own comment claimed, and which is the finding.</summary>
    private const int APageOfConversations = 100;

    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<IDatabaseContributor> Contributors =
        [new TrustDatabaseContributor(), new ConversationsDatabaseContributor()];

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DiasporaDbContext> _options;
    private readonly QueryCountingInterceptor _queries = new();

    public HotReadQueryBudgetTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DiasporaDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_queries)
            .ReplaceService<IModelCacheKeyFactory, QueryBudgetModelCacheKeyFactory>()
            .Options;

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    // ── The feed ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The budget: one query for a whole page of authors.</b> Before, this was one per distinct
    /// author — <c>FeedComposer</c> looping over the page because <c>ITrustService</c> had only a
    /// single-subject method to loop over.
    /// </summary>
    [Fact]
    public async Task The_trust_levels_for_a_page_of_authors_cost_one_query()
    {
        var authors = await SeedTrustProfilesAsync(APageOfAuthors);

        _queries.Reset();

        var tiers = await Profiles().GetTiersAsync(authors);

        _queries.Count.Should().Be(
            1,
            "a page of {0} authors is one read, not {0}. Statements:\n{1}",
            APageOfAuthors,
            _queries.Describe());

        tiers.Should().HaveCount(
            APageOfAuthors, "every seeded author has a profile and a tier to report");

        FiltersInTheDatabase(_queries.Statements[0], "UserId");
    }

    /// <summary>
    /// <b>One statement is the wrong answer if it is <c>SELECT * FROM TrustProfiles</c>.</b> A
    /// count alone cannot tell a batched read from a table load filtered in memory, and the second
    /// one is worse than the loop it replaced: the budget would read 1 and the page would get
    /// slower as the platform grows. EF refuses to client-evaluate a top-level <c>Where</c> and
    /// throws instead, so this cannot happen silently today — but that is a property of EF's
    /// current behaviour rather than of this code, and the assertion costs one line.
    /// <para>
    /// It also catches the narrower mistake the strongly typed identifiers make easy: a predicate
    /// over a value-converted property that compiles, reads correctly, and quietly stops naming
    /// the column.
    /// </para>
    /// </summary>
    private static void FiltersInTheDatabase(string statement, string column)
    {
        statement.Should().Contain(
            "WHERE",
            "a read of named rows filters in SQL. Statement: {0}", statement);

        statement.Should().Contain(
            column,
            "the filter names the column the identifiers are stored in, or it is filtering on " +
            "something else. Statement: {0}",
            statement);
    }

    /// <summary>
    /// <b>The budget does not move when the page does.</b> A ceiling that happened to be met at
    /// fifty and rose at a hundred would be describing the test rather than the query, and a feed
    /// page size is a configuration value somebody will change.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(APageOfAuthors)]
    [InlineData(200)]
    public async Task The_trust_budget_does_not_grow_with_the_page(int authorCount)
    {
        var authors = await SeedTrustProfilesAsync(authorCount);

        _queries.Reset();

        await Profiles().GetTiersAsync(authors);

        _queries.Count.Should().Be(
            1, "the query is one statement at any page size. Statements:\n{0}", _queries.Describe());
    }

    /// <summary>
    /// <b>No authors is no query, not one that asks about nobody.</b> EF would translate an empty
    /// <c>IN</c> to <c>WHERE 1 = 0</c> — a correct round trip that can only return nothing — and a
    /// feed of posts whose authors are all absent is an ordinary page rather than an error.
    /// </summary>
    [Fact]
    public async Task No_authors_costs_no_query_at_all()
    {
        await SeedTrustProfilesAsync(3);

        _queries.Reset();

        var tiers = await Profiles().GetTiersAsync([]);

        _queries.Count.Should().Be(0, "there is nothing to ask");
        tiers.Should().BeEmpty();
    }

    /// <summary>
    /// A member with no profile is absent rather than invented, and asking about one does not cost
    /// a second query to find that out.
    /// </summary>
    [Fact]
    public async Task A_member_with_no_profile_is_absent_and_costs_nothing_extra()
    {
        var known = await SeedTrustProfilesAsync(3);
        var stranger = UserId.New();

        _queries.Reset();

        var tiers = await Profiles().GetTiersAsync([.. known, stranger]);

        _queries.Count.Should().Be(1, "statements:\n{0}", _queries.Describe());
        tiers.Should().HaveCount(3);
        tiers.Should().NotContainKey(stranger);
    }

    // ── The conversation list ────────────────────────────────────────────

    /// <summary>
    /// <b>The budget: one query for the last message of every thread on the page.</b> The loop this
    /// replaces was honest about itself — bounded, primary-key lookups, cheaper on a repeat because
    /// the change tracker held it — and the bound was a hundred round trips on the second-most-used
    /// screen, which is what the finding is.
    /// </summary>
    [Fact]
    public async Task The_last_messages_for_a_page_of_conversations_cost_one_query()
    {
        var ids = await SeedMessagesAsync(APageOfConversations);

        _queries.Reset();

        var messages = await NewMessageRepository().GetManyAsync(ids);

        _queries.Count.Should().Be(
            1,
            "a page of {0} conversations is one read, not {0}. Statements:\n{1}",
            APageOfConversations,
            _queries.Describe());

        messages.Should().HaveCount(APageOfConversations);

        FiltersInTheDatabase(_queries.Statements[0], "Id");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(APageOfConversations)]
    public async Task The_message_budget_does_not_grow_with_the_page(int messageCount)
    {
        var ids = await SeedMessagesAsync(messageCount);

        _queries.Reset();

        await NewMessageRepository().GetManyAsync(ids);

        _queries.Count.Should().Be(
            1, "the query is one statement at any page size. Statements:\n{0}", _queries.Describe());
    }

    [Fact]
    public async Task No_last_messages_costs_no_query_at_all()
    {
        await SeedMessagesAsync(3);

        _queries.Reset();

        var messages = await NewMessageRepository().GetManyAsync([]);

        _queries.Count.Should().Be(0, "a page of empty threads asks nothing");
        messages.Should().BeEmpty();
    }

    /// <summary>
    /// <b>A repeated identifier is one row, not two, and still one query.</b> Two threads can share
    /// a last message only rarely, but the caller passes what the page gives it, and a batch that
    /// threw on a duplicate key would be a worse seam than the loop it replaced.
    /// </summary>
    [Fact]
    public async Task A_repeated_identifier_is_still_one_query_and_one_row()
    {
        var ids = await SeedMessagesAsync(2);

        _queries.Reset();

        var messages = await NewMessageRepository().GetManyAsync([ids[0], ids[1], ids[0]]);

        _queries.Count.Should().Be(1, "statements:\n{0}", _queries.Describe());
        messages.Should().HaveCount(2);
    }

    // ── The counter itself ───────────────────────────────────────────────

    /// <summary>
    /// <b>The budget is an assertion that a number is small, which an interceptor that counted
    /// nothing would satisfy forever.</b> So the counter is shown to count: a loop of reads is
    /// reported as a loop of reads, with the same code under it that the budgets above trust.
    /// <para>
    /// This is the shape of the defect being guarded against, written out — if the two budgets
    /// above ever read 1 because the interceptor stopped seeing SQL, this one reads 0 and says so.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_counter_counts_a_loop_as_a_loop()
    {
        var authors = await SeedTrustProfilesAsync(5);
        var profiles = Profiles();

        _queries.Reset();

        foreach (var author in authors)
        {
            await profiles.GetByUserReadOnlyAsync(author);
        }

        _queries.Count.Should().Be(
            5,
            "five single-subject reads are five statements, which is what the hot paths used to " +
            "do and what the budgets above exist to forbid. Statements:\n{0}",
            _queries.Describe());
    }

    // ── Seeding ──────────────────────────────────────────────────────────

    private async Task<List<UserId>> SeedTrustProfilesAsync(int count)
    {
        await using var context = NewContext();

        var userIds = new List<UserId>(count);

        for (var index = 0; index < count; index++)
        {
            var userId = UserId.New();
            userIds.Add(userId);
            context.Set<TrustProfile>().Add(TrustProfile.Open(userId, Now.AddDays(-index)));
        }

        await context.SaveChangesAsync();

        return userIds;
    }

    private async Task<List<ConversationMessageId>> SeedMessagesAsync(int count)
    {
        await using var context = NewContext();

        var ids = new List<ConversationMessageId>(count);

        for (var index = 0; index < count; index++)
        {
            var id = ConversationMessageId.New();
            ids.Add(id);

            context.Set<ConversationMessage>().Add(ConversationMessage.System(
                id,
                ConversationId.New(),
                SystemMessageKind.ConversationStarted,
                $"thread {index}",
                Now.AddMinutes(-index)));
        }

        await context.SaveChangesAsync();

        return ids;
    }

    // ── Plumbing ─────────────────────────────────────────────────────────

    /// <summary>
    /// A repository over a fresh context, so a budget never passes because the change tracker was
    /// still holding the rows the seed just wrote.
    /// </summary>
    private TrustProfileRepository Profiles() => new(NewContext());

    private ConversationMessageRepository NewMessageRepository() => new(NewContext());

    private DiasporaDbContext NewContext() =>
        new(
            _options,
            FakeCurrentUser.Authenticated(UserId.New()),
            new FakeClock(Now),
            eventDispatcher: null,
            Contributors);

    public void Dispose()
    {
        _connection.Dispose();
    }
}
