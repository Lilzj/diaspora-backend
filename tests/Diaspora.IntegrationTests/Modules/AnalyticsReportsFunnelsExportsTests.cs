using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Analytics.Application.Projection;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Saved reports, funnels, exports and the community dataset — FRONTEND-PHASE15-GAP-027, -016, -026
/// and -003 — over real HTTP against real rows.
/// <para>
/// <b>Every figure is produced by an action.</b> Jobs and homes are published as their modules' own
/// contracts through the real outbox, views arrive through the telemetry endpoint, members join a
/// community through its own route. Nothing writes an analytics row directly.
/// </para>
/// <para>
/// <b>Every predicate from both sides.</b> An owner and a stranger, a person who viewed and applied and
/// one who applied without a recorded view, a member who stayed and one who left.
/// </para>
/// </summary>
public sealed class AnalyticsReportsFunnelsExportsTests : IdentityTestBase
{
    private const string Analytics = "/api/v1/analytics";
    private const string Telemetry = "/api/v1/telemetry";
    private const string Communities = "/api/v1/communities";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AnalyticsReportsFunnelsExportsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Funnels — FRONTEND-PHASE15-GAP-016
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A job's funnel counts people who opened it and then applied, and names the steps nothing
    /// records.</b> Two people open the job and one of them applies; a third applies without a
    /// recorded opening and is not counted at "Applied", because the journey cannot be followed.
    /// </summary>
    [Fact]
    public async Task A_job_funnel_counts_viewers_then_applicants_and_lists_unrecorded_steps()
    {
        var (employer, jobId) = await PublishedJobAsync();

        var applicant = await NewMemberAsync();
        await PostViewAsync(jobId, "job");
        await PublishApplicationAsync(jobId, applicant.UserId);

        await NewMemberAsync();
        await PostViewAsync(jobId, "job");

        var blindApplicant = await NewMemberAsync();
        await PublishApplicationAsync(jobId, blindApplicant.UserId);

        await Factory.DrainOutboxAsync();
        await ProjectAsync();

        Api.Authenticate(employer.Token);

        var funnel = await ReadDataAsync<FunnelDto>(
            await HttpClient.GetAsync($"{Analytics}/funnels/job?subjectType=job&subjectId={jobId}&period=30d"));

        funnel.Key.Should().Be("job");
        funnel.Source.Should().Be("backend");
        funnel.Steps.Select(step => step.Key).Should().Equal("view", "apply");
        funnel.Steps[0].Count.Should().Be(2, "two people opened the job");
        funnel.Steps[1].Count.Should().Be(1, "one of them applied; the third applicant was never seen opening it");
        funnel.Steps[1].ConversionFromPrevious.Should().BeApproximately(0.5, 1e-9);
        funnel.Steps[1].DropOffFromPrevious.Should().BeApproximately(0.5, 1e-9);
        funnel.UnavailableSteps.Select(step => step.Key).Should().Equal("shortlist", "interview", "hire");
        funnel.UnavailableSteps.Should().OnlyContain(step => !string.IsNullOrWhiteSpace(step.Reason));

        // The employer's own jobs, asked as the role subject with the member's own id, agree.
        var asEmployer = await ReadDataAsync<FunnelDto>(
            await HttpClient.GetAsync(
                $"{Analytics}/funnels/job?subjectType=employer&subjectId={employer.UserId}&period=30d"));

        asEmployer.Steps.Select(step => step.Count).Should().Equal(2, 1);

        // A journey that does not apply to the subject is refused, and an unknown one is not invented.
        var mismatch = await HttpClient.GetAsync(
            $"{Analytics}/funnels/marketplace?subjectType=job&subjectId={jobId}&period=30d");
        mismatch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await mismatch.Content.ReadAsStringAsync()).Should().Contain("analytics.funnel.subject-mismatch");

