using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Paging a member's points history without repeating it.
/// <para>
/// <b>The boundary was inclusive and nothing was excluded at it.</b> The read compared
/// <c>OccurredAt &lt;= before</c> and took no identifiers, so every page began with the entry the
/// previous page ended on. Worse, a page whose entries all shared one instant — which a batch of
/// awards written in one unit of work produces — returned itself, and the client reads a full page
/// as "there is more", so that history scrolled for ever without advancing.
/// </para>
/// <para>
/// Its own neighbour in the same file, <c>/rewards/conversions</c>, has taken <c>excludeIds</c>
/// since it shipped, as do deposits, withdrawals, payments and placements. This route was the one
/// that never got it, and the client's own comment recorded the gap as "a property of the contract"
/// for as long as it stood.
/// </para>
/// <para>
/// <b>Over HTTP on purpose.</b> Half the defect is binding: a route that does not bind
/// <c>excludeIds</c> ignores it silently and answers exactly as it did before, which is how the
/// sibling creator route spent its whole life answering page one. Calling the handler directly
/// would prove the filter and miss that entirely.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class RewardHistoryPagingTests : PooledIdentityTestBase
{
    private static readonly DateTimeOffset Moment =
        new(2026, 9, 20, 11, 30, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RewardHistoryPagingTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    /// <summary>
    /// Six entries sharing one instant, read two at a time. Every one appears, once, and the walk
    /// ends.
    /// </summary>
    [Fact]
    public async Task Entries_sharing_an_instant_are_not_repeated_across_a_page_boundary()
    {
        var userId = await SignInAsync();

        var written = await GrantAsync(userId, atTheSameInstant: 6);

        var seen = await PageThroughAsync(pageSize: 2);

        seen.Should().BeEquivalentTo(written, "every entry has to appear");
        seen.Should().OnlyHaveUniqueItems("and none of them twice");
    }

    /// <summary>
    /// The ordinary case. An inclusive boundary re-served the last row of every page here too —
    /// one duplicate per page, quietly, on a history with no ties in it at all.
    /// </summary>
    [Fact]
    public async Task Entries_at_distinct_instants_are_shown_exactly_once_each()
    {
        var userId = await SignInAsync();

        var written = await GrantAsync(userId, atDistinctInstants: 5);

        var seen = await PageThroughAsync(pageSize: 2);

        seen.Should().BeEquivalentTo(written);
        seen.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// A tie group longer than the page, so the boundary falls inside it twice over — the case a
    /// client that carried only the last page's identifiers gets wrong, handing page one's entries
    /// back on page three.
    /// </summary>
    [Fact]
    public async Task A_tie_group_longer_than_a_page_is_paged_through_completely()
    {
        var userId = await SignInAsync();

        var written = await GrantAsync(userId, atTheSameInstant: 7, atDistinctInstants: 3);

        var seen = await PageThroughAsync(pageSize: 3);

        seen.Should().BeEquivalentTo(written);
        seen.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// <b>The binding, on its own.</b> A route that does not bind <c>excludeIds</c> ignores it and
    /// answers exactly as it did before — which every assertion above would still pass if the
    /// client simply never reached a boundary. This one names an entry and requires it gone.
    /// </summary>
    [Fact]
    public async Task An_excluded_entry_is_actually_excluded()
    {
        var userId = await SignInAsync();

        var written = await GrantAsync(userId, atTheSameInstant: 3);
        var unwanted = written[0];

        var seen = await ReadPageAsync(before: Moment, excludeIds: [unwanted], take: 25);

        seen.Should().NotContain(unwanted, "the route has to bind what the client sends it");
        seen.Should().HaveCount(2);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Walks every page the way the client does: the last entry's instant as the boundary, plus
    /// the identifiers already shown at that instant — carried forward while the boundary has not
    /// moved, because a tie group wider than a page is still one boundary.
    /// <para>
    /// Bounded, because a cursor that stops advancing should fail this test rather than hang it.
    /// </para>
    /// </summary>
    private async Task<List<Guid>> PageThroughAsync(int pageSize)
    {
        var seen = new List<Guid>();

        DateTimeOffset? before = null;
        var excludeIds = new List<Guid>();

        for (var page = 0; page < 20; page++)
        {
            var rows = await ReadRowsAsync(before, excludeIds, pageSize);

            if (rows.Count == 0)
            {
                return seen;
            }

            seen.AddRange(rows.Select(row => row.EntryId));

            var boundary = rows[^1].OccurredAt;

            if (before != boundary)
            {
                excludeIds = [];
                before = boundary;
            }

            excludeIds.AddRange(
                rows.Where(row => row.OccurredAt == boundary).Select(row => row.EntryId));
        }

        throw new InvalidOperationException(
            "The history never reached its end, which means the boundary stopped moving.");
    }

    private async Task<List<Guid>> ReadPageAsync(
        DateTimeOffset? before, IReadOnlyList<Guid> excludeIds, int take) =>
        [.. (await ReadRowsAsync(before, excludeIds, take)).Select(row => row.EntryId)];

    private async Task<List<(Guid EntryId, DateTimeOffset OccurredAt)>> ReadRowsAsync(
        DateTimeOffset? before, IReadOnlyList<Guid> excludeIds, int take)
    {
        var query = new List<string>
        {
            "take=" + take.ToString(CultureInfo.InvariantCulture)
        };

        if (before is { } boundary)
        {
            query.Add("before=" + Uri.EscapeDataString(
                boundary.ToString("O", CultureInfo.InvariantCulture)));
        }

        query.AddRange(excludeIds.Select(id => "excludeIds=" + id.ToString("D")));

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/rewards/me/history?" + string.Join('&', query),
                UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return
        [
            .. payload.GetProperty("data").EnumerateArray().Select(row =>
                (row.GetProperty("entryId").GetGuid(),
                 row.GetProperty("occurredAt").GetDateTimeOffset()))
        ];
    }

    private async Task<Guid> SignInAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", auth.Tokens.AccessToken);

        return auth.User.Id;
    }

    /// <summary>
    /// Seeds the history at instants this test chooses.
    /// <para>
    /// The instants are explicit rather than taken from the clock: entries written in one unit of
    /// work share an <c>OccurredAt</c> in production, and a test that let the real clock decide
    /// would reproduce that only when the machine happened to be fast enough.
    /// </para>
    /// </summary>
    private async Task<List<Guid>> GrantAsync(
        Guid userId, int atTheSameInstant = 0, int atDistinctInstants = 0)
    {
        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

        var account = await accounts.FindByMemberAsync(userId)
            ?? OpenAccount(accounts, guids, userId);

        var written = new List<Guid>();

        for (var index = 0; index < atTheSameInstant; index++)
        {
            written.Add(Grant(account, guids, $"same-{index}", Moment));
        }

        for (var index = 0; index < atDistinctInstants; index++)
        {
            written.Add(Grant(account, guids, $"apart-{index}", Moment.AddMinutes(-(index + 1))));
        }

        accounts.Update(account);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .SaveChangesAsync(CancellationToken.None);

        return written;
    }

    private static RewardAccount OpenAccount(
        IRewardAccountRepository accounts, IGuidGenerator guids, Guid userId)
    {
        var account = RewardAccount.Open(
            RewardAccountId.From(guids.NewSequentialGuid()), userId, Moment);

        accounts.Add(account);

        return account;
    }

    private static Guid Grant(
        RewardAccount account, IGuidGenerator guids, string reference, DateTimeOffset occurredAt)
    {
        var entryId = guids.NewSequentialGuid();

        account.Grant(entryId, points: 10, "IntegrationTest", reference, occurredAt)
            .Should().BeTrue("the seeding references are distinct");

        return entryId;
    }
}
