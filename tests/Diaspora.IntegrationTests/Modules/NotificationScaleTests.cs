using System.Diagnostics;
using System.Globalization;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Volume, and what breaks at it, for the platform's most frequently read surface.
/// <para>
/// <b>Correctness tests that happen to be large.</b> The timings they print go in the performance
/// report; none asserts on wall-clock time, because a loaded build agent would fail a clock
/// assertion for reasons that have nothing to do with the code, and a suite that goes red for that
/// stops being read. B10's, B11's and B8.1's scale suites are its siblings.
/// </para>
/// <para>
/// What they do assert is what only appears at volume: an unread count whose cost does not grow with
/// a member's history; a cursor that walks a large inbox without losing or repeating a message —
/// including across the tie group every notification created in one unit of work shares; and
/// counters that survive a bulk read.
/// </para>
/// <para>
/// <b>Every test scopes itself to its own member.</b> The class shares one database with the rest of
/// the suite, so a test asserting on a global count would pass or fail depending on which other
/// tests ran first — a worse failure than the bug it was meant to catch, because it does not
/// reproduce.
/// </para>
/// </summary>
public sealed class NotificationScaleTests : IntegrationTestBase
{
    /// <summary>
    /// Large enough that a scan and an index lookup diverge, and that a paging defect shows up as a
    /// missing message rather than passing by luck. Small enough that the suite still finishes.
    /// </summary>
    private const int NotificationCount = 2_000;

    private readonly ITestOutputHelper _output;

