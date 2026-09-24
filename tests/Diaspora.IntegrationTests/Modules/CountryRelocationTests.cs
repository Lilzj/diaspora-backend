using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Moving country: what the platform does when a member says they have left.
/// <para>
/// <b>Both halves were built and never joined.</b> Location raised
/// <c>CountryExperienceEndedDomainEvent</c>, whose own summary said leaving "is a Trust concern,
/// and Trust cannot act on a fact it is not told". Trust had
/// <c>CountryStanding.RecordDeparture</c> waiting for exactly that fact. Neither had a caller:
/// the domain event reached no handler, the method reached no code path, and so a member who
/// moved from Mauritius to the UK stayed an open, verified resident of Mauritius for ever, with
/// the residence clock still running on a country they had left.
/// </para>
/// <para>
/// These tests cross three modules by design, because the defect lived between them and nothing
/// inside any one of them could see it. <c>CountryStandingDepartureTests</c> covers what a
/// departure means; this covers whether one ever arrives.
/// </para>
/// </summary>
[Collection(DiscoveryCollection.Name)]
public sealed class CountryRelocationTests : PooledIdentityTestBase
{
    private static readonly DateOnly ArrivedInMauritius = new(2019, 3, 1);
    private static readonly DateOnly LeftMauritius = new(2025, 6, 30);

    public CountryRelocationTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The whole wire, end to end: a member verified in Mauritius declares that they left, and
    /// Trust closes the standing without being asked by anything but the event.
    /// </summary>
    [Fact]
    public async Task Declaring_a_departure_closes_the_trust_standing()
    {
        var member = await VerifiedResidentOfMauritiusAsync();

        var standing = await StandingAsync(member, "MU");
        standing.Should().NotBeNull();
        standing!.DepartedOn.Should().BeNull("the member has not said they left yet");

        (await DeclareResidenceAsync("MU", ArrivedInMauritius, departedOn: LeftMauritius))
            .IsSuccessStatusCode.Should().BeTrue();

        await Factory.DrainOutboxAsync();

        var closed = await StandingAsync(member, "MU");

        closed!.DepartedOn.Should().Be(LeftMauritius);
        closed.Role.Should().Be("Former Resident");
    }

    /// <summary>
    /// Leaving must cost nothing. If it did, the sensible thing for a member to do would be to
    /// never tell us they moved — and a flow that rewards silence is worse than no flow.
    /// </summary>
    [Fact]
    public async Task Leaving_takes_away_neither_the_verification_nor_the_points()
    {
        var member = await VerifiedResidentOfMauritiusAsync();

        var before = await StandingAsync(member, "MU");

        await DeclareResidenceAsync("MU", ArrivedInMauritius, departedOn: LeftMauritius);
        await Factory.DrainOutboxAsync();

        var after = await StandingAsync(member, "MU");

        after!.IsVerified.Should().BeTrue();
        after.Points.Should().Be(before!.Points);
    }

    /// <summary>
    /// The path a member actually walks: they were living there, and now they are not. The
    /// residence exists and is open before the move, and closing it is the move.
    /// </summary>
    [Fact]
    public async Task Closing_an_open_residence_closes_the_standing()
    {
        var member = await VerifiedResidentOfMauritiusAsync();

        await DeclareResidenceAsync("MU", ArrivedInMauritius);
        await Factory.DrainOutboxAsync();

        (await StandingAsync(member, "MU"))!.DepartedOn.Should().BeNull(
            "an open residence is somebody still living there");

        await DeclareResidenceAsync("MU", ArrivedInMauritius, departedOn: LeftMauritius);
        await Factory.DrainOutboxAsync();

        (await StandingAsync(member, "MU"))!.DepartedOn.Should().Be(LeftMauritius);
    }

    /// <summary>
    /// A member who mistypes the month must be able to fix it. The trigger is the departure
    /// date, so a correction travels the same way the original did.
    /// </summary>
    [Fact]
    public async Task Correcting_a_departure_date_moves_the_standing_with_it()
    {
        var member = await VerifiedResidentOfMauritiusAsync();

        await DeclareResidenceAsync("MU", ArrivedInMauritius, departedOn: LeftMauritius);
        await Factory.DrainOutboxAsync();

        var corrected = LeftMauritius.AddMonths(-3);

        await DeclareResidenceAsync("MU", ArrivedInMauritius, departedOn: corrected);
        await Factory.DrainOutboxAsync();

        (await StandingAsync(member, "MU"))!.DepartedOn.Should().Be(corrected);
    }

