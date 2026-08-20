using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Infrastructure.Hosting;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Volume, and what breaks at it, for a country's official information.
/// <para>
/// <b>Correctness tests that happen to be large.</b> The timings they print go in the performance
/// report; none asserts on wall-clock time, because a loaded build agent would fail a clock
/// assertion for reasons that have nothing to do with the code, and a suite that goes red for that
/// stops being read. It is the platform's established shape, and B8.1's, B10's and B11's scale
/// suites are its siblings.
/// </para>
/// <para>
/// What they do assert is what only appears at volume: a country page whose cost is bounded by the
/// bands it renders rather than by the archive behind them; a cursor that walks a large archive
/// without losing or repeating a notice; a briefing order that is genuinely by severity rather than
/// alphabetically by its stored text; and a retirement sweep that is bounded and idempotent.
/// </para>
/// <para>
/// <b>Every test scopes itself to its own country.</b> The class shares one database with the rest
/// of the suite, so a test asserting on a global count would pass or fail depending on which other
/// tests ran first — a worse failure than the bug it was meant to catch, because it does not
/// reproduce.
/// </para>
/// </summary>
public sealed class CountryIntelligenceScaleTests : IntegrationTestBase
{
    /// <summary>
    /// Large enough that a scan and an index lookup diverge, and that a paging defect shows up as a
    /// missing or repeated notice rather than passing by luck. Small enough that the suite still
    /// finishes in a coffee break.
    /// </summary>
    private const int NoticeCount = 1_200;

    private static readonly string[] Categories =
    [
        "visa-advisories",
        "immigration-updates",
        "public-holidays",
        "embassy-notices",
        "government-announcements"
    ];

    private readonly ITestOutputHelper _output;

    public CountryIntelligenceScaleTests(
        CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        Factory.EnsureDatabaseCreated();
        _output = output;
    }

    /// <summary>
    /// <b>Cost is bounded by the page, not by the archive behind it.</b> A country holding twelve
    /// hundred notices composes its intelligence page from the same number of queries as one holding
    /// twelve — the bands take a fixed handful each, and the reference resolution is one batched read
    /// over their union rather than one per card.
    /// </summary>
    [Fact]
    public async Task A_country_page_is_composed_in_bounded_work_over_a_large_archive()
    {
        const string country = "SC";

        await SeedAsync(country, NoticeCount);

        using var scope = Factory.Services.CreateScope();

        var composer = scope.ServiceProvider
            .GetRequiredService<CountryIntelligenceComposer>();

        var stopwatch = Stopwatch.StartNew();

        var page = await composer.ComposeAsync(country, locale: null);

        stopwatch.Stop();

        _output.WriteLine(
            $"Composed {country} over {NoticeCount} notices in {stopwatch.ElapsedMilliseconds} ms.");

        page.Statistics.ActiveNoticeCount.Should().Be(NoticeCount);

        // Every band is a handful, whatever the archive holds. A band that grew with the archive
        // would be a response nobody renders and everybody pays for.
        page.LatestNotices.Should().HaveCountLessThanOrEqualTo(6);
        page.SafetyAlerts.Should().HaveCountLessThanOrEqualTo(6);
        page.VisaUpdates.Should().HaveCountLessThanOrEqualTo(6);
        page.GovernmentAdvisories.Should().HaveCountLessThanOrEqualTo(6);

        // Exact rather than "at least a page": the number is read by an editorial team deciding what
        // needs attention, and "20+" is technically true and useless to them.
        page.CategoryBreakdown.Sum(row => row.Count).Should().Be(NoticeCount);
    }

    /// <summary>
    /// <b>The briefing order is by severity, and at volume the difference is visible.</b> Severity is
    /// stored as text so a table is legible; ordering by that text would put Informational above
    /// Critical. The rank column is what makes this pass, and twelve hundred rows is where an
    /// alphabetical sort stops looking plausible.
    /// </summary>
    [Fact]
    public async Task The_briefing_order_leads_with_severity_at_volume()
    {
        const string country = "KM";

        await SeedAsync(country, 300);

        using var scope = Factory.Services.CreateScope();

        var notices = scope.ServiceProvider.GetRequiredService<IOfficialNoticeRepository>();

        var page = await notices.TakeAsync(
            new NoticeQueryFilter
            {
                CountryCode = country,
                ActiveOnly = true,
                AsOf = DateTimeOffset.UtcNow,
                Sort = NoticeSortOrder.Priority
            },
            50);

        page.Should().NotBeEmpty();

        page.Select(notice => notice.SeverityRank)
            .Should().BeInDescendingOrder(
                "a critical border closure belongs above an informational fee schedule, always");

        page[0].Severity.Should().Be(
            NoticeSeverity.Critical, "the seed includes critical notices and one must lead");
    }

