using System.Net;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Modules;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// What a caller this deployment knows nothing about can learn of it, and that an operator can still
/// learn all of it (BSEC-009, BBL-007).
/// <para>
/// Four routes told a stranger more than whether the platform is up. <c>GET /</c> listed every
/// composed module with its version. <c>/health/ready</c> sent every check's data, which carried the
/// same inventory beside the outbox backlog per module and how transactions execute. And the two
/// realtime routes answered anonymously although the published contract said a token was required.
/// Each fact below pins one of those shut, and the operator facts pin that nothing was lost.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class PlatformDisclosureTests : PooledIdentityTestBase
{
    private const string ModulesRoute = "/api/v1/platform/operations/modules";

    public PlatformDisclosureTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task The_root_says_which_platform_this_is_and_where_to_ask_whether_it_is_up()
    {
        using var stranger = Factory.CreateClient();

        var response = await stranger.GetAsync(new Uri("/", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        string[] expected = ["platform", "health"];

        data.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                expected,
                "the root used to list every composed module and a version string, and a caller it "
                + "knows nothing about is owed neither");
        data.GetProperty("platform").GetString().Should().Be("Diaspora");
        data.GetProperty("health").GetString().Should().Be("/health/live");
    }

    [Theory]
    [InlineData(ModulesRoute)]
    [InlineData("/api/v1/notifications/realtime/configuration")]
    [InlineData("/api/v1/notifications/realtime/presence")]
    public async Task A_stranger_is_refused(string route)
    {
        using var stranger = Factory.CreateClient();

        var response = await stranger.GetAsync(new Uri(route, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_member_is_refused_the_module_inventory()
    {
        var (_, member) = await RegisterAndVerifyAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(new Uri(ModulesRoute, UriKind.Relative));

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "the inventory is an operator's question, not a member's");
    }

    [Fact]
    public async Task An_operator_reads_every_composed_module()
    {
        await SignInAsRoleAsync("Admin");

        var response = await HttpClient.GetAsync(new Uri(ModulesRoute, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        json.RootElement.GetProperty("data").EnumerateArray()
            .Select(module => module.GetProperty("name").GetString())
            .Should().BeEquivalentTo(ComposedModules(), "the list moved here from GET / whole");
    }

    [Fact]
    public async Task A_stranger_learns_whether_each_dependency_is_ready_and_nothing_more()
    {
        using var stranger = Factory.CreateClient();

        var response = await stranger.GetAsync(new Uri("/health/ready", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(body);
        var checks = json.RootElement.GetProperty("checks").EnumerateArray().ToList();

        checks.Should().NotBeEmpty();
        checks.Select(check => check.GetProperty("data").ValueKind)
            .Should().OnlyContain(
                kind => kind == JsonValueKind.Null,
                "a check's numbers are an operator's: they carried every module with its version, "
                + "the outbox backlog per module and how transactions execute here");
        body.Should().NotContain("CommunityLife", "the module inventory travelled in the data");
    }

    [Fact]
    public async Task An_operator_still_reads_every_readiness_number()
    {
        await SignInAsRoleAsync("Admin");

        var response = await HttpClient.GetAsync(new Uri("/health/ready", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var modules = json.RootElement.GetProperty("checks").EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "modules");

        modules.GetProperty("data").GetProperty("modules").EnumerateArray()
            .Select(entry => entry.GetString() ?? string.Empty)
            .Should().HaveCount(
                ComposedModules().Count, "an operator loses nothing by asking with a token")
            .And.Contain(entry => entry.StartsWith("CommunityLife ", StringComparison.Ordinal));
    }

    private List<string> ComposedModules() =>
        [.. Factory.Services.GetRequiredService<PlatformManifest>().GetRegisteredModules()
            .Select(module => module.Name)];
}