    /// <summary>
    /// A residence that is still open is not a departure. Amending an arrival date, changing a
    /// role or adding expertise all go through the same upsert, and none of them means the
    /// member has moved.
    /// </summary>
    [Fact]
    public async Task Amending_an_open_residence_does_not_close_the_standing()
    {
        var member = await VerifiedResidentOfMauritiusAsync();

        await DeclareResidenceAsync("MU", ArrivedInMauritius);
        await DeclareResidenceAsync("MU", ArrivedInMauritius.AddDays(-30), role: "Student");

        await Factory.DrainOutboxAsync();

        (await StandingAsync(member, "MU"))!.DepartedOn.Should().BeNull();
    }

    /// <summary>
    /// The second country is a second standing, not a replacement. This is the shape the whole
    /// relocation story rests on: a member who moves keeps what they built and starts building
    /// somewhere else, and the trust centre shows both.
    /// </summary>
    [Fact]
    public async Task Verifying_a_second_country_leaves_the_first_standing_in_place()
    {
        var member = await VerifiedResidentOfMauritiusAsync();

        await DeclareResidenceAsync("MU", ArrivedInMauritius, departedOn: LeftMauritius);
        await Factory.DrainOutboxAsync();

        await VerifyCountryAsync("GB", new DateOnly(2025, 7, 1));
        await Factory.DrainOutboxAsync();

        var mauritius = await StandingAsync(member, "MU");
        var britain = await StandingAsync(member, "GB");

        mauritius!.IsVerified.Should().BeTrue();
        mauritius.DepartedOn.Should().Be(LeftMauritius);

        britain.Should().NotBeNull();
        britain!.IsVerified.Should().BeTrue();
        britain.DepartedOn.Should().BeNull();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A real member who has been through the real country verification: submitted, assigned and
    /// approved by a real reviewer. Nothing here writes a standing directly.
    /// </summary>
    private async Task<Guid> VerifiedResidentOfMauritiusAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        await VerifyCountryAsync("MU", ArrivedInMauritius);
        await Factory.DrainOutboxAsync();

        return auth.User.Id;
    }

    /// <summary>
    /// Submits a country verification as the currently authenticated member and walks it through
    /// review. Leaves that member authenticated, because every caller needs them afterwards.
    /// </summary>
    private async Task VerifyCountryAsync(string countryCode, DateOnly arrivalDate)
    {
        var submitted = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/country",
            new
            {
                countryCode,
                documentType = "UtilityBill",
                documentReference = "media:" + Guid.NewGuid(),
                arrivalDate,
                expertiseAreas = new[] { "Housing" }
            });

        submitted.IsSuccessStatusCode.Should().BeTrue(
            await submitted.Content.ReadAsStringAsync());

        var requestId = await ReadIdAsync(submitted);
        var member = CurrentToken();

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{requestId}/assign", new { }))
            .EnsureSuccessStatusCode();

        var approved = await HttpClient.PostAsJsonAsync(
            $"/api/v1/trust/verifications/{requestId}/review",
            new { decision = "Approve", notes = "Utility bill checks out." });

        approved.IsSuccessStatusCode.Should().BeTrue(
            await approved.Content.ReadAsStringAsync());

        Api.Authenticate(member);
    }

    private Task<HttpResponseMessage> DeclareResidenceAsync(
        string countryCode,
        DateOnly arrivedOn,
        DateOnly? departedOn = null,
        string? role = null) =>
        HttpClient.PostAsJsonAsync(
            "/api/v1/location/me/countries",
            new { countryCode, arrivedOn, departedOn, role });

    /// <summary>
    /// Reads the standing out of the database rather than off a screen, so a defect in a
    /// projection cannot make this pass. The trust centre rendering is somebody else's test.
    /// </summary>
    private async Task<CountryStanding?> StandingAsync(Guid userId, string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        var profile = await scope.ServiceProvider
            .GetRequiredService<ITrustProfileRepository>()
            .GetByUserAsync(UserId.From(userId));

        return profile?.Countries.FirstOrDefault(
            country => string.Equals(
                country.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
    }

    private string CurrentToken() =>
        HttpClient.DefaultRequestHeaders.Authorization?.Parameter
        ?? throw new InvalidOperationException("No member is authenticated.");

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var data = document.RootElement.GetProperty("data");

        foreach (var name in new[] { "verificationId", "requestId", "id" })
        {
            if (data.TryGetProperty(name, out var value))
            {
                return value.GetGuid();
            }
        }

        throw new InvalidOperationException(
            "The submission response carried no identifier: " + data);
    }
}
