using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Academy;
using Diaspora.Modules.Creator.Domain.Challenges;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Grants;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Milestones;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Domain.Rewards;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Creator.Infrastructure.Hosting;

/// <summary>
/// Puts the creator programme's catalogue into the database at startup.
/// <para>
/// <b>What this seeds is the programme's own configuration, and nothing else.</b> §38 of the Wave 6
/// brief draws the line: "Do not hide missing functionality behind seeded fake records." There is no
/// seeded creator here, no seeded application, no seeded redemption and no seeded progress — every
/// row a member's actions produce is produced by their actions. What is seeded is the milestone
/// catalogue, the challenge catalogue, the grant programmes with their served forms, the Academy's
/// content, the points rules and the redemption catalogue: reference data a deployment would
/// otherwise have to write by hand before the module did anything at all.
/// </para>
/// <para>
/// <b>A <see cref="BackgroundService"/> with a public <see cref="SeedAsync"/>, and both halves
/// matter.</b> As a hosted service it runs after the host is up, so a slow or failing seed does not
/// stop the application serving; as a public method the integration factory awaits it
/// deterministically rather than racing the host. A seeder that only ran as a hosted service would
/// make every integration test flaky — the platform has that on record four times over.
/// </para>
/// <para>
/// <b>Idempotent by key, and existing rows are left alone.</b> A deployment that renamed a milestone
/// or retired a challenge means it, and a seeder restating its own values on every boot would undo
/// that silently once a night.
/// </para>
/// <para>
/// <b>Every failure is swallowed and logged.</b> A catalogue that did not seed leaves a deployment
/// with an empty milestone list, which somebody notices; an exception out of a hosted service takes
/// the host down at startup, which is the same outage for every other module too.
/// </para>
/// </summary>
public sealed class CreatorCatalogueSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CreatorCatalogueSeeder> _logger;

    public CreatorCatalogueSeeder(
        IServiceScopeFactory scopes, ILogger<CreatorCatalogueSeeder> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public string Module => CreatorModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => SeedAsync(stoppingToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            await SeedPointRulesAsync(scope.ServiceProvider, cancellationToken);
            await SeedRewardsAsync(scope.ServiceProvider, cancellationToken);
            await SeedMilestonesAsync(scope.ServiceProvider, cancellationToken);
            await SeedChallengesAsync(scope.ServiceProvider, cancellationToken);
            await SeedGrantsAsync(scope.ServiceProvider, cancellationToken);
            await SeedAcademyAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown during startup. Nothing to report.
        }
#pragma warning disable CA1031 // A seeding failure must not take the host down. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "Seeding the Creator Platform catalogue failed.");
        }
