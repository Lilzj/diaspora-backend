using System.Net;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// FRONTEND-PHASE13-GAP-001 and -002, over real HTTP.
/// <para>
/// <b>These run over the wire rather than against the handlers because the defects this phase is
/// closing live in the transport.</b> The repository suite proves the query; nothing below the
/// endpoint can prove that <c>?types=NationalDiaspora,CityChapter</c> reaches it. An unbound query
/// parameter is discarded by the model binder with no error, so the route answers 200 with an
/// unfiltered list under a lit filter chip — which is how four filters on this platform were dead
/// for a release with every test green.
/// </para>
/// <para>
/// The wire shapes are re-declared here by hand rather than imported from the module, for the reason
/// the B12 suite records: a contract test bound to the module's own records passes whenever the
/// module compiles.
/// </para>
/// </summary>
public sealed class CommunityDirectoryTests : IdentityTestBase
{
    private const string Root = "/api/v1/communities";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityDirectoryTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // The directory — GAP-001
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>An anonymous caller reads the directory and is told they belong to nothing.</b> The
    /// register is explicit: browsing communities is what somebody does before they have an account,
    /// and the answer is <c>membershipState: "None"</c> rather than a 401.
    /// </summary>
    [Fact]
    public async Task An_anonymous_caller_reads_public_communities()
    {
        var slug = await SeedOpenCommunityAsync("Anonymous readable");

        using var anonymous = Factory.CreateClient();

        var page = await ReadDataAsync<DirectoryPageDto>(
            await anonymous.GetAsync($"{Root}?query=Anonymous%20readable"));

        var row = page.Items.Should().ContainSingle(item => item.Community.Slug == slug).Subject;

        row.MembershipState.Should().Be("None");
        row.IsPrimary.Should().BeFalse();
    }

    /// <summary>
    /// <b>A private community is absent from the directory, and absent means absent.</b> Forgetting
    /// this filter does not throw — it leaks the existence of every private community as an ordinary
    /// 200, which is why it is asserted at the transport as well as at the repository.
    /// </summary>
    [Fact]
    public async Task The_directory_never_lists_a_private_community()
    {
        var visible = await SeedOpenCommunityAsync("Privacy probe public");
        var hidden = await SeedOpenCommunityAsync(
            "Privacy probe hidden", visibility: CommunityVisibility.Private);

        var page = await ReadDataAsync<DirectoryPageDto>(
            await HttpClient.GetAsync($"{Root}?query=Privacy%20probe"));

        page.Items.Should().Contain(item => item.Community.Slug == visible);
        page.Items.Should().NotContain(item => item.Community.Slug == hidden);
    }

    /// <summary>
    /// <b>The type filter arrives as one comma-joined parameter, because that is what the client
    /// sends.</b> Declared as a <c>string[]</c> this would bind repeated keys, take the whole
    /// comma-joined value as a single element, match no type and serve an unfiltered browse. The
    /// assertion that catches that is the *exclusion* — a test that only checked the wanted row was
    /// present would pass against a filter that does nothing at all.
    /// </summary>
    [Fact]
    public async Task The_type_filter_binds_a_comma_joined_list()
    {
        var national = await SeedOpenCommunityAsync("Binding probe national");
        var interest = await SeedOpenCommunityAsync(
            "Binding probe interest", type: CommunityType.InterestCommunity, origin: null);

        var page = await ReadDataAsync<DirectoryPageDto>(
            await HttpClient.GetAsync(
                $"{Root}?query=Binding%20probe&types=InterestCommunity,CityChapter"));

        page.Items.Should().Contain(item => item.Community.Slug == interest);

        page.Items.Should().NotContain(
            item => item.Community.Slug == national,
            "a filter that binds nothing serves an unfiltered browse, and only the exclusion "
            + "distinguishes the two");
    }

    /// <summary>
    /// <b>Origin and presence are separate filters because they are separate questions.</b> Where a
    /// community is from, and where it has a presence. Collapsing them is the "one country, one
    /// community" assumption the whole phase exists to remove.
    /// </summary>
    [Fact]
    public async Task Origin_and_presence_filter_independently()
    {
        var nigerian = await SeedOpenCommunityAsync("Origin probe NG", origin: "NG");
        var ghanaian = await SeedOpenCommunityAsync(
            "Origin probe GH", origin: "GH", presence: ["GB"]);

        var byOrigin = await ReadDataAsync<DirectoryPageDto>(
            await HttpClient.GetAsync($"{Root}?query=Origin%20probe&originCountryCodes=GH"));

        byOrigin.Items.Should().Contain(item => item.Community.Slug == ghanaian);
        byOrigin.Items.Should().NotContain(item => item.Community.Slug == nigerian);

        var byPresence = await ReadDataAsync<DirectoryPageDto>(
            await HttpClient.GetAsync($"{Root}?query=Origin%20probe&presenceCountryCode=GB"));

        byPresence.Items.Should().Contain(item => item.Community.Slug == ghanaian);
        byPresence.Items.Should().NotContain(item => item.Community.Slug == nigerian);
    }

