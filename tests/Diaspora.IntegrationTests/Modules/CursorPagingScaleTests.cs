using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Cursor paging under the conditions that break it: rows sharing a sort key, and tie groups
/// wider than a page.
/// <para>
/// <b>Every one of these listings lost rows before the fix, silently.</b> A cursor resuming with
/// a strict comparison on the sort key alone drops the whole tie group at the boundary, and these
/// timestamps tie readily — the persistence interceptor stamps one instant on every row written
/// in a single unit of work, and two writes inside a clock tick tie anyway. The community feed
/// was worse still: its keyset compared value-converted identifiers, which no provider
/// translates, so page two of every feed was a 500.
/// </para>
/// <para>
/// <b>Two traps live in these fixtures, and both cost real time.</b> Tie groups must not divide
/// evenly into the page size — with groups of two and pages of fifty every boundary falls neatly
/// between groups, and a broken cursor passes by luck. And a fixture cannot set an
/// interceptor-stamped column: whatever it writes to <c>CreatedAt</c> is overwritten at
/// <c>SaveChanges</c>, which turns a carefully spread fixture into one enormous tie group. That
/// is not a reason to avoid such columns — it is the most realistic case there is, and the
/// comment listing below leans on it deliberately.
/// </para>
/// </summary>
public sealed class CursorPagingScaleTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public CursorPagingScaleTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory) => _output = output;

    // ── The community feed ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Three hundred posts in groups of seven sharing a publication instant, walked in pages of
    /// twenty. Seven is deliberately not a divisor of twenty, so tie groups straddle page
    /// boundaries rather than aligning with them.
    /// </summary>
    [Fact]
    public async Task The_feed_walks_a_tied_corpus_without_loss_or_repetition()
    {
        const int PostCount = 300;

        var author = await SeedPostsAsync(PostCount, groupSize: 7, pinnedCount: 0);

        var seen = new HashSet<Guid>();
        var pages = 0;
        FeedCursor? cursor = null;

        using var scope = Factory.Services.CreateScope();
        var posts = scope.ServiceProvider.GetRequiredService<ICommunityPostRepository>();

        while (true)
        {
            var page = await posts.QueryAsync(new PostQueryFilter
            {
                AuthorIds = [author],
                Cursor = cursor,
                Limit = 20
            });

            foreach (var post in page.Items)
            {
                seen.Add(post.Id.Value).Should().BeTrue(
                    "a post returned twice is a cursor with no tie-break");
            }

            pages++;

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = FeedCursor.Decode(page.NextCursor);
            cursor.Should().NotBeNull("the cursor the repository just issued must decode");

            pages.Should().BeLessThan(60, "a cursor that never exhausts would loop here forever");
        }

        seen.Should().HaveCount(PostCount, "every post is seen exactly once");

        _output.WriteLine($"Walked {PostCount} posts in {pages} pages of 20.");
    }

    /// <summary>
    /// <b>The bug that made every feed page after the first a 500.</b> The keyset compared
    /// <c>post.Id.Value.CompareTo(cursor.LastId)</c> — an ordering comparison on a
    /// value-converted strongly-typed id, which SQLite refuses to translate. Reaching page two at
    /// all is the assertion.
    /// </summary>
    [Fact]
    public async Task Page_two_of_a_feed_does_not_throw()
    {
        var author = await SeedPostsAsync(count: 10, groupSize: 3, pinnedCount: 0);

        using var scope = Factory.Services.CreateScope();
        var posts = scope.ServiceProvider.GetRequiredService<ICommunityPostRepository>();

        var first = await posts.QueryAsync(new PostQueryFilter { AuthorIds = [author], Limit = 4 });

        first.NextCursor.Should().NotBeNull();

        var second = await posts.QueryAsync(new PostQueryFilter
        {
            AuthorIds = [author],
            Cursor = FeedCursor.Decode(first.NextCursor),
            Limit = 4
        });

        second.Items.Should().NotBeEmpty();

        second.Items.Select(post => post.Id)
            .Should().NotIntersectWith(first.Items.Select(post => post.Id));
    }

    /// <summary>
    /// A pinned post sorts first regardless of age. A cursor comparing only the timestamp
    /// re-admits it on page two — it is older than the boundary — and it jumps back to the top of
    /// every page thereafter. The cursor has to carry the column the ORDER BY leads with.
    /// </summary>
    [Fact]
    public async Task A_pinned_post_appears_once_rather_than_on_every_page()
    {
        var author = await SeedPostsAsync(count: 60, groupSize: 7, pinnedCount: 3);

        var seen = new HashSet<Guid>();
        FeedCursor? cursor = null;
        var pages = 0;

        using var scope = Factory.Services.CreateScope();
        var posts = scope.ServiceProvider.GetRequiredService<ICommunityPostRepository>();

        while (true)
        {
            var page = await posts.QueryAsync(new PostQueryFilter
            {
                AuthorIds = [author],
                Cursor = cursor,
                Limit = 10,
                PinnedFirst = true
            });

            foreach (var post in page.Items)
            {
                seen.Add(post.Id.Value).Should().BeTrue(
                    "a pinned post must be shown once, not on every page");
            }

            pages++;

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = FeedCursor.Decode(page.NextCursor);
            pages.Should().BeLessThan(30);
        }

        seen.Should().HaveCount(60, "pinning changes the order, not the contents");
    }

    // ── Comments ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The degenerate case, and the realistic one. <c>Comment.CreatedAt</c> is stamped by the
    /// persistence interceptor, so a hundred comments written in one unit of work share a single
    /// instant — one tie group ten pages wide. A cursor carrying only the previous page's
    /// identifiers hands page one's rows back on page three; only one that accumulates while the
    /// boundary holds still walks the thread.
    /// </summary>
    [Fact]
    public async Task A_comment_thread_pages_cleanly_when_every_row_shares_one_timestamp()
    {
        const int CommentCount = 100;

        Factory.EnsureDatabaseCreated();

        var content = ContentReference.Create(CommunityContentType.Post, Guid.NewGuid());
        var authorId = UserId.New();

        using (var seed = Factory.Services.CreateScope())
        {
            var dbContext = seed.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            for (var index = 0; index < CommentCount; index++)
            {
                var comment = Comment.Create(
                    // A fresh reference per comment, equal but not the same instance: an owned
                    // value shared between two aggregates in one DbContext is tracked against
                    // one of them only, and the other writes nulls.
                    ContentReference.Create(content.Type, content.Id),
                    authorId,
                    CommentBody.Create($"Scale comment {index}").Value,
                    parent: null,
                    DateTimeOffset.UtcNow).Value;

                dbContext.Set<Comment>().Add(comment);
            }

            await dbContext.SaveChangesAsync();
        }

        var seen = new HashSet<Guid>();
        FeedCursor? cursor = null;
        var pages = 0;

        using var scope = Factory.Services.CreateScope();
        var comments = scope.ServiceProvider.GetRequiredService<ICommentRepository>();

        while (true)
        {
            var page = await comments.ListRootsAsync(content, cursor, 10, false);

            foreach (var comment in page.Items)
            {
                seen.Add(comment.Id.Value).Should().BeTrue(
                    "the boundary set must accumulate while the timestamp holds still");
            }

            pages++;

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = FeedCursor.Decode(page.NextCursor);
            pages.Should().BeLessThan(40);
        }

        seen.Should().HaveCount(CommentCount);

        _output.WriteLine($"Walked {CommentCount} same-instant comments in {pages} pages.");
    }

    // ── Bookmarks ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bookmarks tie constantly: somebody working through a results page taps five in a second,
    /// and the cursor's sort key is truncated to the millisecond, which widens every tie group
    /// further.
    /// </summary>
    [Fact]
    public async Task A_bookmark_list_pages_without_loss_when_timestamps_tie()
    {
        const int BookmarkCount = 150;

        Factory.EnsureDatabaseCreated();

        var userId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        using (var seed = Factory.Services.CreateScope())
        {
            var dbContext = seed.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            for (var index = 0; index < BookmarkCount; index++)
            {
                var bookmark = CommunityBookmark.Create(
                    userId,
                    ContentReference.Create(CommunityContentType.Post, Guid.NewGuid()),
                    // Threes, so tie groups straddle pages of ten.
                    now.AddSeconds(-(index / 3)));

                dbContext.Set<CommunityBookmark>().Add(bookmark);
            }

            await dbContext.SaveChangesAsync();
        }

        var seen = new HashSet<Guid>();
        FeedCursor? cursor = null;
        var pages = 0;

        using var scope = Factory.Services.CreateScope();
        var bookmarks = scope.ServiceProvider.GetRequiredService<IBookmarkRepository>();

        while (true)
        {
            var page = await bookmarks.ListAsync(userId, null, null, cursor, 10);

            foreach (var bookmark in page.Items)
            {
                seen.Add(bookmark.Id.Value).Should().BeTrue();
            }

            pages++;

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = FeedCursor.Decode(page.NextCursor);
            pages.Should().BeLessThan(50);
        }

        seen.Should().HaveCount(BookmarkCount);
    }

    // ── Cursor compatibility ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A client mid-scroll holds a token issued by the previous build. It has to keep working:
    /// the missing fields default, and paging degrades to the old behaviour rather than throwing
    /// a member of the public out of an infinite scroll.
    /// </summary>
    [Fact]
    public void An_older_two_part_cursor_still_decodes()
    {
        var legacy = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"1700000000000|{Guid.NewGuid():N}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var decoded = FeedCursor.Decode(legacy);

        decoded.Should().NotBeNull();
        decoded!.BoundaryIds.Should().BeEmpty();
        decoded.LeadingFlag.Should().BeFalse();
    }

    [Fact]
    public void A_cursor_round_trips_its_boundary_set_and_flag()
    {
        var original = FeedCursor.FromTimestamp(
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            $"{Guid.NewGuid():N},{Guid.NewGuid():N}",
            leadingFlag: true);

        var decoded = FeedCursor.Decode(original.Encode());

        decoded.Should().NotBeNull();
        decoded!.BoundaryIds.Should().Be(original.BoundaryIds);
        decoded.LeadingFlag.Should().BeTrue();
        decoded.LastId.Should().Be(original.LastId);
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Posts under one fresh author, so each test's assertions are independent of what else the
    /// shared database holds.
    /// </summary>
    private async Task<UserId> SeedPostsAsync(int count, int groupSize, int pinnedCount)
    {
        Factory.EnsureDatabaseCreated();

        var authorId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        for (var index = 0; index < count; index++)
        {
            var post = CommunityPost.Draft(
                authorId,
                PublishingIdentity.Personal(),
                PostBody.Create($"Scale post {index}, long enough to be a valid body.").Value,
                title: null,
                PostCategory.Community,
                UrgencyLevel.Normal,
                ContentAudience.Public,
                PostLocationContext.None,
                // Groups sharing one instant. PublishedAt is domain-set, so unlike CreatedAt it
                // survives the interceptor and the grouping is really there.
                now.AddSeconds(-(index / groupSize))).Value;

            post.Publish(now.AddSeconds(-(index / groupSize)));

            if (index < pinnedCount)
            {
                post.Pin(true);
            }

            dbContext.Set<CommunityPost>().Add(post);
        }

        await dbContext.SaveChangesAsync();

        return authorId;
    }
}