#pragma warning restore CA1031
    }

    // ── The points rules ────────────────────────────────────────────────────────────────────
    //
    // What an action pays. Read by the activity recorder as the weight of a recognised action, so
    // this catalogue is what makes a guide worth more than a comment to a milestone, to a leaderboard
    // and to a member's balance — one number rather than three.

    private static async Task SeedPointRulesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<ICreatorRewardRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();

        var seeds = new (string Key, string Action, string Label, string Description, int Points,
            string? Route, string? Limit, string? Category, int? Max, int? Cooldown)[]
        {
            ("guide-published", CreatorActivityTypes.GuidePublished,
                "Publish a guide", "Write something that helps somebody settle in.", 50,
                "/knowledge/create", null, "knowledge", null, null),

            ("guide-featured", CreatorActivityTypes.GuideFeatured,
                "Have a guide featured", "The platform features guides it thinks everyone should read.",
                100, null, null, "knowledge", null, null),

            ("post-published", CreatorActivityTypes.PostPublished,
                "Post to the community", "Share news, ask a question, start a conversation.", 10,
                "/community/create", "Up to 10 a day", "community", 10, 24),

            ("comment-added", CreatorActivityTypes.CommentAdded,
                "Answer somebody", "Reply to a question or add to a discussion.", 5,
                null, "Up to 20 a day", "community", 20, 24),

            ("event-published", CreatorActivityTypes.EventPublished,
                "List an event", "Put something on for people near you.", 30,
                "/community-life/events/create", null, "events", null, null),

            ("event-hosted", CreatorActivityTypes.EventHosted,
                "Host an event", "Run it, and check people in.", 75, null, null, "events", null, null),

            ("provider-published", CreatorActivityTypes.ProviderPublished,
                "List a service", "Publish a provider profile.", 40, null, null, "services", null, null),

            ("listing-published", CreatorActivityTypes.ListingPublished,
                "List something for sale", "Publish a marketplace listing.", 15,
                null, "Up to 10 a day", "marketplace", 10, 24),

            ("job-published", CreatorActivityTypes.JobPublished,
                "Post a job", "Publish a role somebody could take.", 30, null, null, "jobs", null, null),

            ("housing-published", CreatorActivityTypes.HousingPublished,
                "List a home", "Publish a housing listing.", 30, null, null, "housing", null, null),

            ("volunteer-hours", CreatorActivityTypes.VolunteerHoursConfirmed,
                "Volunteer", "Hours an organizer confirmed.", 25, null, null, "community", null, null),

            ("referral-qualified", CreatorActivityTypes.ReferralQualified,
                "Refer somebody who contributes",
                "Points land once the person you invited makes their first contribution.",
                ReferralQualificationPolicy.RewardPoints, "/creator/referrals", null, "growth",
                null, null),

            ("course-completed", CreatorActivityTypes.CourseCompleted,
                "Finish an Academy course", "Learn how the platform works, and get better at it.", 40,
                "/creator/academy", null, "learning", null, null)
        };

        var order = 0;
        var added = false;

        foreach (var seed in seeds)
        {
            order++;

            if (await repository.FindRuleByKeyAsync(seed.Key, cancellationToken) is not null)
            {
                continue;
            }

            repository.AddRule(CreatorPointRule.Define(
                CreatorPointRuleId.From(guids.NewSequentialGuid()),
                seed.Key,
                seed.Action,
                seed.Label,
                seed.Description,
                seed.Points,
                order,
                seed.Route,
                seed.Limit,
                seed.Category,
                seed.Max,
                seed.Cooldown));

            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ── The redemption catalogue ────────────────────────────────────────────────────────────
    //
    // What points buy. Every one of these is an entitlement the platform grants rather than money —
    // the crossing into currency is B15's single RewardAccount.Convert and goes nowhere near here.

    private static async Task SeedRewardsAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<ICreatorRewardRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();

        var seeds = new (string Key, string Title, string Description, int Cost, string Category,
            string GrantKind, CreatorStatus Minimum, int? Max, int? Cooldown)[]
        {
            ("featured-guide-slot", "Featured guide slot",
                "Put one of your guides in front of the whole community for a week.",
                1_500, "visibility", "placement", CreatorStatus.EmergingCreator, null, 24 * 30),

            ("profile-highlight", "Profile highlight",
                "Your creator profile is highlighted in the directory for a fortnight.",
                800, "visibility", "placement", CreatorStatus.Contributor, null, 24 * 14),

            ("creator-badge-supporter", "Supporter badge",
                "A badge on your profile that says you back the community.",
                300, "recognition", "badge", CreatorStatus.NotACreator, 1, null),

            ("academy-mentor-session", "Mentor session",
                "A half hour with somebody from the Academy on whatever you are stuck on.",
                2_000, "learning", "entitlement", CreatorStatus.EmergingCreator, null, 24 * 60),

            ("event-promotion", "Event promotion",
                "One of your events promoted to people near it.",
                1_000, "visibility", "placement", CreatorStatus.Contributor, null, 24 * 14)
        };

        var order = 0;
        var added = false;

        foreach (var seed in seeds)
        {
            order++;

            if (await repository.FindRewardByKeyAsync(seed.Key, cancellationToken) is not null)
            {
                continue;
            }

            repository.AddReward(CreatorRewardDefinition.Define(
                CreatorRewardId.From(guids.NewSequentialGuid()),
                seed.Key,
                seed.Title,
                seed.Description,
                seed.Cost,
                order,
                seed.Category,
                grantKind: seed.GrantKind,
                minimumStatus: seed.Minimum,
                maxPerCreator: seed.Max,
                cooldownHours: seed.Cooldown));

            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ── Milestones ──────────────────────────────────────────────────────────────────────────
    //
    // Two of these are deliberately weighted and capped, because the register says a client dividing
    // current by target "renders visibly incorrect figures" and the catalogue should contain the
    // shapes that prove it.

    private static async Task SeedMilestonesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<ICreatorMilestoneRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();

        var added = false;

        async Task<bool> MissingAsync(string key) =>
            await repository.FindByKeyAsync(key, cancellationToken) is null;

        if (await MissingAsync("first-guide"))
        {
            repository.Add(CreatorMilestoneDefinition
                .Define(
                    CreatorMilestoneId.From(guids.NewSequentialGuid()),
                    "first-guide",
                    "First guide",
                    "Publish your first guide.",
                    target: 1,
                    "100 points",
                    rewardPoints: 100,
                    sortOrder: 1,
                    category: "knowledge",
                    icon: "book",
                    unit: "guides")
                .Counting(CreatorActivityTypes.GuidePublished, weight: 1));

            added = true;
        }

        if (await MissingAsync("ten-guides"))
        {
            repository.Add(CreatorMilestoneDefinition
                .Define(
                    CreatorMilestoneId.From(guids.NewSequentialGuid()),
                    "ten-guides",
                    "Ten guides",
                    "Publish ten guides.",
                    target: 10,
                    "500 points and the Knowledge badge",
                    rewardPoints: 500,
                    sortOrder: 2,
                    category: "knowledge",
                    icon: "library",
                    unit: "guides")
                .Counting(CreatorActivityTypes.GuidePublished, weight: 1));

            added = true;
        }

        // Weighted: a featured guide counts three. A client dividing a raw count by ten would show a
        // different figure from this one, which is precisely the register's point.
        if (await MissingAsync("trusted-voice"))
        {
            repository.Add(CreatorMilestoneDefinition
                .Define(
                    CreatorMilestoneId.From(guids.NewSequentialGuid()),
                    "trusted-voice",
                    "Trusted voice",
                    "Build a body of work the platform features. A featured guide counts for three.",
                    target: 15,
                    "750 points and the Trusted Voice badge",
                    rewardPoints: 750,
                    sortOrder: 3,
                    category: "knowledge",
                    icon: "star",
                    unit: "points")
                .Counting(CreatorActivityTypes.GuidePublished, weight: 1)
                .Counting(CreatorActivityTypes.GuideFeatured, weight: 3));

            added = true;
        }

        // Capped: answers help, but they cannot carry the whole milestone. A raw sum would let a
        // hundred comments finish a milestone about hosting.
        if (await MissingAsync("community-builder"))
        {
            repository.Add(CreatorMilestoneDefinition
                .Define(
                    CreatorMilestoneId.From(guids.NewSequentialGuid()),
                    "community-builder",
                    "Community builder",
                    "Bring people together. Answers count toward this, but for no more than a third of it.",
                    target: 30,
                    "600 points",
                    rewardPoints: 600,
                    sortOrder: 4,
                    category: "community",
                    icon: "users",
                    unit: "points")
                .Counting(CreatorActivityTypes.EventHosted, weight: 10)
                .Counting(CreatorActivityTypes.EventPublished, weight: 4)
                .Counting(CreatorActivityTypes.CommentAdded, weight: 1, cap: 10));

            added = true;
        }

        if (await MissingAsync("first-referral"))
        {
            repository.Add(CreatorMilestoneDefinition
                .Define(
                    CreatorMilestoneId.From(guids.NewSequentialGuid()),
                    "first-referral",
                    "First referral",
                    "Invite somebody who goes on to contribute.",
                    target: 1,
                    "200 points",
                    rewardPoints: 200,
                    sortOrder: 5,
                    category: "growth",
                    icon: "user-plus",
                    unit: "referrals")
                .Counting(CreatorActivityTypes.ReferralQualified, weight: 1));

            added = true;
        }

        if (await MissingAsync("academy-graduate"))
        {
            repository.Add(CreatorMilestoneDefinition
                .Define(
                    CreatorMilestoneId.From(guids.NewSequentialGuid()),
                    "academy-graduate",
                    "Academy graduate",
                    "Finish three Academy courses.",
                    target: 3,
                    "300 points",
                    rewardPoints: 300,
                    sortOrder: 6,
                    category: "learning",
                    icon: "graduation-cap",
                    unit: "courses")
                .Counting(CreatorActivityTypes.CourseCompleted, weight: 1));

            added = true;
        }

        // 21 Oct 2026. The badges these two reward labels have always promised. Applied to new and
        // existing definitions alike — the repository finds a staged one first — so a database seeded
        // before today gains the badge on the next start rather than never.
        foreach (var (key, badge) in MilestoneBadges)
        {
            if (await repository.FindByKeyAsync(key, cancellationToken) is { } definition
                && definition.AwardBadge(badge))
            {
                added = true;
            }
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Milestones whose reward is a badge, by milestone key.</summary>
    private static readonly (string MilestoneKey, string BadgeKey)[] MilestoneBadges =
    [
        ("ten-guides", "knowledge"),
        ("trusted-voice", "trusted-voice")
    ];

    // ── Challenges ──────────────────────────────────────────────────────────────────────────

    private static async Task SeedChallengesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<ICreatorChallengeRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var added = false;

        if (await repository.FindByKeyAsync("settling-in-sprint", cancellationToken) is null)
        {
            repository.Add(CreatorChallenge
                .Define(
                    CreatorChallengeId.From(guids.NewSequentialGuid()),
                    "settling-in-sprint",
                    "Settling-in sprint",
                    "Three guides and an answer, for the people arriving this month.",
                    now.AddDays(-1),
                    now.AddDays(30),
                    "1,000 points",
                    rewardPoints: 1_000,
                    hasLeaderboard: true,
                    category: "knowledge",
                    icon: "compass")
                .WithTask(
                    CreatorChallengeTaskId.From(guids.NewSequentialGuid()),
                    "Publish three guides",
                    CreatorActivityTypes.GuidePublished,
                    target: 3,
                    actionRoute: "/knowledge/create",
                    actionLabel: "Write a guide")
                .WithTask(
                    CreatorChallengeTaskId.From(guids.NewSequentialGuid()),
                    "Answer five questions",
                    CreatorActivityTypes.CommentAdded,
                    target: 5)
                .WithRule("Only guides and answers published after you join count.")
                .WithRule("The leaderboard ranks by weighted total, so a featured guide counts more.")
                .WithRule("You can claim the reward once every task is done.")
                .Publish());

            added = true;
        }

        if (await repository.FindByKeyAsync("host-something", cancellationToken) is null)
        {
            repository.Add(CreatorChallenge
                .Define(
                    CreatorChallengeId.From(guids.NewSequentialGuid()),
                    "host-something",
                    "Host something",
                    "Put one event on and run it to the end.",
                    now.AddDays(-1),
                    now.AddDays(60),
                    "750 points",
                    rewardPoints: 750,
                    hasLeaderboard: false,
                    category: "community",
                    icon: "calendar",
                    minimumStatus: CreatorStatus.Contributor)
                .WithTask(
                    CreatorChallengeTaskId.From(guids.NewSequentialGuid()),
                    "List an event",
                    CreatorActivityTypes.EventPublished,
                    target: 1,
                    actionRoute: "/community-life/events/create",
                    actionLabel: "List an event")
                .WithTask(
                    CreatorChallengeTaskId.From(guids.NewSequentialGuid()),
                    "Run it",
                    CreatorActivityTypes.EventHosted,
                    target: 1)
                .WithRule("Open to contributors and above.")
                .WithRule("The event has to finish inside the challenge window.")
                .Publish());

            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ── Grants ──────────────────────────────────────────────────────────────────────────────
    //
    // Two programmes, each with a real served form. The forms are the point: adding a question is a
    // change here rather than an app release.

    private static async Task SeedGrantsAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<ICommunityGrantRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var added = false;

        if (await repository.FindByKeyAsync("community-project-fund", cancellationToken) is null)
        {
            repository.Add(CommunityGrant
                .Define(
                    CommunityGrantId.From(guids.NewSequentialGuid()),
                    "community-project-fund",
                    "Community project fund",
                    "Up to £2,000 for something that helps people settle in.",
                    "For projects run by members, for members: a welcome pack, a language exchange, "
                    + "a legal clinic, a weekend of childcare while people sit exams. We fund the "
                    + "cost of doing it rather than the idea of doing it.",
                    "community",
                    fundingMinorUnits: 200_000,
                    currency: "GBP",
                    opensAt: now.AddDays(-7),
                    deadlineAt: now.AddDays(45),
                    allowsAmountRequest: true,
                    minimumStatus: CreatorStatus.Contributor)
                .WithRange(25_000, 200_000)
                .WithField("project-name", "What is it called?", GrantFieldKind.Text, true,
                    maxLength: 120)
                .WithField("summary", "What will you do?", GrantFieldKind.LongText, true,
                    "A few sentences. What happens, for whom, and where.", 2_000)
                .WithField("people-reached", "Roughly how many people will it reach?",
                    GrantFieldKind.Number, true)
                .WithField("category", "What kind of project is it?", GrantFieldKind.Select, true,
                    options:
                    [
                        ("welcome", "Welcoming new arrivals"),
                        ("language", "Language and learning"),
                        ("legal", "Legal and paperwork"),
                        ("work", "Work and money"),
                        ("family", "Family and childcare"),
                        ("other", "Something else")
                    ])
                .WithField("budget", "How will you spend it?", GrantFieldKind.LongText, true,
                    "A rough breakdown is fine.", 2_000)
                .WithField("partners", "Anyone doing it with you?", GrantFieldKind.LongText, false,
                    maxLength: 1_000)
                .WithDeclaration("Everything I have written here is true as far as I know.")
                .WithDeclaration(
                    "I will report on what happened, and return anything I do not spend.")
                .WithDeliverable("A short report within three months of the award.")
                .WithDeliverable("Photographs or a note about what happened, where that is possible.")
                .Publish());

            added = true;
        }

        if (await repository.FindByKeyAsync("creator-equipment-grant", cancellationToken) is null)
        {
            repository.Add(CommunityGrant
                .Define(
                    CommunityGrantId.From(guids.NewSequentialGuid()),
                    "creator-equipment-grant",
                    "Creator equipment grant",
                    "£500 toward the kit you need to keep making things.",
                    "A microphone, a second-hand laptop, a year of a tool you cannot otherwise "
                    + "afford. For verified creators who are already publishing.",
                    "creators",
                    fundingMinorUnits: 50_000,
                    currency: "GBP",
                    opensAt: now.AddDays(-1),
                    deadlineAt: now.AddDays(90),
                    allowsAmountRequest: false,
                    minimumStatus: CreatorStatus.VerifiedCreator,
                    requiresVerifiedCreator: true)
                .WithField("what-you-need", "What do you need?", GrantFieldKind.LongText, true,
                    maxLength: 1_500)
                .WithField("why", "What will it let you make?", GrantFieldKind.LongText, true,
                    maxLength: 1_500)
                .WithDeclaration("The equipment is for my own creative work on this platform.")
                .WithDeliverable("Tell us what you made with it within six months.")
                .Publish());

            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ── The Academy ─────────────────────────────────────────────────────────────────────────
    //
    // Real structured content, including a quiz whose answers exist only in the database. The quiz
    // is what makes the answer-key test meaningful — a suite that graded an empty quiz would prove
    // nothing.

    private static async Task SeedAcademyAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IAcademyRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();

        var sections = new (string Key, string Title, string Description, string Icon)[]
        {
            ("start-here", "Start here", "What the platform is, and how to be useful on it.", "compass"),
            ("craft", "Craft", "Writing guides people actually finish.", "pen"),
            ("safety", "Community safety", "Keeping yourself and other people safe.", "shield")
        };

        var order = 0;
        var seeded = false;

        foreach (var section in sections)
        {
            order++;

            var existing = (await repository.ListSectionsAsync(cancellationToken))
                .Any(row => string.Equals(row.Key, section.Key, StringComparison.Ordinal));

            if (existing)
            {
                continue;
            }

            repository.AddSection(AcademySection.Define(
                AcademySectionId.From(guids.NewSequentialGuid()),
                section.Key,
                section.Title,
                order,
                section.Description,
                section.Icon));

            seeded = true;
        }

        if (seeded)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await SeedFirstCourseAsync(repository, unitOfWork, guids, cancellationToken);
        await SeedSafetyCourseAsync(repository, unitOfWork, guids, cancellationToken);
    }

    private static async Task SeedFirstCourseAsync(
        IAcademyRepository repository,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        CancellationToken cancellationToken)
    {
        if (await repository.FindCourseBySlugAsync("welcome-to-diaspora", cancellationToken)
            is not null)
        {
            return;
        }

        var course = AcademyCourse.Define(
            AcademyCourseId.From(guids.NewSequentialGuid()),
            "welcome-to-diaspora",
            "Welcome to Diaspora",
            "What this place is for, and how to get something out of it in your first week.",
            CourseLevel.Beginner,
            estimatedMinutes: 15,
            sortOrder: 1,
            sectionKey: "start-here",
            awardsCertificate: false,
            completionPoints: 40,
            icon: "compass");

        course.WithTag("basics").WithTag("first-week");
        course.SetLessonCount(2);

        repository.AddCourse(course);

        var welcome = AcademyLesson.Define(
            AcademyLessonId.From(guids.NewSequentialGuid()),
            course.Id,
            "what-this-is",
            "What this is",
            LessonKind.Reading,
            estimatedMinutes: 6,
            sortOrder: 1);

        welcome
            .WithBlock(LessonBlock.Heading("A place built by the people using it", 0))
            .WithBlock(LessonBlock.Paragraph(
                "Everything you will read here was written by somebody who had already done the "
                + "thing they are describing. Nobody is paid to write it and nothing is "
                + "sponsored — which is why it is worth reading, and why it is worth adding to.",
                1))
            .WithBlock(
                LessonBlock.List(ordered: false, 2),
                "Guides: how to do a specific thing in a specific place.",
                "The community: questions, answers and news.",
                "Services and listings: people offering something real.")
            .WithBlock(LessonBlock.Callout(
                CalloutTone.Info,
                "One rule",
                "Write what you actually did. A guide that says what you heard somebody else did "
                + "helps nobody and is the fastest way to lose your standing here.",
                3))
            .WithBlock(LessonBlock.Action(
                "Read a guide", "/knowledge", "See what good looks like before you write.", 4));

        repository.AddLesson(welcome);

        var quizLesson = AcademyLesson.Define(
            AcademyLessonId.From(guids.NewSequentialGuid()),
            course.Id,
            "check-you-have-it",
            "Check you have it",
            LessonKind.Quiz,
            estimatedMinutes: 4,
            sortOrder: 2);

        quizLesson.WithBlock(LessonBlock.Paragraph(
            "Two questions. Get both right and the course is finished.", 0));

        var quizId = AcademyQuizId.From(guids.NewSequentialGuid());
        quizLesson.WithQuiz(quizId);

        repository.AddLesson(quizLesson);

        var quiz = AcademyQuiz.Define(quizId, quizLesson.Id, "welcome-quiz", passMark: 2);

        quiz.WithQuestion(
            AcademyQuizQuestionId.From(guids.NewSequentialGuid()),
            "What makes a guide worth reading here?",
            "First-hand experience is the whole basis of the platform's usefulness.",
            [
                (guids.NewSequentialGuid(), "It is written by somebody who did the thing.", true),
                (guids.NewSequentialGuid(), "It is long.", false),
                (guids.NewSequentialGuid(), "It has a lot of links.", false),
                (guids.NewSequentialGuid(), "It was published recently.", false)
            ]);

        quiz.WithQuestion(
            AcademyQuizQuestionId.From(guids.NewSequentialGuid()),
            "Somebody asks a question you half know the answer to. What do you do?",
            "Saying what you know and what you do not is more useful than a confident guess.",
            [
                (guids.NewSequentialGuid(), "Answer, and say which part you are unsure about.", true),
                (guids.NewSequentialGuid(), "Answer as though you are certain.", false),
                (guids.NewSequentialGuid(), "Say nothing.", false)
            ]);

        repository.AddQuiz(quiz);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSafetyCourseAsync(
        IAcademyRepository repository,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        CancellationToken cancellationToken)
    {
        if (await repository.FindCourseBySlugAsync("staying-safe", cancellationToken) is not null)
        {
            return;
        }

        var course = AcademyCourse.Define(
            AcademyCourseId.From(guids.NewSequentialGuid()),
            "staying-safe",
            "Staying safe",
            "How to spot a scam, and what to do about one.",
            CourseLevel.Beginner,
            estimatedMinutes: 10,
            sortOrder: 1,
            sectionKey: "safety",
            awardsCertificate: true,
            awardsBadgeKey: "safety-aware",
            completionPoints: 40,
            icon: "shield");

        course.WithTag("safety");
        course.SetLessonCount(1);

        repository.AddCourse(course);

        var lesson = AcademyLesson.Define(
            AcademyLessonId.From(guids.NewSequentialGuid()),
            course.Id,
            "spotting-a-scam",
            "Spotting a scam",
            LessonKind.Checklist,
            estimatedMinutes: 10,
            sortOrder: 1);

        lesson
            .WithBlock(LessonBlock.Heading("The four things every scam here has in common", 0))
            .WithBlock(
                LessonBlock.Checklist(1),
                "It is urgent. You have to decide today.",
                "It moves off the platform. WhatsApp, quickly.",
                "The money goes first. A deposit, a fee, a holding payment.",
                "It is slightly too good. Cheaper, faster or easier than everything else.")
            .WithBlock(LessonBlock.Callout(
                CalloutTone.Warning,
                "If somebody asks you to pay off-platform",
                "Stop. Report the listing. You will not get the money back and we cannot help "
                + "you if the payment never went through us.",
                2))
            .WithBlock(LessonBlock.Quote(
                "The ones that work are never obviously fake. They are ordinary, and rushed.",
                "A community moderator",
                3))
            .WithBlock(LessonBlock.Action(
                "Report something", "/community/report", "It takes about a minute.", 4));

        repository.AddLesson(lesson);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
