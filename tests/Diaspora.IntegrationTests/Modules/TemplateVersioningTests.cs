using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Editing a published template must leave the published version intact.
/// <para>
/// <b>The defect of 7 Oct 2026.</b> Editing a published template opens a draft of the next
/// version. The draft was handed the published version's own localization, variant, variable and
/// action objects, and each row carries its template's id, so saving the draft moved them all: the
/// published version — still the one every notification rendered from — was left with no wording at
/// all. Found in production while applying the verification email wording; the two live templates
/// were empty for about two minutes until the drafts were published.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class TemplateVersioningTests : PooledIdentityTestBase
{
    private const string Key = "trust.verification.approved";

    public TemplateVersioningTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Opening_a_draft_leaves_the_published_version_its_wording()
    {
        await SignInAsRoleAsync("Admin");

        var before = await PublishedAsync();
        var localizations = before.GetProperty("localizations").GetArrayLength();
        var variables = before.GetProperty("variables").GetArrayLength();

        localizations.Should().BeGreaterThan(0, "the premise: the seeded template has wording");

        var edit = await HttpClient.PutAsJsonAsync(
            $"/api/v1/notifications/templates/{before.GetProperty("id").GetGuid()}",
            new { description = "Edited by a test." });

        edit.IsSuccessStatusCode.Should().BeTrue(await edit.Content.ReadAsStringAsync());

        var after = await PublishedAsync();

        after.GetProperty("localizations").GetArrayLength().Should().Be(localizations);
        after.GetProperty("variables").GetArrayLength().Should().Be(variables);

        var draft = (await VersionsAsync()).Single(version => version.GetProperty("status").GetString() == "Draft");
        draft.GetProperty("localizations").GetArrayLength().Should().Be(localizations);
    }

    private async Task<JsonElement> PublishedAsync() =>
        (await VersionsAsync()).Single(version => version.GetProperty("status").GetString() == "Published");

    private async Task<List<JsonElement>> VersionsAsync()
    {
        var body = await HttpClient.GetStringAsync($"/api/v1/notifications/templates/{Key}");
        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").EnumerateArray()
            .Select(element => element.Clone())
            .ToList();
    }
}
