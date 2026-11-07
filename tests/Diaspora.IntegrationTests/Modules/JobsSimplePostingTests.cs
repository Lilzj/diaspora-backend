using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// <b>Simple jobs</b> (owner's request, 4 Nov 2026): "Two cleaners needed in Port Louis, 200
/// rupees a day, one day." Short one-off work did not fit the full posting, which had no
/// start date, no length, and no way to count places off as people were taken. A simple job
/// says when the work is and for how long, stops taking interest once the work is over, and
/// closes itself when its last place is filled.
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class JobsSimplePostingTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/jobs";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobsSimplePostingTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_simple_job_says_when_the_work_is_and_stops_taking_interest_after_it()
    {
        Factory.EnsureDatabaseCreated();

        var startsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3));
        var job = await PostSimpleJobAsync("Two cleaners needed", startsOn, lengthAmount: 2, lengthUnit: "Days");

        var read = await ReadJsonAsync(await HttpClient.GetAsync($"{Root}/{job.JobId}"));

        read.GetProperty("postingMode").GetString().Should().Be("Simple");
        read.GetProperty("startsOn").GetString().Should().Be(startsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        read.GetProperty("lengthAmount").GetInt32().Should().Be(2);
        read.GetProperty("lengthUnit").GetString().Should().Be("Days");
        read.GetProperty("vacancyCount").GetInt32().Should().Be(2);
        read.GetProperty("closingAt").GetDateTimeOffset().Should().Be(
            new DateTimeOffset(startsOn.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            "a two-day job stops taking interest at the end of its second day");

        var cards = await ReadJsonAsync(await HttpClient.GetAsync($"{Root}/?country=MU&limit=50"));
        var card = cards.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("jobId").GetGuid() == job.JobId);

        card.GetProperty("postingMode").GetString().Should().Be("Simple", "a card is told it is a simple job");
        card.GetProperty("startsOn").GetString().Should().Be(startsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        card.GetProperty("remainingVacancies").GetInt32().Should().Be(2, "a card says how many places are left");
    }

    [Fact]
    public async Task The_poster_fills_places_one_by_one_and_the_last_one_closes_the_job()
    {
        Factory.EnsureDatabaseCreated();

        var job = await PostSimpleJobAsync(
            "One builder for a day", DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)), 1, "Days", people: 2);

        // A member says they are interested: an application with no CV and no profile set up.
        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync($"{Root}/{job.JobId}/apply", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Created, "interest needs no work profile");
        (await HttpClient.PostAsJsonAsync($"{Root}/{job.JobId}/apply", new { }))
            .IsSuccessStatusCode.Should().BeFalse("the same member cannot say it twice");

        Api.Authenticate(job.PosterToken);

        var first = await HttpClient.PostAsync($"{Root}/{job.JobId}/places/fill", content: null);
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());

        var afterFirst = await ReadJsonAsync(first);
        afterFirst.GetProperty("remainingVacancies").GetInt32().Should().Be(1);
        afterFirst.GetProperty("status").GetString().Should().NotBe("Filled", "one place is still open");

        var last = await ReadJsonAsync(
            await HttpClient.PostAsync($"{Root}/{job.JobId}/places/fill", content: null));
        last.GetProperty("remainingVacancies").GetInt32().Should().Be(0);
        last.GetProperty("status").GetString().Should().Be("Filled", "the last place filled closes the job");

        (await HttpClient.PostAsync($"{Root}/{job.JobId}/places/fill", content: null))
            .IsSuccessStatusCode.Should().BeFalse("there is no place left to fill");

        var (_, late) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(late.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync($"{Root}/{job.JobId}/apply", new { }))
            .IsSuccessStatusCode.Should().BeFalse("a filled job takes no more interest");

        (await HttpClient.PostAsync($"{Root}/{job.JobId}/places/fill", content: null))
            .IsSuccessStatusCode.Should().BeFalse("only the poster can count places off");
    }

    /// <summary>
    /// Found on production, 6 Nov 2026: an Admin's card offered "I'm interested" and the page
    /// said "YOUR JOB — You manage this job for Zoe" with the button gone. The page read
    /// <c>canManage</c>, which was then true for staff on every job, as "this is mine". Since
    /// 7 Nov 2026 staff no longer pass the owner's routes, so <c>canManage</c> is the team's alone
    /// and staff are told apart by <c>canModerate</c>.
    /// </summary>
    [Fact]
    public async Task Staff_moderate_any_job_but_do_not_manage_it_and_can_still_say_they_are_interested()
    {
        Factory.EnsureDatabaseCreated();

        var job = await PostSimpleJobAsync(
            "Two cleaners for a day", DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)), 1, "Days");

        Api.Authenticate(job.PosterToken);
        var asPoster = await ReadJsonAsync(await HttpClient.GetAsync($"{Root}/{job.JobId}"));
        asPoster.GetProperty("canManage").GetBoolean().Should().BeTrue();
        asPoster.GetProperty("canModerate").GetBoolean().Should().BeFalse("the poster is not staff");
        asPoster.GetProperty("isOnEmployerTeam").GetBoolean().Should().BeTrue("the poster is the employer's team");

        var filled = await ReadJsonAsync(await HttpClient.PostAsync($"{Root}/{job.JobId}/places/fill", content: null));
        filled.GetProperty("isOnEmployerTeam").GetBoolean().Should().BeTrue("a write answers the same question as a read");

        await SignInAsRoleAsync("Moderator");
        var asStaff = await ReadJsonAsync(await HttpClient.GetAsync($"{Root}/{job.JobId}"));
        asStaff.GetProperty("canManage").GetBoolean().Should().BeFalse("the owner's routes refuse staff");
        asStaff.GetProperty("canModerate").GetBoolean().Should().BeTrue("staff moderate every job");
        asStaff.GetProperty("isOnEmployerTeam").GetBoolean().Should().BeFalse("staff are not the employer");
        asStaff.GetProperty("canApply").GetBoolean().Should().BeTrue();

        (await HttpClient.PostAsJsonAsync($"{Root}/{job.JobId}/apply", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Created, "the server lets staff say they are interested");

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);
        var asMember = await ReadJsonAsync(await HttpClient.GetAsync($"{Root}/{job.JobId}"));
        asMember.GetProperty("canManage").GetBoolean().Should().BeFalse();
        asMember.GetProperty("canModerate").GetBoolean().Should().BeFalse();
        asMember.GetProperty("isOnEmployerTeam").GetBoolean().Should().BeFalse();
    }

    /// <summary>The form's own example is "Two cleaners needed", and there was no Cleaning to file it under.</summary>
    [Fact]
    public async Task A_simple_job_can_be_filed_under_cleaning()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var categories = await ReadJsonAsync(await HttpClient.GetAsync($"{Root}/categories"));
        var keys = categories.EnumerateArray().Select(category => category.GetProperty("key").GetString()).ToList();

        keys.Should().Contain("cleaning");
        keys.IndexOf("cleaning").Should().Be(keys.IndexOf("hospitality") + 1, "it sits with the other everyday work");

        var job = await PostSimpleJobAsync(
            "Two cleaners needed", DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)), 1, "Days",
            categoryKey: "cleaning");

        var read = await ReadJsonAsync(await HttpClient.GetAsync($"{Root}/{job.JobId}"));
        read.GetProperty("categoryKey").GetString().Should().Be("cleaning");
    }

    [Theory]
    [InlineData(0, "Days")]
    [InlineData(25, "Hours")]
    [InlineData(400, "Days")]
    [InlineData(1, "Weeks")]
    public async Task A_length_that_is_not_a_real_stretch_of_work_is_refused(int amount, string unit)
    {
        Factory.EnsureDatabaseCreated();

        var (employerId, _) = await RegisterIndividualPosterAsync();
        var response = await HttpClient.PostAsJsonAsync($"{Root}/", SimpleCreateBody(
            employerId, "Help at a wedding", DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)), amount, unit));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_simple_job_cannot_start_in_the_past()
    {
        Factory.EnsureDatabaseCreated();

        var (employerId, _) = await RegisterIndividualPosterAsync();
        var response = await HttpClient.PostAsJsonAsync($"{Root}/", SimpleCreateBody(
            employerId, "Help at a wedding", DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3)), 1, "Days"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Editing_a_simple_job_without_its_schedule_leaves_the_schedule_alone()
    {
        Factory.EnsureDatabaseCreated();

        var startsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(5));
        var job = await PostSimpleJobAsync("Waiters for one evening", startsOn, 4, "Hours");

        // An app build from before simple jobs edits the whole job and sends no schedule.
        (await HttpClient.PutAsJsonAsync($"{Root}/{job.JobId}", new
        {
            title = "Waiters for one evening",
            description = "Serving at a family dinner in Curepipe. Black trousers, white shirt.",
            jobType = "Temporary",
            experienceLevel = "Entry",
            countryCode = "MU",
            salaryMinimum = 500m,
            salaryCurrency = "MUR",
            salaryPeriod = "Project",
            vacancyCount = 3
        })).IsSuccessStatusCode.Should().BeTrue();

        var read = await ReadJsonAsync(await HttpClient.GetAsync($"{Root}/{job.JobId}"));

        read.GetProperty("startsOn").GetString().Should().Be(startsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        read.GetProperty("lengthAmount").GetInt32().Should().Be(4);
        read.GetProperty("lengthUnit").GetString().Should().Be("Hours");
        read.GetProperty("postingMode").GetString().Should().Be("Simple");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A verified member with an employer of their own, as a simple post sets up.</summary>
    private async Task<(Guid EmployerId, string Token)> RegisterIndividualPosterAsync()
    {
        var (_, poster) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(poster.Tokens.AccessToken);

        var employer = await ReadJsonAsync(await HttpClient.PostAsJsonAsync(
            $"{Root}/employers",
            new { displayName = "Ama Mensah", organizationType = "Individual", countryCode = "MU" }));

        return (employer.GetProperty("employerId").GetGuid(), poster.Tokens.AccessToken);
    }

    private static object SimpleCreateBody(
        Guid employerId, string title, DateOnly startsOn, int lengthAmount, string lengthUnit,
        string categoryKey = "hospitality") => new
        {
            employerId,
            title,
            description = $"{title} in Port Louis. Paid on the day.",
            categoryKey,
            jobType = "Temporary",
            experienceLevel = "Entry",
            countryCode = "MU",
            cityName = "Port Louis",
            postingMode = "Simple",
            startsOn = startsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            lengthAmount,
            lengthUnit
        };

    /// <summary>Posts a simple job the way the app does: create, set the pay and places, publish.</summary>
    private async Task<SimpleJob> PostSimpleJobAsync(
        string title, DateOnly startsOn, int lengthAmount, string lengthUnit, int people = 2,
        string categoryKey = "hospitality")
    {
        var (employerId, token) = await RegisterIndividualPosterAsync();

        var created = await HttpClient.PostAsJsonAsync(
            $"{Root}/", SimpleCreateBody(employerId, title, startsOn, lengthAmount, lengthUnit, categoryKey));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var jobId = (await ReadJsonAsync(created)).GetProperty("jobId").GetGuid();

        var terms = await HttpClient.PutAsJsonAsync($"{Root}/{jobId}", new
        {
            title,
            description = $"{title} in Port Louis. Paid on the day.",
            jobType = "Temporary",
            experienceLevel = "Entry",
            countryCode = "MU",
            cityName = "Port Louis",
            salaryMinimum = 200m,
            salaryCurrency = "MUR",
            salaryPeriod = "Daily",
            vacancyCount = people,
            startsOn = startsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            lengthAmount,
            lengthUnit
        });
        terms.StatusCode.Should().Be(HttpStatusCode.OK, await terms.Content.ReadAsStringAsync());

        // No closing date sent: a simple job closes when its work is over.
        var published = await HttpClient.PostAsJsonAsync(
            $"{Root}/{jobId}/publish", new { openImmediately = true });
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new SimpleJob(jobId, token);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Clone();
    }

    private sealed record SimpleJob(Guid JobId, string PosterToken);
}