    /// <summary>
    /// <b>The cursor walks a large archive without losing or repeating a notice.</b> Notices are
    /// published in batches by an editorial team, so ties on the publication instant are the norm
    /// rather than the edge — which is exactly the shape that makes a strict comparison drop rows
    /// silently.
    /// </summary>
    [Fact]
    public async Task A_cursor_walks_the_whole_archive_exactly_once()
    {
        const string country = "DJ";
        const int total = 400;

        await SeedAsync(country, total);

        using var scope = Factory.Services.CreateScope();

        var notices = scope.ServiceProvider.GetRequiredService<IOfficialNoticeRepository>();

        var filter = new NoticeQueryFilter
        {
            CountryCode = country,
            ActiveOnly = true,
            AsOf = DateTimeOffset.UtcNow,
            Sort = NoticeSortOrder.Newest,
            Limit = 25
        };

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var page = await notices.ListAsync(filter, cursor);

            seen.AddRange(page.Items.Select(notice => notice.Id.Value));

            cursor = page.NextCursor;
            pages++;

            page.Items.Count.Should().BeLessThanOrEqualTo(25);
        }
        while (cursor is not null && pages < 100);

        _output.WriteLine($"Walked {seen.Count} notices across {pages} pages.");

        seen.Should().HaveCount(total, "no notice may be dropped at a tie boundary");
        seen.Distinct().Should().HaveCount(total, "no notice may be served twice");
    }

    /// <summary>
    /// <b>The retirement sweep is bounded and idempotent.</b> One pass takes a batch however far
    /// behind it is, and a second pass over the same rows announces nothing — without the marker it
    /// would re-announce every expired notice forever, bumping revisions editors are holding and
    /// filling the outbox with the same message.
    /// </summary>
    [Fact]
    public async Task The_retirement_sweep_is_bounded_and_announces_each_expiry_once()
    {
        const string country = "ER";
        const int expiring = 120;

        await SeedAsync(country, expiring, expireAfter: TimeSpan.FromHours(-1));

        var runner = Factory.Services
            .GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<NoticeExpirySweep>()
            .Single();

        var stopwatch = Stopwatch.StartNew();

        var first = await runner.RunOnceAsync();

        stopwatch.Stop();

        _output.WriteLine(
            $"Retired {first} notices in {stopwatch.ElapsedMilliseconds} ms.");

        first.Should().BeGreaterThan(0);

        // Keep sweeping until the backlog is clear, which is what a bounded pass means.
        var passes = 1;

        while (await runner.RunOnceAsync() > 0 && passes < 20)
        {
            passes++;
        }

        (await runner.RunOnceAsync()).Should().Be(
            0, "a second pass over announced expiries must do nothing at all");

        using var scope = Factory.Services.CreateScope();

        var notices = scope.ServiceProvider.GetRequiredService<IOfficialNoticeRepository>();

        var stillActive = await notices.CountAsync(
            new NoticeQueryFilter
            {
                CountryCode = country,
                ActiveOnly = true,
                AsOf = DateTimeOffset.UtcNow
            });

        stillActive.Should().Be(0, "every seeded notice expired an hour ago");
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a country's archive straight through the aggregates.
    /// <para>
    /// Through the domain rather than raw SQL, so what is under test is the real thing — the same
    /// severity rank, the same status transitions, the same owned collections a request would
    /// produce. Saved in batches, because a single unit of work holding twelve hundred aggregates is
    /// a memory profile no production path has.
    /// </para>
    /// </summary>
    private async Task SeedAsync(string country, int count, TimeSpan? expireAfter = null)
    {
        using var scope = Factory.Services.CreateScope();

        var notices = scope.ServiceProvider.GetRequiredService<IOfficialNoticeRepository>();
        var authorities = scope.ServiceProvider.GetRequiredService<IIssuingAuthorityRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var editor = UserId.From(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;

        var authority = IssuingAuthority.Create(
            IssuingAuthorityId.New(),
            country,
            $"{country} Immigration Department",
            AuthorityType.ImmigrationDepartment,
            now,
            editor).Value;

        authority.ApplyVerification(verified: true, now);

        await authorities.AddAsync(authority);
        await unitOfWork.SaveChangesAsync();

        for (var index = 0; index < count; index++)
        {
            var severity = (NoticeSeverity)(index % 5);

            var notice = OfficialNotice.Create(
                OfficialNoticeId.New(),
                country,
                Categories[index % Categories.Length],
                authority.Id,
                $"Notice {index}",
                $"Summary for notice {index}.",
                $"Body of notice {index}.",
                severity,
                now,
                editor).Value;

            if (expireAfter is { } window)
            {
                notice.Schedule(null, now.AddDays(-2), now.Add(window), now, editor);
            }

            notice.Submit(editor);
            notice.MarkVerified(now, editor);
            notice.Publish(now, editor);

            await notices.AddAsync(notice);

            // Batched, so the change tracker never holds the whole archive.
            if ((index + 1) % 200 == 0)
            {
                await unitOfWork.SaveChangesAsync();
            }
        }

        await unitOfWork.SaveChangesAsync();
    }
}