    /// <summary>
    /// <b>The caller's own standing rides on the row.</b> A client deriving "you are a member" from
    /// a cached list offers Leave to somebody whose membership was revoked an hour ago.
    /// </summary>
    [Fact]
    public async Task A_row_carries_the_callers_own_membership_state()
    {
        var auth = await RegisterVerifiedMemberAsync();
        var member = UserId.From(auth.Auth.User.Id);

        var slug = await SeedOpenCommunityAsync("Standing probe", member: member);

        Api.Authenticate(auth.Auth.Tokens.AccessToken);
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", auth.Auth.Tokens.AccessToken);

        var page = await ReadDataAsync<DirectoryPageDto>(
            await client.GetAsync($"{Root}?query=Standing%20probe"));

        var row = page.Items.Should().ContainSingle(item => item.Community.Slug == slug).Subject;

        row.MembershipState.Should().Be("Member");
    }

    /// <summary>
    /// <b>Ordering by member count is refused rather than quietly served in another order.</b> §31
    /// forbids implying a popularity the backend does not authoritatively report, and the count is
    /// maintained as members come and go rather than measured.
    /// </summary>
    [Fact]
    public async Task Ordering_by_member_count_is_refused_with_its_reason()
    {
        var response = await HttpClient.GetAsync($"{Root}?sort=members");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(
            "sort.members-unavailable",
            "the client renders the refusal, so it needs the code rather than a bare 400");
    }

    /// <summary>The client's default ordering is served rather than refused.</summary>
    [Fact]
    public async Task The_clients_default_ordering_is_served()
    {
        await SeedOpenCommunityAsync("Default sort probe");

        var response = await HttpClient.GetAsync($"{Root}?sort=relevance");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, "the client sends sort=relevance on every directory read");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // The profile — GAP-002
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>One route, resolving a handle or an identifier.</b> A deep link carries the handle and a
    /// client that already loaded the community carries the id; two routes would push the choice
    /// onto the client, which would have to guess what a pasted string is.
    /// </summary>
    [Fact]
    public async Task A_community_resolves_by_handle_and_by_identifier()
    {
        var slug = await SeedOpenCommunityAsync("Resolution probe");

        var bySlug = await ReadDataAsync<CommunityDto>(
            await HttpClient.GetAsync($"{Root}/{slug}"));

        var byId = await ReadDataAsync<CommunityDto>(
            await HttpClient.GetAsync($"{Root}/{bySlug.Id}"));

        byId.Slug.Should().Be(slug);
        byId.Id.Should().Be(bySlug.Id);
    }

    /// <summary>
    /// <b>A suspended community reads, and carries the reason verbatim.</b> §75 keeps suspended
    /// distinct from deleted precisely so a link sent last year resolves to an explanation rather
    /// than a dead end, and §74 requires the server's own words rather than a generic error.
    /// </summary>
    [Fact]
    public async Task A_suspended_community_reads_with_its_reason()
    {
        const string Reason = "Paused while we review a report from a member.";

        var slug = await SeedOpenCommunityAsync("Suspension probe", suspendWith: Reason);

        var community = await ReadDataAsync<CommunityDto>(
            await HttpClient.GetAsync($"{Root}/{slug}"));

        community.Status.Should().Be("Suspended");
        community.StatusReason.Should().Be(Reason);
    }

    /// <summary>
    /// <b>A private community answers 404 to a stranger, not 403.</b> A 403 would confirm it exists,
    /// which turns the route into an oracle for probing which communities the platform holds. This is
    /// the documented rule the register asked for, and it is why this route never answers 403 at all.
    /// </summary>
    [Fact]
    public async Task A_private_community_is_absent_rather_than_forbidden()
    {
        var slug = await SeedOpenCommunityAsync(
            "Absence probe", visibility: CommunityVisibility.Private);

        var response = await HttpClient.GetAsync($"{Root}/{slug}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        response.StatusCode.Should().NotBe(
            HttpStatusCode.Forbidden,
            "a refusal that confirms existence is the leak the 404 rule exists to prevent");
    }

