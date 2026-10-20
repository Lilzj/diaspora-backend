using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Creator.Application.Grants;
using Diaspora.Modules.Creator.Domain.Academy;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Creator routes added on 21 Oct 2026: a course and a lesson read by id, one grant application
/// read by id, and a creator's reputation (the caller's own and anybody's public one).
/// <para>
/// <b>Every refusal is asserted beside the read it refuses</b>, because each of these routes exists
/// to answer one population and must answer everybody else exactly as a missing record — and a suite
/// that only proved the happy path would pass against a route that answered everybody.
/// </para>
/// <para>
/// The database is shared across the run, so nothing here asserts on the size of a shared list:
/// each test makes its own members and finds its own rows by identifier.
/// </para>
/// </summary>
public sealed class CreatorDirectReadAndReputationTests : IdentityTestBase
{
    private const string Creators = "/api/v1/creators";
    private const string Grants = "/api/v1/grants";
    private const string Academy = "/api/v1/academy";
    private const string GuidesRoot = "/api/v1/guides";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CreatorDirectReadAndReputationTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // GET /academy/courses/{id} and GET /academy/lessons/{id}
    // ════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_course_read_by_id_is_the_catalogues_course()
    {
        await AuthenticateAsync();

        var welcome = await WelcomeCourseAsync();

        var course = await ReadDataAsync<CourseDto>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}"));

