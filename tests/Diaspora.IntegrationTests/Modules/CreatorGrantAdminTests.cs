using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Users;
using Diaspora.IntegrationTests.Common;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// <b>The platform's administrators post the grants</b> (UAT, 3 Nov 2026). The grants members saw
/// were two the Creator seeder wrote at every startup in every environment, in pounds, with
/// requirements reading "Be at least a VerifiedCreator". An administrator now posts, edits,
/// publishes, unpublishes and closes grants, in the currency and countries they choose; members
/// read the requirements in words.
/// </summary>
public sealed class CreatorGrantAdminTests : IdentityTestBase
{
    private const string Grants = "/api/v1/grants";
    private const string Admin = "/api/v1/grants/admin";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CreatorGrantAdminTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task An_administrator_posts_a_grant_members_can_apply_to_and_closes_it()
    {
        await AuthenticateAsStaffAsync("Admin");

        var created = await HttpClient.PostAsJsonAsync(Admin, new
        {
            title = "Lagos newcomers fund",
            summary = "Up to ₦500,000 for a project that helps people settle in Lagos.",
            description = "For members running something for other members in Lagos.",
            category = "community",
            fundingMinorUnits = 50_000_000L,
            fundingMinimumMinorUnits = 10_000_000L,
            currency = "ngn",
            countryCodes = new[] { "NG" },
            deadlineAt = DateTimeOffset.UtcNow.AddDays(30),
            minimumStatus = "VerifiedCreator",
            requiresVerifiedCreator = false,
            publish = true
        }, JsonOptions);

        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var grant = await IdentityApiClient.ReadDataAsync<AdminGrantDto>(created);

        grant.IsPublished.Should().BeTrue();
        grant.State.Should().Be("Open");
        grant.FundingAmount.Currency.Should().Be("NGN", "the administrator chose naira");
        grant.CountryCodes.Should().Equal("NG");

        var adminList = await IdentityApiClient.ReadDataAsync<List<AdminGrantDto>>(
            await HttpClient.GetAsync(Admin));
        adminList.Should().Contain(item => item.GrantId == grant.GrantId);

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var open = await IdentityApiClient.ReadDataAsync<List<GrantDto>>(
            await HttpClient.GetAsync($"{Grants}?state=Open,ClosingSoon"));
        var seen = open.Single(item => item.GrantId == grant.GrantId);

        seen.Requirements.Select(requirement => requirement.Label)
            .Should().Contain("Be at least a verified creator")
            .And.NotContain(label => label.Contains("VerifiedCreator", StringComparison.Ordinal),
                "a member reads the requirement in words, not an enum name");

        // A complete grant, so the answer is about who is asking rather than what was sent.
        (await HttpClient.PostAsJsonAsync(Admin, new
            {
                title = "Mine",
                summary = "A grant of my own.",
                description = "Nobody should be able to post this.",
                category = "community",
                fundingMinorUnits = 100_000L,
                currency = "GBP",
                publish = true
            }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "only an administrator posts a grant");
        (await HttpClient.PostAsync($"{Admin}/{grant.GrantId}/close", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthenticateAsStaffAsync("Admin");

        var closed = await IdentityApiClient.ReadDataAsync<AdminGrantDto>(
            await HttpClient.PostAsync($"{Admin}/{grant.GrantId}/close", content: null));
        closed.State.Should().Be("Closed", "closing ends the window now");

        Api.Authenticate(member.Tokens.AccessToken);

        (await IdentityApiClient.ReadDataAsync<List<GrantDto>>(
                await HttpClient.GetAsync($"{Grants}?state=Open,ClosingSoon")))
            .Should().NotContain(item => item.GrantId == grant.GrantId,
                "the Open tab shows only grants that are taking applications");

        await AuthenticateAsStaffAsync("Admin");

        var unpublished = await IdentityApiClient.ReadDataAsync<AdminGrantDto>(
            await HttpClient.PostAsync($"{Admin}/{grant.GrantId}/unpublish", content: null));
        unpublished.IsPublished.Should().BeFalse();

        Api.Authenticate(member.Tokens.AccessToken);

        (await IdentityApiClient.ReadDataAsync<List<GrantDto>>(await HttpClient.GetAsync(Grants)))
            .Should().NotContain(item => item.GrantId == grant.GrantId, "an unpublished grant is nobody's to see");
    }

    [Fact]
    public async Task An_administrator_edits_a_grant_without_touching_its_form()
    {
        await AuthenticateAsStaffAsync("Admin");

        var grant = await IdentityApiClient.ReadDataAsync<AdminGrantDto>(
            await HttpClient.PostAsJsonAsync(Admin, new
            {
                title = "Accra creators kit",
                summary = "Kit for creators in Accra.",
                description = "A microphone or a year of a tool.",
                category = "creators",
                fundingMinorUnits = 300_000L,
                currency = "GHS",
                countryCodes = new[] { "GH" },
                minimumStatus = "Contributor",
                publish = false
            }, JsonOptions));

        grant.IsPublished.Should().BeFalse("a grant can be prepared before it is posted");
        grant.Fields.Should().NotBeEmpty("an applicant always has something to fill in");

        var edited = await IdentityApiClient.ReadDataAsync<AdminGrantDto>(
            await HttpClient.PutAsJsonAsync($"{Admin}/{grant.GrantId}", new
            {
                title = "Accra creators kit grant",
                summary = "Kit for creators in Accra and Kumasi.",
                description = "A microphone or a year of a tool.",
                category = "creators",
                fundingMinorUnits = 500_000L,
                currency = "GHS",
                countryCodes = new[] { "GH" },
                minimumStatus = "Contributor"
            }, JsonOptions));

        edited.Title.Should().Be("Accra creators kit grant");
        edited.FundingAmount.MinorUnits.Should().Be(500_000);
        edited.Fields.Select(field => field.Key).Should().Equal(grant.Fields.Select(field => field.Key));

        var published = await IdentityApiClient.ReadDataAsync<AdminGrantDto>(
            await HttpClient.PostAsync($"{Admin}/{grant.GrantId}/publish", content: null));
        published.IsPublished.Should().BeTrue();
    }

    private async Task AuthenticateAsStaffAsync(string roleName)
    {
        var (phone, auth) = await RegisterVerifiedMemberAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
            var userId = new UserId(auth.User.Id);
            var user = await dbContext.Set<User>().FirstOrDefaultAsync(candidate => candidate.Id == userId);

            user.Should().NotBeNull();
            user!.AssignRole(UserRole.All.Single(role => role.Name == roleName));

            await dbContext.SaveChangesAsync();
        }

        var login = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.LoginAsync(new LoginRequest { Email = EmailFor(phone), Password = ValidPassword }));

        Api.Authenticate(login.Tokens.AccessToken);
    }

    private sealed record MoneyDto(long MinorUnits, string Currency);

    private sealed record RequirementDto(string Key, string Label, bool IsMet);

    private sealed record FieldDto(string Key, string Label);

    private sealed record GrantDto(
        Guid GrantId, string Title, string State, MoneyDto FundingAmount, List<RequirementDto> Requirements);

    private sealed record AdminGrantDto(
        Guid GrantId,
        string Title,
        string State,
        bool IsPublished,
        MoneyDto FundingAmount,
        List<string> CountryCodes,
        List<FieldDto> Fields);
}
