using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A landlord's viewing requests — 15 Sep 2026.
/// <para>
/// <b>Found on two phones: a member could ask to see a home, and the landlord had nowhere to
/// answer.</b> The diary route existed and listed each request by the seeker's identifier alone,
/// which told a landlord agreeing a time nothing about who was coming. Requests now carry the
/// member's account name and whether that account is verified, on the list and on the answer to
/// every move.
/// </para>
/// </summary>
[Collection(HousingCollection.Name)]
public sealed class HousingViewingDiaryTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/housing";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HousingViewingDiaryTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_landlord_sees_who_asked_and_can_agree_a_time()
    {
        Factory.EnsureDatabaseCreated();

        var (_, landlordMember) = await RegisterVerifiedMemberAsync();
        var (_, seeker) = await RegisterVerifiedMemberAsync();

        var viewingId = await SeedRequestAsync(landlordMember.User.Id, seeker.User.Id);

        Api.Authenticate(landlordMember.Tokens.AccessToken);

        var diary = await IdentityApiClient.ReadDataAsync<List<ViewingShape>>(
            await HttpClient.GetAsync($"{Root}/viewings?side=landlord&openOnly=false"));

        var request = diary.Should().ContainSingle(item => item.Id == viewingId).Subject;
        request.Status.Should().Be("Requested");
        request.SeekerName.Should().Be(
            seeker.User.FullName, "a landlord agreeing a time needs to know who is coming");
        request.SeekerIsVerified.Should().NotBeNull();

        var scheduled = await HttpClient.PostAsJsonAsync(
            $"{Root}/viewings/{viewingId}",
            new
            {
                action = "Schedule",
                scheduledFor = DateTimeOffset.UtcNow.AddDays(3),
                place = "Outside the bus station"
            },
            JsonOptions);

        var answer = await IdentityApiClient.ReadDataAsync<ViewingShape>(scheduled);
        answer.Status.Should().Be("Scheduled");
        answer.SeekerName.Should().Be(seeker.User.FullName);
    }

    [Fact]
    public async Task The_member_who_asked_cannot_agree_the_time_themselves_but_can_call_it_off()
    {
        Factory.EnsureDatabaseCreated();

        var (_, landlordMember) = await RegisterVerifiedMemberAsync();
        var (_, seeker) = await RegisterVerifiedMemberAsync();

        var viewingId = await SeedRequestAsync(landlordMember.User.Id, seeker.User.Id);

        Api.Authenticate(seeker.Tokens.AccessToken);

        var own = await IdentityApiClient.ReadDataAsync<List<ViewingShape>>(
            await HttpClient.GetAsync($"{Root}/viewings?side=member&openOnly=false"));

        own.Should().Contain(item => item.Id == viewingId);

        var scheduled = await HttpClient.PostAsJsonAsync(
            $"{Root}/viewings/{viewingId}",
            new { action = "Schedule", scheduledFor = DateTimeOffset.UtcNow.AddDays(3) },
            JsonOptions);

        scheduled.IsSuccessStatusCode.Should().BeFalse("only the landlord agrees a time");

        var cancelled = await IdentityApiClient.ReadDataAsync<ViewingShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/viewings/{viewingId}",
                new { action = "Cancel", reason = "Found somewhere else" },
                JsonOptions));

        cancelled.Status.Should().Be("Cancelled");
    }

    private async Task<Guid> SeedRequestAsync(Guid landlordUserId, Guid seekerUserId)
    {
        var landlordId = LandlordProfileId.New();
        var viewingId = HousingViewingId.New();
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        dbContext.Set<LandlordProfile>().Add(
            LandlordProfile.Register(
                landlordId,
                UserId.From(landlordUserId),
                LandlordIdentity.Create("Diary Lettings", LandlordKind.Individual, null).Value,
                HousingSlug.Create($"diary-lettings-{Guid.NewGuid():N}"),
                "MU",
                LandlordContact.None,
                now).Value);

        dbContext.Set<HousingViewing>().Add(
            HousingViewing.Request(
                viewingId,
                HousingListingId.New(),
                landlordId,
                UserId.From(seekerUserId),
                ViewingWindow.Create(now.AddDays(2)).Value,
                "Could I come round on Saturday?",
                now,
                Guid.NewGuid()).Value);

        await dbContext.SaveChangesAsync();

        return viewingId.Value;
    }

    private sealed record ViewingShape
    {
        public Guid Id { get; init; }

        public string Status { get; init; } = string.Empty;

        public string? SeekerName { get; init; }

        public bool? SeekerIsVerified { get; init; }
    }
}
