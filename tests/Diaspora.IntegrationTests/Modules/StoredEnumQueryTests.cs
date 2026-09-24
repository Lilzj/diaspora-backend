using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Levels, roles and priorities compared by rank rather than by name — 15 Sep 2026.
/// <para>
/// <b>Every enum below is stored as its name</b>, and each query used an ordering operator or an
/// <c>OrderBy</c> on it, which SQL answers alphabetically. Each test seeds a row at every level and
/// asserts the whole answer, with what the text comparison returned instead noted beside it; each
/// fails on the old predicate. <c>PersistenceRuleArchitectureTests</c> keeps the shape out.
/// </para>
/// </summary>
[Collection(DiscoveryCollection.Name)]
public sealed class StoredEnumQueryTests : PooledIdentityTestBase
{
    public StoredEnumQueryTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Job search ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Senior_or_above_returns_every_role_ranked_above_senior()
    {
        var run = await SeedPostingsAsync();
        Api.ClearAuthentication();

        var page = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.GetAsync($"/api/v1/jobs?q={run}&experience=Senior&limit=50"));

        // As text: Senior alone. Lead and Director sort before S.
        page.Items.Select(item => item.Title).Should().BeEquivalentTo(
            [$"Senior {run}", $"Lead {run}", $"Director {run}"]);
    }

    [Fact]
    public async Task A_ceiling_on_experience_or_education_keeps_the_levels_below_it()
    {
        var run = await SeedPostingsAsync();

        using var scope = Factory.Services.CreateScope();
        var postings = scope.ServiceProvider.GetRequiredService<IJobPostingRepository>();

        var upToMid = await postings.QueryAsync(
            new JobQueryFilter { Text = run, MaximumExperience = ExperienceLevel.Mid }, null, 50);

        // As text: Director, Entry, Lead and Mid — everything spelled before "Mid".
        upToMid.Items.Select(posting => posting.Title.Value).Should().BeEquivalentTo(
            [$"Entry {run}", $"Mid {run}"]);

        var forABachelor = await postings.QueryAsync(
            new JobQueryFilter { Text = run, MinimumEducation = EducationLevel.Bachelor }, null, 50);

        // As text: none of these — only Associate and Bachelor are spelled at or before "Bachelor",
        // so the posting asking for a bachelor's was the only one left.
        forABachelor.Items.Select(posting => posting.Title.Value).Should().BeEquivalentTo(
            [$"Entry {run}", $"Mid {run}", $"Senior {run}"]);
    }

    // ── The moderation queue ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_moderation_queue_orders_and_filters_by_priority_rank()
    {
        Factory.EnsureDatabaseCreated();

        var moderator = UserId.New();
        var author = UserId.New();
        var now = DateTimeOffset.UtcNow;

        using (var seed = Factory.Services.CreateScope())
        {
            var dbContext = seed.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            foreach (var priority in new[]
            {
                ModerationPriority.Low, ModerationPriority.High,
                ModerationPriority.Normal, ModerationPriority.Urgent
            })
            {
                var item = ModerationItem.Enqueue(
                    ModerationContentType.Comment,
                    $"ranked-{Guid.NewGuid():N}",
                    author,
                    now,
                    title: priority.ToString(),
                    priority: priority);

                // Assigned to a moderator nobody else uses, so the queue this test reads is its own.
                item.Assign(moderator, now);
                item.ClearDomainEvents();
                dbContext.Set<ModerationItem>().Add(item);
            }

            await dbContext.SaveChangesAsync();
        }

        using var scope = Factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IModerationItemRepository>();
        var pagination = new PaginationRequest { Page = 1, PageSize = 50 };

        // As text: Urgent, Normal, Low, High — high-priority work at the bottom of the queue.
        (await queue.ListQueueAsync(pagination, assignedTo: moderator))
            .Items.Select(item => item.Priority).Should().Equal(
                ModerationPriority.Urgent, ModerationPriority.High,
                ModerationPriority.Normal, ModerationPriority.Low);

        // As text: all four, because Low and Normal are both spelled after High.
        (await queue.ListQueueAsync(pagination, minimumPriority: ModerationPriority.High, assignedTo: moderator))
            .Items.Select(item => item.Priority).Should().Equal(
                ModerationPriority.Urgent, ModerationPriority.High);

        // As text: Normal and Urgent — High was left out.
        (await queue.ListQueueAsync(pagination, minimumPriority: ModerationPriority.Normal, assignedTo: moderator))
            .Items.Select(item => item.Priority).Should().Equal(
                ModerationPriority.Urgent, ModerationPriority.High, ModerationPriority.Normal);
    }

    // ── Organisation memberships ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Role_counts_and_the_role_filter_go_by_rank()
    {
        var organization = OrganizationId.New();

        await SeedMembershipsAsync(
            (organization, UserId.New(), MembershipRole.Member),
            (organization, UserId.New(), MembershipRole.Volunteer),
            (organization, UserId.New(), MembershipRole.Volunteer),
            (organization, UserId.New(), MembershipRole.Moderator),
            (organization, UserId.New(), MembershipRole.Administrator),
            (organization, UserId.New(), MembershipRole.Owner));

        using var scope = Factory.Services.CreateScope();
        var memberships = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipRepository>();

        // As text: 3 — both Volunteers counted as owners, so the last owner could step down.
        (await memberships.CountByRoleAsync(organization, MembershipRole.Owner)).Should().Be(1);

        // As text: 6 — every member, because "Administrator" is spelled first.
        (await memberships.CountByRoleAsync(organization, MembershipRole.Administrator)).Should().Be(2);

        // As text: 2 — the Volunteers alone; the moderator, administrator and owner went uncounted.
        (await memberships.CountByRoleAsync(organization, MembershipRole.Volunteer)).Should().Be(5);

        var staff = await memberships.SearchAsync(new MembershipQueryFilter
        {
            OrganizationId = organization,
            MinimumRole = MembershipRole.Moderator,
            Limit = 50
        });

        // As text: Moderator, Owner and both Volunteers; the Administrator shut out.
        staff.Items.Select(membership => membership.Role).Should().BeEquivalentTo(
            [MembershipRole.Moderator, MembershipRole.Administrator, MembershipRole.Owner]);
    }

    [Fact]
    public async Task A_members_organisations_are_listed_highest_role_first()
    {
        var member = UserId.New();

        await SeedMembershipsAsync(
            (OrganizationId.New(), member, MembershipRole.Volunteer),
            (OrganizationId.New(), member, MembershipRole.Owner),
            (OrganizationId.New(), member, MembershipRole.Member),
            (OrganizationId.New(), member, MembershipRole.Administrator));

        using var scope = Factory.Services.CreateScope();
        var memberships = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipRepository>();

        // As text: Volunteer, Owner, Member, Administrator.
        (await memberships.FindCurrentForMemberAsync(member))
            .Select(membership => membership.Role).Should().Equal(
                MembershipRole.Owner, MembershipRole.Administrator,
                MembershipRole.Volunteer, MembershipRole.Member);
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A published, open posting at each of five levels, each asking for more education.</summary>
    private async Task<string> SeedPostingsAsync()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var employerId = EmployerProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        (ExperienceLevel Experience, EducationLevel Education)[] levels =
        [
            (ExperienceLevel.Entry, EducationLevel.None),
            (ExperienceLevel.Mid, EducationLevel.Vocational),
            (ExperienceLevel.Senior, EducationLevel.Bachelor),
            (ExperienceLevel.Lead, EducationLevel.Master),
            (ExperienceLevel.Director, EducationLevel.Doctorate)
        ];

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        foreach (var (experience, education) in levels)
        {
            var posting = JobPosting.Create(
                JobPostingId.New(),
                employerId,
                ownerId,
                JobTitle.Create($"{experience} {run}").Value,
                JobSlug.Create($"ranked-{run}-{experience.ToString().ToLowerInvariant()}").Value,
                JobDescription.Create($"A posting seeded for the ranked filter tests: {experience}.").Value,
                "technology",
                JobType.FullTime,
                experience,
                JobLocationContext.Create("MU", null, "Port Louis", null, null).Value,
                WorkArrangement.Create(WorkplaceType.OnSite, false).Value,
                now).Value;

            posting.UpdateDetails(
                    posting.Title,
                    posting.Description,
                    posting.JobType,
                    experience,
                    education,
                    posting.Location,
                    posting.WorkArrangement,
                    posting.Salary,
                    posting.Visibility,
                    Math.Max(1, posting.VacancyCount),
                    posting.RequiresResume,
                    baseRevision: null)
                .IsSuccess.Should().BeTrue();

            posting.EducationLevel.Should().Be(education);

            // Asserted: a posting that failed to publish would pass every "not returned" check.
            posting.Publish(now, openImmediately: true).IsSuccess.Should().BeTrue();

            dbContext.Set<JobPosting>().Add(posting);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private async Task SeedMembershipsAsync(
        params (OrganizationId Organization, UserId Member, MembershipRole Role)[] memberships)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var now = DateTimeOffset.UtcNow;

        foreach (var (organization, member, role) in memberships)
        {
            var membership = OrganizationMembership.Open(
                OrganizationMembershipId.New(),
                organization,
                member,
                MembershipStatus.Active,
                role,
                Guid.NewGuid(),
                now).Value;

            // No organisation row exists for these; the counters the event would move are not
            // what this test reads.
            membership.ClearDomainEvents();
            dbContext.Set<OrganizationMembership>().Add(membership);
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed record PageShape
    {
        public List<CardShape> Items { get; init; } = [];
    }

    private sealed record CardShape
    {
        public string Title { get; init; } = string.Empty;
    }
}