        course.Should().BeEquivalentTo(welcome, "one course has one projection, however it is reached");
    }

    [Fact]
    public async Task A_lesson_read_by_id_is_the_lesson_lists_lesson_with_progress()
    {
        await AuthenticateAsync();

        var welcome = await WelcomeCourseAsync();

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        var reading = lessons.Single(lesson => lesson.Kind == "Reading");

        var before = await ReadDataAsync<LessonDto>(
            await HttpClient.GetAsync($"{Academy}/lessons/{reading.LessonId}"));

        before.Should().BeEquivalentTo(reading);
        before.Blocks.Should().NotBeEmpty();
        before.IsComplete.Should().BeFalse();

        (await HttpClient.PostAsJsonAsync(
                $"{Academy}/lessons/{reading.LessonId}/complete", new { }, JsonOptions))
            .IsSuccessStatusCode.Should().BeTrue();

        var after = await ReadDataAsync<LessonDto>(
            await HttpClient.GetAsync($"{Academy}/lessons/{reading.LessonId}"));

        after.IsComplete.Should().BeTrue("the read carries this member's own progress");
    }

    /// <summary>The answer key stays on the server whichever route serves the quiz.</summary>
    [Fact]
    public async Task A_quiz_lesson_read_by_id_carries_no_answers()
    {
        await AuthenticateAsync();

        var welcome = await WelcomeCourseAsync();

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        var quizLesson = lessons.Single(lesson => lesson.Kind == "Quiz");

        var response = await HttpClient.GetAsync($"{Academy}/lessons/{quizLesson.LessonId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"quiz\"");
        body.Should().NotContainAny(["isCorrect", "IsCorrect", "correctOptionId", "answerKey"]);
    }

    [Fact]
    public async Task An_unknown_course_or_lesson_is_a_404()
    {
        await AuthenticateAsync();

        (await HttpClient.GetAsync($"{Academy}/courses/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.GetAsync($"{Academy}/lessons/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// <b>A withdrawn course hides its lessons too.</b> The lesson below is itself published; only
    /// its course is not. Checking the lesson alone would serve it by id after the course had left
    /// the catalogue.
    /// </summary>
    [Fact]
    public async Task An_unpublished_course_and_the_lessons_inside_it_are_404s()
    {
        await AuthenticateAsync();

        var (courseId, lessonId) = await SeedUnpublishedCourseAsync();

        (await HttpClient.GetAsync($"{Academy}/courses/{courseId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var lesson = await HttpClient.GetAsync($"{Academy}/lessons/{lessonId}");

        lesson.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await lesson.Content.ReadAsStringAsync()).Should().NotContain(
            "Withdrawn paragraph", "a refused read carries none of the lesson's body");
    }

    [Fact]
    public async Task The_academy_reads_refuse_an_anonymous_caller()
    {
        await AuthenticateAsync();

        var welcome = await WelcomeCourseAsync();

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var lesson = await HttpClient.GetAsync($"{Academy}/lessons/{lessons[0].LessonId}");

        lesson.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await lesson.Content.ReadAsStringAsync()).Should().NotContain("\"blocks\"");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // GET /grants/applications/{id}
    // ════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task An_applicant_reads_their_own_application_by_id()
    {
        await AuthenticateAsync();

        var draft = await DraftApplicationAsync("My paperwork clinic");

        var read = await ReadDataAsync<GrantApplicationDto>(
            await HttpClient.GetAsync($"{Grants}/applications/{draft.ApplicationId}"));

        read.ApplicationId.Should().Be(draft.ApplicationId);
        read.GrantId.Should().Be(draft.GrantId);
        read.State.Should().Be("Draft");
        read.Answers.Should().ContainKey("project-name")
            .WhoseValue.Should().Be("My paperwork clinic");
    }

    /// <summary>Another member is answered exactly as a missing application is.</summary>
    [Fact]
    public async Task Another_members_application_is_a_404_by_id()
    {
        await AuthenticateAsync();

        var draft = await DraftApplicationAsync("Not yours");

        await AuthenticateAsync();

        var theirs = await HttpClient.GetAsync($"{Grants}/applications/{draft.ApplicationId}");
        var missing = await HttpClient.GetAsync($"{Grants}/applications/{Guid.NewGuid()}");

        theirs.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await theirs.Content.ReadAsStringAsync()).Should().NotContain("Not yours");
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_an_application()
    {
        await AuthenticateAsync();

        var draft = await DraftApplicationAsync("Anonymous probe");

        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"{Grants}/applications/{draft.ApplicationId}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// <b>The panel reads what was filed, and never a draft.</b> A reviewer holding
    /// <c>creator:grant:review</c> reads the submitted application; the same reviewer is refused a
    /// draft, as anybody else is.
    /// </summary>
    [Fact]
    public async Task A_reviewer_reads_a_submitted_application_but_not_a_draft()
    {
        await AuthenticateAsync();

        var draft = await DraftApplicationAsync("Saturday paperwork clinic");

        var submitted = await ReadDataAsync<GrantApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Grants}/{draft.GrantId}/applications/submit",
                new { idempotencyKey = Guid.NewGuid().ToString("N") },
                JsonOptions));

        submitted.State.Should().Be("Submitted");

        await AuthenticateAsync();
        var otherDraft = await DraftApplicationAsync("Still drafting");

        var (_, reviewer) = await RegisterVerifiedMemberAsync();
        var reviewerToken = reviewer.Tokens.AccessToken;

        var read = await AsReviewerAsync(
            reviewerToken, new GetGrantApplicationQuery { ApplicationId = submitted.ApplicationId });

        read.IsSuccess.Should().BeTrue($"the panel reads a filed application: {read.Error.Code}");
        read.Value.ApplicationId.Should().Be(submitted.ApplicationId);

        var refused = await AsReviewerAsync(
            reviewerToken, new GetGrantApplicationQuery { ApplicationId = otherDraft.ApplicationId });

        refused.IsFailure.Should().BeTrue("a draft is the applicant's unfinished work");
        refused.Error.Type.Should().Be(Diaspora.SharedKernel.Results.ErrorType.NotFound);

        // And without the permission, the same member is anybody else.
        Api.Authenticate(reviewerToken);

        (await HttpClient.GetAsync($"{Grants}/applications/{submitted.ApplicationId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // GET /creators/me/reputation and GET /creators/{creatorId}/reputation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A new member gets Trust's own answer — a Member on zero — and nothing estimated.</b> The
    /// milestone badges are served unearned with their progress, and reach is declared unavailable
    /// rather than shown as a zero.
    /// </summary>
    [Fact]
    public async Task A_new_member_reads_their_reputation_as_a_member_on_zero()
    {
        await AuthenticateAsync();

        var reputation = await ReadDataAsync<ReputationDto>(
            await HttpClient.GetAsync($"{Creators}/me/reputation"));

        var me = await ReadDataAsync<CreatorProfileDto>(await HttpClient.GetAsync($"{Creators}/me"));

        reputation.CreatorId.Should().Be(me.CreatorId, "the read opens and serves the real record");
        reputation.IsSelf.Should().BeTrue();
        reputation.Score.Should().Be(0);
        reputation.Tier.Should().Be("Member");
        reputation.TierLabel.Should().Be("Member");
        reputation.NextTier.Should().Be("VerifiedResident");
        reputation.PointsToNextTier.Should().Be(100, "Trust's first threshold, served by Trust");

        reputation.Dimensions.Select(dimension => dimension.Key)
            .Should().Equal("contribution", "verification", "trust");
        reputation.Dimensions.Should().OnlyContain(dimension => dimension.MaxScore == null);

        reputation.Signals.Single(signal => signal.Key == "followers").Value.Should().Be(0);
        reputation.Unavailable.Select(item => item.Key).Should().Contain("reach");

        var knowledge = reputation.Badges.Single(badge => badge.Key == "knowledge");

        knowledge.Source.Should().Be("milestone");
        knowledge.IsEarned.Should().BeFalse();
        knowledge.Progress.Should().NotBeNull();
        knowledge.Progress!.Current.Should().Be(0);
        knowledge.Progress.Target.Should().Be(10);
    }

    /// <summary>
    /// <b>Every figure comes from the module that owns it.</b> A guide published through B8 moves
    /// Trust's ledger, the guide counts and the milestone; a follow made through Community's own
    /// route moves the follower count; a view recorded through Guides' route moves the views; a
    /// verification approved through Trust's review awards Trust's badge.
    /// </summary>
    [Fact]
    public async Task Reputation_is_read_from_the_ledger_the_follow_graph_and_the_milestones()
    {
        var (_, author) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(author.Tokens.AccessToken);

        await VerifyIdentityWithTrustAsync();
        Api.Authenticate(author.Tokens.AccessToken);

        var guideId = await PublishGuideAsync("Opening a bank account");
        await Factory.DrainOutboxAsync();

        var me = await ReadDataAsync<CreatorProfileDto>(await HttpClient.GetAsync($"{Creators}/me"));

        await AuthenticateAsync();

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/follows/{author.User.Id}", new { targetType = "member" }))
            .IsSuccessStatusCode.Should().BeTrue();

        (await HttpClient.PostAsJsonAsync(
                $"{GuidesRoot}/{guideId}/view", new { isUniqueView = true }, JsonOptions))
            .IsSuccessStatusCode.Should().BeTrue();

        Api.Authenticate(author.Tokens.AccessToken);

        var mine = await ReadDataAsync<ReputationDto>(
            await HttpClient.GetAsync($"{Creators}/me/reputation"));

        mine.Signals.Single(signal => signal.Key == "followers").Value.Should().Be(1);
        mine.Signals.Single(signal => signal.Key == "guides_published").Value.Should().Be(1);
        mine.Signals.Single(signal => signal.Key == "guide_views").Value.Should().Be(1);
        mine.Signals.Single(signal => signal.Key == "guide_unique_views").Value.Should().Be(1);
        mine.Signals.Single(signal => signal.Key == "content_published").Value.Should().Be(1);

        mine.Dimensions.Single(dimension => dimension.Key == "contribution").Score
            .Should().BeGreaterThan(0, "Trust scored the published guide");
        mine.Dimensions.Single(dimension => dimension.Key == "verification").Score
            .Should().BeGreaterThan(0, "Trust scored the approved identity verification");

        mine.Score.Should().Be(mine.Dimensions.Sum(dimension => dimension.Score),
            "the combined score is the three dimensions and nothing else");

        mine.Badges.Should().Contain(
            badge => badge.Source == "trust" && badge.IsEarned,
            "the approved verification awarded a Trust badge");

        mine.Badges.Single(badge => badge.Key == "knowledge").Progress!.Current
            .Should().Be(1, "one of the ten guides is published");

        var other = await AuthenticateAsync();
        other.Should().NotBeNullOrEmpty();

        var theirs = await ReadDataAsync<ReputationDto>(
            await HttpClient.GetAsync($"{Creators}/{me.CreatorId}/reputation"));

        theirs.IsSelf.Should().BeFalse();
        theirs.Tier.Should().Be(mine.Tier);
        theirs.Score.Should().BeNull();
        theirs.PointsToNextTier.Should().BeNull();
        theirs.Dimensions.Select(dimension => dimension.Key)
            .Should().Equal("contribution", "verification");
        theirs.Unavailable.Select(item => item.Key).Should().Contain(["trust", "score", "reach"]);
        theirs.Badges.Should().OnlyContain(badge => badge.IsEarned,
            "what somebody has not yet earned is theirs to look at");
        theirs.Badges.Should().Contain(badge => badge.Source == "trust");
        theirs.Signals.Single(signal => signal.Key == "followers").Value.Should().Be(1);

        var raw = await (await HttpClient.GetAsync($"{Creators}/{me.CreatorId}/reputation"))
            .Content.ReadAsStringAsync();

        raw.Should().NotContain("\"key\":\"trust\",\"label\":\"Trust\",\"score\"",
            "the trust dimension is withheld, not nulled");
    }

    /// <summary>
    /// <b>The directory's visibility rule.</b> A creator who opted out is a 404 to everybody else and
    /// still themselves to themselves; an unknown creator is the same 404.
    /// </summary>
    [Fact]
    public async Task A_hidden_creators_reputation_is_a_404_to_everybody_but_them()
    {
        var owner = await AuthenticateAsync();

        await HttpClient.PutAsJsonAsync($"{Creators}/me", new { isDiscoverable = false }, JsonOptions);

        var me = await ReadDataAsync<CreatorProfileDto>(await HttpClient.GetAsync($"{Creators}/me"));

        (await HttpClient.GetAsync($"{Creators}/{me.CreatorId}/reputation"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "a creator has not hidden from themselves");

        await AuthenticateAsync();

        (await HttpClient.GetAsync($"{Creators}/{me.CreatorId}/reputation"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.GetAsync($"{Creators}/{Guid.NewGuid()}/reputation"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        owner.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Reputation_refuses_an_anonymous_caller()
    {
        await AuthenticateAsync();

        var me = await ReadDataAsync<CreatorProfileDto>(await HttpClient.GetAsync($"{Creators}/me"));

        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"{Creators}/me/reputation"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await HttpClient.GetAsync($"{Creators}/{me.CreatorId}/reputation"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<string> AuthenticateAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        return auth.Tokens.AccessToken;
    }

    private async Task<CourseDto> WelcomeCourseAsync()
    {
        var courses = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        return courses.Single(course => course.Slug == "welcome-to-diaspora");
    }

    /// <summary>
    /// A course with one published lesson, then the course withdrawn.
    /// <para>
    /// The domain has no unpublish operation — the seeder only ever publishes — so the flag is set
    /// on the row directly. What is under test is the read's handling of a state the schema allows.
    /// </para>
    /// </summary>
    private async Task<(Guid CourseId, Guid LessonId)> SeedUnpublishedCourseAsync()
    {
        Factory.EnsureDatabaseCreated();

        var tag = Guid.NewGuid().ToString("N")[..10];
        var courseId = AcademyCourseId.From(Guid.NewGuid());
        var lessonId = AcademyLessonId.From(Guid.NewGuid());

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var course = AcademyCourse.Define(
            courseId, $"withdrawn-{tag}", "Withdrawn course", "A course taken down.",
            CourseLevel.Beginner, estimatedMinutes: 5, sortOrder: 999);

        var lesson = AcademyLesson
            .Define(lessonId, courseId, $"withdrawn-{tag}-1", "Withdrawn lesson",
                LessonKind.Reading, estimatedMinutes: 5, sortOrder: 0)
            .WithBlock(LessonBlock.Paragraph("Withdrawn paragraph", 0));

        dbContext.Add(course);
        dbContext.Add(lesson);
        await dbContext.SaveChangesAsync();

        dbContext.Entry(course).Property(nameof(AcademyCourse.IsPublished)).CurrentValue = false;
        await dbContext.SaveChangesAsync();

        return (courseId.Value, lessonId.Value);
    }

    /// <summary>
    /// Drafts an application for the authenticated member, after making them a contributor — the
    /// community project fund refuses anybody who has contributed nothing.
    /// </summary>
    private async Task<GrantApplicationDto> DraftApplicationAsync(string projectName)
    {
        await PublishGuideAsync("A first contribution");
        await Factory.DrainOutboxAsync();

        var grants = await ReadDataAsync<List<GrantDto>>(await HttpClient.GetAsync(Grants));
        var fund = grants.Single(grant => grant.Key == "community-project-fund");

        return await ReadDataAsync<GrantApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Grants}/{fund.GrantId}/applications",
                new
                {
                    answers = new Dictionary<string, string>
                    {
                        ["project-name"] = projectName,
                        ["summary"] = "Two hours a week helping people with forms.",
                        ["people-reached"] = "40",
                        ["category"] = "legal",
                        ["budget"] = "Room hire and printing."
                    },
                    requestedAmountMinorUnits = 60_000,
                    acceptedDeclarations = new[] { "declaration-1", "declaration-2" }
                },
                JsonOptions));
    }

    /// <summary>
    /// Runs a read as a grant reviewer: a real member holding <c>creator:grant:review</c>, through
    /// the whole pipeline. In a scope rather than over HTTP for the reason the Creator Platform suite
    /// records — this suite has no staff role to grant a token.
    /// </summary>
    private async Task<Diaspora.SharedKernel.Results.Result<TResponse>> AsReviewerAsync<TResponse>(
        string reviewerToken, Diaspora.Application.Abstractions.IQuery<TResponse> query)
    {
        using var scope = Factory.Services.CreateScope();

        ScopedCurrentUser.Apply(scope.ServiceProvider, reviewerToken, "creator:grant:review");

        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();

        return await sender.Send(query);
    }

    /// <summary>Takes the authenticated member through Trust's own identity verification.</summary>
    private async Task VerifyIdentityWithTrustAsync()
    {
        var submitted = await ReadDataAsync<TrustVerificationDto>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/trust/verifications/identity",
                new
                {
                    documentType = "Passport",
                    issueCountry = "NG",
                    documentReference = $"media:{Guid.NewGuid():N}",
                    selfieReference = $"media:{Guid.NewGuid():N}",
                    documentNumber = "A0000000",
                    expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5))
                },
                JsonOptions));

        var (_, reviewer) = await RegisterVerifiedMemberAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            ScopedCurrentUser.Apply(
                scope.ServiceProvider, reviewer.Tokens.AccessToken, "trust:verification:review");

            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();

            var reviewed = await sender.Send(
                new Diaspora.Modules.Trust.Application.Verifications.ReviewVerificationCommand
                {
                    VerificationId = submitted.VerificationId,
                    Decision = Diaspora.Modules.Trust.Application.Verifications.ReviewDecision.Approve,
                    Notes = "Integration suite."
                });

            reviewed.IsSuccess.Should().BeTrue(
                $"the Trust review should have succeeded: {reviewed.Error.Code}");
        }

        await Factory.DrainOutboxAsync();
    }

    private async Task<Guid> PublishGuideAsync(string title)
    {
        var draft = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                GuidesRoot,
                new { title, categoryKey = "getting-started", language = "en", countryCode = "GB" },
                JsonOptions));

        await HttpClient.PutAsJsonAsync(
            $"{GuidesRoot}/{draft.GuideId}",
            new
            {
                title,
                countryCode = "GB",
                sections = new[]
                {
                    new
                    {
                        sectionId = Guid.NewGuid(),
                        title = "What to do",
                        order = 0,
                        blocks = new[]
                        {
                            new
                            {
                                blockId = Guid.NewGuid(),
                                kind = "Markdown",
                                order = 0,
                                content = "Take the form, the fee and one hour of patience."
                            }
                        }
                    }
                }
            },
            JsonOptions);

        var published = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                $"{GuidesRoot}/{draft.GuideId}/publish",
                new { revisionNotes = "First edition." },
                JsonOptions));

        return published.GuideId;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, but answered {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record GuideDto(Guid GuideId, string Status);

    private sealed record TrustVerificationDto(Guid VerificationId, string Status);

    private sealed record CreatorProfileDto(Guid CreatorId, Guid UserId);

    private sealed record GrantDto(Guid GrantId, string Key);

    private sealed record GrantApplicationDto(
        Guid ApplicationId, Guid GrantId, string State, Dictionary<string, string> Answers);

    private sealed record CourseProgressDto(
        Guid CourseId, string State, int LessonsCompleted, int LessonCount, int Percentage);

    private sealed record CourseDto(
        Guid CourseId,
        string Slug,
        string Title,
        string Level,
        int LessonCount,
        bool AwardsCertificate,
        CourseProgressDto? Progress,
        bool IsSaved,
        int ContentVersion);

    private sealed record LessonBlockDto(
        string Kind, string? Text, string? Title, List<string>? Items);

    private sealed record LessonDto(
        Guid LessonId,
        Guid CourseId,
        string Title,
        string Kind,
        List<LessonBlockDto> Blocks,
        bool IsComplete,
        int ContentVersion);

    private sealed record ProgressDto(long Current, long Target, int Percentage);

    private sealed record DimensionDto(string Key, string Label, int Score, int? MaxScore);

    private sealed record BadgeDto(
        string BadgeId, string Key, string Label, bool IsEarned, string Source, ProgressDto? Progress);

    private sealed record SignalDto(string Key, string Label, long Value, string Source);

    private sealed record UnavailableDto(string Key, string Label, string Reason);

    private sealed record ReputationDto(
        Guid CreatorId,
        Guid UserId,
        bool IsSelf,
        int? Score,
        string Tier,
        string TierLabel,
        List<DimensionDto> Dimensions,
        List<BadgeDto> Badges,
        int? PointsToNextTier,
        string? NextTier,
        string? NextTierLabel,
        List<SignalDto> Signals,
        List<UnavailableDto> Unavailable);
}