    /// <summary>
    /// <b>A member of a private community reads it.</b> The other half of the rule: they already
    /// know it exists, so answering 404 would be a lie they can detect.
    /// </summary>
    [Fact]
    public async Task A_member_reads_the_private_community_they_belong_to()
    {
        var auth = await RegisterVerifiedMemberAsync();
        var member = UserId.From(auth.Auth.User.Id);

        var slug = await SeedOpenCommunityAsync(
            "Private member probe",
            visibility: CommunityVisibility.Private,
            member: member);

        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", auth.Auth.Tokens.AccessToken);

        var community = await ReadDataAsync<CommunityDto>(
            await client.GetAsync($"{Root}/{slug}"));

        community.Slug.Should().Be(slug);
        community.MemberCount.Should().NotBeNull("a member may know how big their community is");
        community.MemberCount!.Source.Should().Be("backend");
    }

    /// <summary>
    /// <b>A handle nobody holds is a 404, and a malformed one is the same 404.</b> A lookup for
    /// something that cannot exist is not a validation error — answering 400 would tell a caller
    /// their guess was well-formed, which is one bit more than they should get.
    /// </summary>
    [Theory]
    [InlineData("no-such-community-anywhere")]
    [InlineData("Not A Valid Handle")]
    public async Task An_unknown_or_malformed_handle_is_the_same_absence(string candidate)
    {
        var response = await HttpClient.GetAsync($"{Root}/{Uri.EscapeDataString(candidate)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// <b>A reserved handle still reaches the route it protects.</b> The literal wins over the
    /// parameter, which is exactly why <c>CommunitySlug</c> refuses these words — a community that
    /// took one would be permanently unreachable.
    /// </summary>
    [Fact]
    public async Task A_reserved_word_still_routes_to_the_endpoint_that_owns_it()
    {
        var response = await HttpClient.GetAsync($"{Root}/campaigns");

        response.StatusCode.Should().NotBe(
            HttpStatusCode.NotFound,
            "/communities/campaigns is the campaign list, and a community may not take that handle");
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a community straight through the repository.
    /// <para>
    /// There is no create route yet — it is the next increment — and these two reads are worth
    /// proving before it exists. Every community is uniquely named so the suite's shared database
    /// cannot make one test's rows another's.
    /// </para>
    /// </summary>
    private async Task<string> SeedOpenCommunityAsync(
        string name,
        CommunityType type = CommunityType.NationalDiaspora,
        CommunityVisibility visibility = CommunityVisibility.Public,
        string? origin = "NG",
        IReadOnlyList<string>? presence = null,
        UserId? member = null,
        string? suspendWith = null)
    {
        // No slicing: FromName already truncates at the maximum length, and a hand-rolled
        // substring here threw on every name shorter than the slice.
        var slug = CommunitySlug.FromName($"{name}-{Guid.NewGuid():N}").Value;

        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var memberships = scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var community = CommunityAggregate
            .Propose(CommunityId.New(), slug, name, type, visibility, origin)
            .Value;

        community.Open();

        if (presence is { Count: > 0 })
        {
            community.SetPresence(presence);
        }

        if (member is not null)
        {
            var membership = CommunityMembership.Start(
                CommunityMembershipId.New(),
                community.Id,
                member,
                CommunityMembershipState.Following).Value;

            membership.Join(community, requiresApproval: false, DateTimeOffset.UtcNow);
            community.RecordMemberJoined();
            memberships.Add(membership);
        }

        // Suspended last, so the join above is not refused by a community that is already stopped.
        if (suspendWith is not null)
        {
            community.Suspend(suspendWith);
        }

        communities.Add(community);
        await unitOfWork.SaveChangesAsync();

        return slug.Value;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded, and the body says why it did not: {0}", body);

        var envelope = JsonSerializer.Deserialize<EnvelopeDto<T>>(body, JsonOptions);

        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();

        return envelope.Data!;
    }

    // ── Wire shapes, re-declared. See the class summary. ─────────────────────────────────────

    private sealed record EnvelopeDto<T>(T? Data);

    private sealed record DirectoryPageDto(
        List<DirectoryEntryDto> Items, string? NextCursor, bool HasMore, int? TotalCount);

    private sealed record DirectoryEntryDto(
        CommunityDto Community, string MembershipState, bool IsPrimary, string? Reason);

    private sealed record CommunityDto(
        Guid Id,
        string Slug,
        string Name,
        string Type,
        string Status,
        string Visibility,
        string? StatusReason,
        SourcedDto<int>? MemberCount,
        OriginDto Origin,
        IdentityDto Identity);

    private sealed record SourcedDto<T>(T Value, string Source);

    private sealed record OriginDto(string? CountryCode, string? RegionName);

    private sealed record IdentityDto(Guid? LogoMediaId, string? IconName, string? AccentColor);
}