        var unknown = await HttpClient.GetAsync($"{Analytics}/funnels/onboarding?period=30d");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknown.Content.ReadAsStringAsync()).Should().Contain("analytics.funnel.unknown");

        // Somebody else's job, and somebody else's employer standing, answer as if absent.
        Api.Authenticate(applicant.Token);

        (await HttpClient.GetAsync($"{Analytics}/funnels/job?subjectType=job&subjectId={jobId}&period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.GetAsync(
                $"{Analytics}/funnels/job?subjectType=employer&subjectId={employer.UserId}&period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownEmployerStanding = await ReadDataAsync<FunnelDto>(
            await HttpClient.GetAsync($"{Analytics}/funnels/job?subjectType=employer&period=30d"));

        ownEmployerStanding.Steps.Should().OnlyContain(step => step.Count == 0, "the applicant posts no jobs");
    }

    /// <summary>
    /// <b>A landlord's funnel ends at a booked viewing, and the steps between are siblings.</b> A
    /// viewing is booked without a message, so "Enquired" is zero while "Viewing booked" is one — and
    /// no rate is published between two steps that are not nested.
    /// </summary>
    [Fact]
    public async Task A_housing_funnel_counts_a_viewing_without_an_enquiry_and_publishes_no_sibling_rate()
    {
        var landlord = await NewMemberAsync();
        var homeId = Guid.NewGuid();
        var landlordProfileId = Guid.NewGuid();

        await PublishHomeAsync(homeId, landlordProfileId, landlord.UserId);
        await Factory.DrainOutboxAsync();

        var seeker = await NewMemberAsync();
        await PostViewAsync(homeId, "housing");
        await PublishViewingAsync(homeId, landlordProfileId, seeker.UserId);

        await Factory.DrainOutboxAsync();
        await ProjectAsync();

        Api.Authenticate(landlord.Token);

        var funnel = await ReadDataAsync<FunnelDto>(
            await HttpClient.GetAsync($"{Analytics}/funnels/housing?subjectType=landlord&period=30d"));

        funnel.Steps.Select(step => step.Key).Should().Equal("view", "save", "inquiry", "viewing");
        funnel.Steps.Select(step => step.Count).Should().Equal(1, 0, 0, 1);

        var viewing = funnel.Steps.Single(step => step.Key == "viewing");
        viewing.ConversionFromPrevious.Should().BeNull("an enquiry is not a precondition of a viewing");
        viewing.ConversionFromStart.Should().BeApproximately(1, 1e-9);

        funnel.UnavailableSteps.Select(step => step.Key).Should().Contain(["impression", "application"]);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Saved reports — FRONTEND-PHASE15-GAP-027
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A saved report round-trips, changes, and belongs to its owner alone.</b> Every route answers
    /// a stranger exactly as it answers for a report that does not exist.
    /// </summary>
    [Fact]
    public async Task A_saved_report_is_created_read_changed_and_deleted_by_its_owner_only()
    {
        var owner = await NewMemberAsync();

        var created = await HttpClient.PostAsJsonAsync(
            $"{Analytics}/reports",
            new
            {
                name = "  Weekly hiring  ",
                description = "Applications against views",
                query = Query("employer", owner.UserId.ToString(), ["content.views", "conversion.count"]),
                visualization = "line",
                sharing = "private"
            },
            JsonOptions);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var report = await ReadDataAsync<ReportDto>(created);

        report.Name.Should().Be("Weekly hiring");
        report.IsOwnedByCaller.Should().BeTrue();
        report.IsFavorite.Should().BeFalse();
        report.Sharing.Should().Be("private");
        report.Visualization.Should().Be("line");
        report.Query.Subject.Type.Should().Be("employer");
        report.Query.Metrics.Should().Equal("content.views", "conversion.count");
        report.Query.Range.Timezone.Should().Be("Europe/London");

        var listed = await ReadDataAsync<List<ReportDto>>(await HttpClient.GetAsync($"{Analytics}/reports"));
        listed.Should().ContainSingle().Which.ReportId.Should().Be(report.ReportId);

        var changed = await ReadDataAsync<ReportDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Analytics}/reports/{report.ReportId}",
                new { name = "Hiring, weekly", isFavorite = true },
                JsonOptions));

        changed.Name.Should().Be("Hiring, weekly");
        changed.IsFavorite.Should().BeTrue();
        changed.Visualization.Should().Be("line", "a field left out of the body is left as it was");
        changed.Query.Metrics.Should().Equal("content.views", "conversion.count");

        (await ReadDataAsync<List<ReportDto>>(await HttpClient.GetAsync($"{Analytics}/reports?scope=favorites")))
            .Should().ContainSingle();
        (await ReadDataAsync<List<ReportDto>>(await HttpClient.GetAsync($"{Analytics}/reports?scope=shared")))
            .Should().BeEmpty("nothing is shared on this deployment");

        // A stranger: not listed, not readable, not changeable, not deletable.
        await NewMemberAsync();

        (await ReadDataAsync<List<ReportDto>>(await HttpClient.GetAsync($"{Analytics}/reports"))).Should().BeEmpty();
        (await HttpClient.GetAsync($"{Analytics}/reports/{report.ReportId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await HttpClient.PutAsJsonAsync($"{Analytics}/reports/{report.ReportId}", new { name = "Mine now" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await HttpClient.DeleteAsync($"{Analytics}/reports/{report.ReportId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        Api.Authenticate(owner.Token);

        (await ReadDataAsync<ReportDto>(await HttpClient.GetAsync($"{Analytics}/reports/{report.ReportId}")))
            .Name.Should().Be("Hiring, weekly", "the stranger's write did not land");

        var deleted = await ReadDataAsync<DeletedDto>(await HttpClient.DeleteAsync($"{Analytics}/reports/{report.ReportId}"));
        deleted.ReportId.Should().Be(report.ReportId);

        (await HttpClient.GetAsync($"{Analytics}/reports/{report.ReportId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary><b>What cannot be kept is refused with a reason</b>, and nothing is saved for it.</summary>
    [Fact]
    public async Task A_report_that_cannot_be_kept_is_refused_with_its_reason()
    {
        var owner = await NewMemberAsync();

        async Task<string> RefusedAsync(object body)
        {
            var response = await HttpClient.PostAsJsonAsync($"{Analytics}/reports", body, JsonOptions);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            return await response.Content.ReadAsStringAsync();
        }

        var query = Query("creator", null, []);

        (await RefusedAsync(new { name = "Shared", query, visualization = "line", sharing = "platform" }))
            .Should().Contain("analytics.report.sharing-unsupported");
        (await RefusedAsync(new { name = " ", query, visualization = "line" }))
            .Should().Contain("analytics.report.invalid");
        (await RefusedAsync(new { name = "Odd", query = Query("galaxy", null, []), visualization = "line" }))
            .Should().Contain("analytics.report.invalid");
        (await RefusedAsync(new { name = "Odd", query, visualization = "hologram" }))
            .Should().Contain("analytics.report.invalid");

        (await ReadDataAsync<List<ReportDto>>(await HttpClient.GetAsync($"{Analytics}/reports")))
            .Should().BeEmpty();

        owner.Should().NotBeNull();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Exports — FRONTEND-PHASE15-GAP-026
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>An owner exports their own dataset; the file opens from the link alone, and nobody else can
    /// reach it.</b> The figures in the file are the dashboard's figures, because the export reads
    /// the dashboard's own query.
    /// </summary>
    [Fact]
    public async Task An_owner_exports_their_dataset_as_csv_and_only_the_link_or_the_owner_can_open_it()
    {
        var (employer, jobId) = await PublishedJobAsync();

        var viewer = await NewMemberAsync();
        await PostViewAsync(jobId, "job");
        await Factory.DrainOutboxAsync();
        await ProjectAsync();

        Api.Authenticate(employer.Token);

        var access = await ReadDataAsync<AccessDto>(
            await HttpClient.GetAsync($"{Analytics}/access?subjectType=employer&subjectId={employer.UserId}"));

        access.CanView.Should().BeTrue();
        access.CanExport.Should().BeTrue("an owner may export what they may view");
        access.CanBuildReports.Should().BeTrue();

        var requested = await HttpClient.PostAsJsonAsync(
            $"{Analytics}/exports",
            new { query = Query("employer", employer.UserId.ToString(), []), format = "csv" },
            JsonOptions);

        requested.StatusCode.Should().Be(HttpStatusCode.Created);

        var job = await ReadDataAsync<ExportDto>(requested);

        job.Status.Should().Be("ready");
        job.Format.Should().Be("csv");
        job.DownloadUrl.Should().StartWith($"{Analytics}/exports/{job.ExportId}/download?token=");
        job.SizeBytes.Should().BeGreaterThan(0);
        job.ExpiresAt.Should().BeAfter(job.CompletedAt!.Value);

        var polled = await ReadDataAsync<ExportDto>(await HttpClient.GetAsync($"{Analytics}/exports/{job.ExportId}"));
        polled.Status.Should().Be("ready");
        polled.DownloadUrl.Should().Be(job.DownloadUrl);

        // The link alone, from a client with no session.
        using var anonymous = Factory.CreateClient();

        var file = await anonymous.GetAsync(job.DownloadUrl);
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var csv = await file.Content.ReadAsStringAsync();
        csv.Should().StartWith("kind,key,label,date,value");
        csv.Should().Contain("metric,content.views,Views,,1,count", "one member opened the job");

        // A guessed token, and no token without a session, open nothing.
        (await anonymous.GetAsync($"{Analytics}/exports/{job.ExportId}/download?token=guess"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync($"{Analytics}/exports/{job.ExportId}/download"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The owner's session is enough on its own.
        (await HttpClient.GetAsync($"{Analytics}/exports/{job.ExportId}/download"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // A stranger can neither poll the job nor export the employer's figures or the job's.
        Api.Authenticate(viewer.Token);

        (await HttpClient.GetAsync($"{Analytics}/exports/{job.ExportId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.PostAsJsonAsync(
                $"{Analytics}/exports",
                new { query = Query("employer", employer.UserId.ToString(), []), format = "csv" },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.PostAsJsonAsync(
                $"{Analytics}/exports",
                new { query = Query("job", jobId.ToString(), []), format = "csv" },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var strangerAccess = await ReadDataAsync<AccessDto>(
            await HttpClient.GetAsync($"{Analytics}/access?subjectType=job&subjectId={jobId}"));
        strangerAccess.CanExport.Should().BeFalse();

        var pdf = await HttpClient.PostAsJsonAsync(
            $"{Analytics}/exports", new { query = Query("creator", null, []), format = "pdf" }, JsonOptions);
        pdf.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await pdf.Content.ReadAsStringAsync()).Should().Contain("analytics.export.format-unsupported");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Community — FRONTEND-PHASE15-GAP-003
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A community's owner reads its members, joins and net change; an ordinary member reads
    /// nothing.</b> Two members join through the community's own route and one leaves, so the figures
    /// move from both sides: joins three (the founder among them), net two, members two.
    /// </summary>
    [Fact]
    public async Task A_community_owner_reads_membership_figures_and_an_ordinary_member_cannot()
    {
        var owner = await NewMemberAsync();
        var slug = await SeedCommunityAsync("Analytics circle", owner.UserId);

        var stayer = await NewMemberAsync();
        await JoinAsync(slug);

        await NewMemberAsync();
        await JoinAsync(slug);
        (await HttpClient.DeleteAsync($"{Communities}/{slug}/membership?action=leave"))
            .IsSuccessStatusCode.Should().BeTrue();

        Api.Authenticate(owner.Token);

        var dataset = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/communities/{slug}?period=30d"));

        dataset.Subject.Type.Should().Be("community");
        dataset.Subject.Label.Should().Be("Analytics circle");
        dataset.Status.Should().Be("PARTIAL", "activity is not recorded per community");

        Value(dataset, "reach.members").Should().Be(2, "the founder and the member who stayed");
        Value(dataset, "audience.users.new").Should().Be(3, "the founder and two joins");
        Value(dataset, "growth.members.net").Should().Be(2, "three joins, one departure");

        dataset.Series.Should().ContainSingle(series => series.Key == "audience.users.new")
            .Which.Points.Sum(point => point.Value).Should().Be(3);
        dataset.UnavailableMetrics.Should().Contain("growth.members.rate");

        // The same community by its id answers the same.
        var byId = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/communities/{dataset.Subject.Id}?period=30d"));
        Value(byId, "reach.members").Should().Be(2);

        var ownerAccess = await ReadDataAsync<AccessDto>(
            await HttpClient.GetAsync($"{Analytics}/access?subjectType=community&subjectId={slug}"));
        ownerAccess.CanView.Should().BeTrue();
        ownerAccess.CanExport.Should().BeTrue();

        // An ordinary member, and a community that does not exist, answer alike.
        Api.Authenticate(stayer.Token);

        (await HttpClient.GetAsync($"{Analytics}/communities/{slug}?period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync($"{Analytics}/communities/no-such-community?period=30d"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var memberAccess = await ReadDataAsync<AccessDto>(
            await HttpClient.GetAsync($"{Analytics}/access?subjectType=community&subjectId={slug}"));
        memberAccess.CanView.Should().BeFalse();
        memberAccess.CanExport.Should().BeFalse();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static object Query(string subjectType, string? subjectId, string[] metrics) => new
    {
        subject = new { type = subjectType, id = subjectId },
        metrics,
        range = new
        {
            preset = "last30d",
            from = DateTime.UtcNow.AddDays(-29).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            to = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            timezone = "Europe/London",
            isDeviceTimezone = false
        },
        comparison = "previousPeriod",
        includeSeries = true
    };

    private static double Value(DatasetDto dataset, string key) =>
        dataset.Metrics.Single(metric => metric.Key == key).Value.Value;

    private async Task<Member> NewMemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        return new Member(auth.Tokens.AccessToken, auth.User.Id);
    }

    private async Task<(Member Employer, Guid JobId)> PublishedJobAsync()
    {
        var employer = await NewMemberAsync();
        var jobId = Guid.NewGuid();

        using (var scope = Factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await publisher.PublishAsync(new JobPublishedIntegrationEvent
            {
                JobId = jobId,
                EmployerId = EmployerProfileId,
                EmployerUserId = employer.UserId,
                EmployerName = "Harbour Care",
                IsEmployerVerified = true,
                Title = "Community nurse, night shifts",
                Slug = $"community-nurse-{jobId:N}",
                CategoryKey = "healthcare",
                Tags = [],
                SkillKeys = [],
                CountryCode = "GB",
                WorkplaceType = "OnSite",
                JobType = "FullTime",
                ExperienceLevel = "Mid",
                OffersVisaSponsorship = true,
                OffersRelocationAssistance = false,
                IsSalaryDisclosed = false,
                VacancyCount = 1,
                IsFirstPublication = true,
                PublishedAt = DateTimeOffset.UtcNow
            });

            await unitOfWork.SaveChangesAsync();
        }

        await Factory.DrainOutboxAsync();

        return (employer, jobId);
    }

    /// <summary>The employer profile, deliberately not the member's id. See AnalyticsPlatformTests.</summary>
    private static readonly Guid EmployerProfileId = Guid.NewGuid();

    private async Task PublishApplicationAsync(Guid jobId, Guid applicantUserId)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await publisher.PublishAsync(new ApplicationSubmittedIntegrationEvent
        {
            ApplicationId = Guid.NewGuid(),
            JobId = jobId,
            EmployerId = EmployerProfileId,
            CandidateId = Guid.NewGuid(),
            ApplicantUserId = applicantUserId,
            EmployerUserId = null,
            CountryCode = "GB",
            SubmittedAt = DateTimeOffset.UtcNow
        });

        await unitOfWork.SaveChangesAsync();
    }

    private async Task PublishHomeAsync(Guid homeId, Guid landlordProfileId, Guid landlordUserId)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await publisher.PublishAsync(new HousingPublishedIntegrationEvent
        {
            ListingId = homeId,
            LandlordId = landlordProfileId,
            LandlordUserId = landlordUserId,
            LandlordName = "Harbour Lettings",
            LandlordKind = "Individual",
            IsLandlordVerified = true,
            Title = "Two-bed flat near the station",
            Slug = $"two-bed-flat-{homeId:N}",
            AccommodationTypeKey = "apartment",
            IsRentDisclosed = false,
            Bedrooms = 2,
            Bathrooms = 1,
            FurnishedStatus = "Furnished",
            TotalUnits = 1,
            AcceptsShortStay = false,
            AcceptsLongStay = true,
            IsStudentFriendly = false,
            IsFamilyFriendly = true,
            IsSharedAccommodation = false,
            CountryCode = "GB",
            IsFirstPublication = true,
            PublishedAt = DateTimeOffset.UtcNow
        });

        await unitOfWork.SaveChangesAsync();
    }

    private async Task PublishViewingAsync(Guid homeId, Guid landlordProfileId, Guid seekerUserId)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTimeOffset.UtcNow;

        await publisher.PublishAsync(new ViewingScheduledIntegrationEvent
        {
            ViewingId = Guid.NewGuid(),
            ListingId = homeId,
            LandlordId = landlordProfileId,
            SeekerUserId = seekerUserId,
            ScheduledFor = now.AddDays(3),
            DurationMinutes = 30,
            IsVirtual = false,
            ScheduledAt = now
        });

        await unitOfWork.SaveChangesAsync();
    }

    private async Task PostViewAsync(Guid contentId, string contentType)
    {
        var response = await HttpClient.PostAsJsonAsync(
            $"{Telemetry}/events",
            new
            {
                events = new[]
                {
                    new
                    {
                        @event = "content.viewed",
                        occurredAt = DateTimeOffset.UtcNow,
                        properties = new { contentId = contentId.ToString(), contentType }
                    }
                }
            },
            JsonOptions);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    private async Task ProjectAsync()
    {
        for (var pass = 0; pass < 10; pass++)
        {
            using var scope = Factory.Services.CreateScope();

            if (await scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>().RunOnceAsync() == 0)
            {
                return;
            }
        }
    }

    /// <summary>A live, open community whose founder is <paramref name="ownerUserId"/>.</summary>
    private async Task<string> SeedCommunityAsync(string name, Guid ownerUserId)
    {
        var slug = CommunitySlug.FromName($"{name}-{Guid.NewGuid():N}").Value;

        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var memberships = scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var community = CommunityAggregate.Propose(
            CommunityId.New(), slug, name,
            CommunityType.NationalDiaspora, CommunityVisibility.Public, "NG").Value;

        community.Open();
        community.SetJoinPolicy(false);
        communities.Add(community);

        memberships.Add(CommunityMembership.Found(
            CommunityMembershipId.New(), community, UserId.From(ownerUserId), DateTimeOffset.UtcNow).Value);

        await unitOfWork.SaveChangesAsync();

        return slug.Value;
    }

    private async Task JoinAsync(string slug)
    {
        var response = await HttpClient.PostAsJsonAsync(
            $"{Communities}/{slug}/membership", new { action = "join" }, JsonOptions);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
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

    private sealed record Member(string Token, Guid UserId);

    private sealed record FunnelStepDto(
        string Key,
        string Label,
        long Count,
        double? ConversionFromPrevious,
        double? ConversionFromStart,
        double? DropOffFromPrevious);

    private sealed record UnavailableStepDto(string Key, string Label, string Reason);

    private sealed record FunnelDto(
        string Key,
        string Label,
        IReadOnlyList<FunnelStepDto> Steps,
        IReadOnlyList<UnavailableStepDto> UnavailableSteps,
        string Source,
        string Computation);

    private sealed record SubjectDto(string Type, string? Id, string? Label);

    private sealed record RangeDto(string? Preset, string From, string To, string Timezone);

    private sealed record QueryDto(SubjectDto Subject, IReadOnlyList<string> Metrics, RangeDto Range);

    private sealed record ReportDto(
        string ReportId,
        string Name,
        string? Description,
        QueryDto Query,
        string Visualization,
        string OwnerId,
        bool IsOwnedByCaller,
        bool IsFavorite,
        string Sharing,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record DeletedDto(string ReportId);

    private sealed record ExportDto(
        string ExportId,
        string Format,
        string Status,
        QueryDto Query,
        DateTimeOffset RequestedAt,
        DateTimeOffset? CompletedAt,
        string? DownloadUrl,
        long? SizeBytes,
        DateTimeOffset? ExpiresAt);

    private sealed record AccessDto(bool CanView, bool CanExport, bool CanBuildReports);

    private sealed record SourcedDto(double Value, string Source);

    private sealed record MetricDto(string Key, string Label, SourcedDto Value);

    private sealed record PointDto(string At, double Value);

    private sealed record SeriesDto(string Key, IReadOnlyList<PointDto> Points);

    private sealed record DatasetDto(
        SubjectDto Subject,
        string Status,
        IReadOnlyList<MetricDto> Metrics,
        IReadOnlyList<SeriesDto> Series,
        IReadOnlyList<string> UnavailableMetrics);
}