    public NotificationScaleTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        factory.EnsureDatabaseCreated();
        _output = output;
    }

    /// <summary>
    /// <b>The badge is one indexed row read, whatever the member's history.</b> This is the reason
    /// the counter is maintained rather than computed: a member who has been here longest would
    /// otherwise have the slowest app launch, which is exactly backwards.
    /// </summary>
    [Fact]
    public async Task The_unread_count_is_answered_from_the_counter_not_a_scan()
    {
        var recipient = UserId.New();

        await SeedAsync(recipient, NotificationCount);

        using var scope = Factory.Services.CreateScope();
        var inboxes = scope.ServiceProvider.GetRequiredService<IUserInboxRepository>();

        var stopwatch = Stopwatch.StartNew();
        var inbox = await inboxes.FindAsync(recipient);
        stopwatch.Stop();

        inbox.Should().NotBeNull();
        inbox!.UnreadCount.Should().Be(NotificationCount);
        inbox.TotalCount.Should().Be(NotificationCount);

        _output.WriteLine(
            $"unread count over {NotificationCount} notifications: {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// <b>The cursor walks a large inbox exactly once.</b> Messages created in one unit of work
    /// share a creation instant to the tick, so a cursor comparing on the timestamp alone would
    /// silently drop all but one of each group — and on an inbox, a message the member never sees is
    /// one they have no way of knowing about.
    /// </summary>
    [Fact]
    public async Task The_cursor_walks_a_large_inbox_without_losing_or_repeating_anything()
    {
        var recipient = UserId.New();

        await SeedAsync(recipient, NotificationCount);

        using var scope = Factory.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var seen = new HashSet<Guid>();
        var duplicates = 0;
        string? cursor = null;
        var pages = 0;

        var stopwatch = Stopwatch.StartNew();

        do
        {
            var page = await notifications.SearchAsync(new NotificationQueryFilter
            {
                RecipientUserId = recipient,
                Cursor = cursor,
                Limit = 50
            });

            foreach (var notification in page.Items)
            {
                if (!seen.Add(notification.Id.Value))
                {
                    duplicates++;
                }
            }

            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null && pages < 200);

        stopwatch.Stop();

        duplicates.Should().Be(0, "a repeated message means the boundary excluded nothing");
        seen.Should().HaveCount(
            NotificationCount,
            "a missing message is one the member has no way of knowing about");

        _output.WriteLine(
            $"walked {seen.Count} notifications in {pages} pages: {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// <b>The reconciliation sweep's recount agrees with the maintained counters.</b> If it did not,
    /// the counters would be drifting on the ordinary write path — which is precisely the defect the
    /// sweep exists to surface rather than to hide.
    /// </summary>
    [Fact]
    public async Task The_measured_counts_agree_with_the_maintained_counters()
    {
        var recipient = UserId.New();

        await SeedAsync(recipient, 400);

        using var scope = Factory.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var inboxes = scope.ServiceProvider.GetRequiredService<IUserInboxRepository>();

        var stopwatch = Stopwatch.StartNew();
        var measured = await notifications.CountForReconciliationAsync(recipient);
        stopwatch.Stop();

        var inbox = await inboxes.FindAsync(recipient);

        measured.Unread.Should().Be(inbox!.UnreadCount);
        measured.Total.Should().Be(inbox.TotalCount);

        _output.WriteLine($"reconciliation recount over 400 notifications: {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// <b>Bulk creation stays linear.</b> Four hundred notifications in one unit of work is what a
    /// broadcast looks like, and a per-notification query in the counter path would turn it
    /// quadratic without anybody noticing until a country audience existed.
    /// </summary>
    [Fact]
    public async Task Bulk_creation_settles_every_counter()
    {
        var recipient = UserId.New();

        var stopwatch = Stopwatch.StartNew();
        await SeedAsync(recipient, 400);
        stopwatch.Stop();

        using var scope = Factory.Services.CreateScope();
        var inboxes = scope.ServiceProvider.GetRequiredService<IUserInboxRepository>();
        var inbox = await inboxes.FindAsync(recipient);

        inbox!.UnreadCount.Should().Be(400);

        // Every message was seeded under one category, so its counter must carry the whole set.
        inbox.Categories.Should().ContainSingle();
        inbox.Categories[0].UnreadCount.Should().Be(400);

        _output.WriteLine($"bulk creation of 400 notifications: {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Searching a large inbox is bounded by the page, not by the history behind it.
    /// </summary>
    [Fact]
    public async Task Searching_a_large_inbox_returns_a_bounded_page()
    {
        var recipient = UserId.New();

        await SeedAsync(recipient, NotificationCount);

        using var scope = Factory.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var stopwatch = Stopwatch.StartNew();

        var page = await notifications.SearchAsync(new NotificationQueryFilter
        {
            RecipientUserId = recipient,
            Search = "message 1234",
            Limit = 20
        });

        stopwatch.Stop();

        page.Items.Should().HaveCountLessThanOrEqualTo(20);

        _output.WriteLine(
            $"search over {NotificationCount} notifications: {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Seeds one member's inbox, in batches, through the ordinary write path.
    /// <para>
    /// Through the repository rather than straight into the tables, because the thing under test is
    /// whether the ordinary path stays correct at volume. Seeding around it would test the seeder.
    /// </para>
    /// <para>
    /// <b>The counters are not settled here, deliberately.</b> Creating a notification raises
    /// <c>NotificationCreatedDomainEvent</c>, whose handler moves the badge inside the same unit of
    /// work — that is the mechanism under test. An explicit settle would double every count, which is
    /// what the first version of this helper did and what the assertion below caught.
    /// </para>
    /// </summary>
    private async Task SeedAsync(UserId recipient, int count)
    {
        const int BatchSize = 100;

        for (var offset = 0; offset < count; offset += BatchSize)
        {
            using var scope = Factory.Services.CreateScope();

            var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var batch = Math.Min(BatchSize, count - offset);

            for (var index = 0; index < batch; index++)
            {
                var ordinal = offset + index;

                var created = Notification.Create(
                    NotificationId.New(),
                    recipient,
                    "jobs",
                    NotificationPriority.Normal,
                    "jobs.application.received",
                    "jobs.application.received",
                    NotificationSource.Create(
                        "Jobs",
                        "ApplicationSubmittedIntegrationEvent",
                        "application",
                        ordinal.ToString(CultureInfo.InvariantCulture)),
                    NotificationContent.Create(
                        $"Notification {ordinal}", $"Scale message {ordinal}."),
                    DeepLink.TryCreate(
                        $"/jobs/{ordinal}", "job", ordinal.ToString(CultureInfo.InvariantCulture)),
                    DateTimeOffset.UtcNow,
                    groupKey: $"scale:{recipient.Value}:{ordinal}");

                created.IsSuccess.Should().BeTrue();

                await notifications.AddAsync(created.Value);
            }

            await unitOfWork.SaveChangesAsync();
        }
    }
}
