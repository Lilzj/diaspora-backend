using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Finishing a course earns XP and no points, and counts toward <c>academy-graduate</c> (RWD-11,
/// RWD-12, Q6).
/// <para>
/// A course paid 40 reward points and no XP, the opposite of the brief, and the
/// <c>academy-graduate</c> milestone could never be reached because nothing recorded a completion.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class CourseCompletionRewardTests : PooledIdentityTestBase
{
    private const string Academy = "/api/v1/academy";

    public CourseCompletionRewardTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_finished_course_pays_10_xp_once_and_no_points()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        await Factory.DrainOutboxAsync();

        var safety = (await DataAsync(await HttpClient.GetAsync($"{Academy}/courses")))
            .EnumerateArray().Single(course => course.GetProperty("slug").GetString() == "staying-safe");
        var courseId = safety.GetProperty("courseId").GetGuid();

        var lesson = (await DataAsync(await HttpClient.GetAsync($"{Academy}/courses/{courseId}/lessons")))
            .EnumerateArray().First().GetProperty("lessonId").GetGuid();

        var progress = await DataAsync(await HttpClient.PostAsJsonAsync($"{Academy}/lessons/{lesson}/complete", new { }));
        progress.GetProperty("state").GetString().Should().Be("Completed");

        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        var award = (await EventsAsync(auth.User.Id)).Should()
            .ContainSingle(item => item.ActionType == ReputationActions.CourseCompleted).Subject;
        award.Points.Should().Be(10);
        award.AwardKey.Should().Be(ReputationAwardKey.Course(courseId).Value);

        using var scope = Factory.Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(auth.User.Id);

        (account?.Entries ?? []).Should().NotContain(
            entry => entry.Source.StartsWith("Course:", StringComparison.Ordinal),
            "a course pays no points for new completions");

        var graduate = (await DataAsync(await HttpClient.GetAsync("/api/v1/creators/me/milestones")))
            .EnumerateArray().Single(milestone => milestone.GetProperty("key").GetString() == "academy-graduate");
        graduate.GetProperty("progress").GetProperty("current").GetInt32()
            .Should().BeGreaterThan(0, "the finished course is the member's activity");
    }

    private async Task<IReadOnlyList<ReputationEvent>> EventsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(UserId.From(userId), new PaginationRequest { PageSize = 50 });

        return page.Items;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, but answered {(int)response.StatusCode}: {body}");

        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }
}
