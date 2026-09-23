using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Landlord ratings, and the tenancy that has to exist behind one.
/// <para>
/// The housing screen rendered <c>★ {listing.host.rating}</c> against a field no response has
/// ever carried, directly above <c>Pay £1850 deposit</c>. These assert the thing that replaced
/// it: a star that a reservation reaching <c>Occupied</c> had to produce.
/// </para>
/// <para>
/// <b><c>LeaseSigned</c> is deliberately not enough.</b> A signed lease is a promise; a tenancy
/// that fell through before move-in is not an experience of living somewhere. That distinction
/// is the one this suite exists to hold.
/// </para>
/// </summary>
[Collection(HousingCollection.Name)]
public sealed class HousingLandlordReviewTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/housing";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HousingLandlordReviewTests(CustomWebApplicationFactory factory) : base(factory) { }

    private sealed record Envelope<T>(T Data);

    private sealed record RatingShape(double Average, int TotalReviews);

    private sealed record ReviewShape(Guid ReviewId, Guid ReservationId, int Rating, bool CanEdit);

    private sealed record ReviewPageShape(RatingShape Rating, List<ReviewShape> Items);

    /// <summary>The gate. Without it the feature is the invented star with extra steps.</summary>
    [Fact]
    public async Task A_member_who_has_rented_nothing_cannot_rate_a_landlord()
    {
        var (_, tenant) = await RegisterVerifiedMemberAsync();
        var landlord = await SeedLandlordAsync();

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(tenant.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords/{landlord.LandlordId.Value}/reviews",
            new { reservationId = Guid.NewGuid(), rating = 5, body = "Never rented here." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("housing.review.no-tenancy");
    }

    /// <summary>
    /// A reservation that never reached move-in is not a tenancy. <c>LeaseSigned</c> is included
    /// on purpose: it is the state most likely to be mistaken for one.
    /// </summary>
    [Theory]
    [InlineData(HousingReservationStatus.Requested)]
    [InlineData(HousingReservationStatus.Reserved)]
    [InlineData(HousingReservationStatus.LeaseSigned)]
    [InlineData(HousingReservationStatus.Cancelled)]
    public async Task A_tenancy_that_never_began_does_not_earn_the_right_to_rate(
        HousingReservationStatus status)
    {
        var (_, tenant) = await RegisterVerifiedMemberAsync();
        var landlord = await SeedLandlordAsync();
        var reservation = await SeedReservationAsync(
            landlord, UserId.From(tenant.User.Id), status);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(tenant.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords/{landlord.LandlordId.Value}/reviews",
            new { reservationId = reservation.Value, rating = 5, body = "Never moved in." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_tenancy_moved_into_earns_a_rating_that_the_rollup_then_carries()
    {
        var (_, tenant) = await RegisterVerifiedMemberAsync();
        var landlord = await SeedLandlordAsync();
        var reservation = await SeedReservationAsync(
            landlord, UserId.From(tenant.User.Id), HousingReservationStatus.Occupied);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(tenant.Tokens.AccessToken);

        var written = await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords/{landlord.LandlordId.Value}/reviews",
            new
            {
                reservationId = reservation.Value,
                rating = 4,
                title = "Fair landlord",
                body = "Deposit returned in full, repairs done within a week."
            });

        written.StatusCode.Should().Be(HttpStatusCode.Created);

        var page = await ReadPageAsync(landlord.LandlordId);

        page.Rating.TotalReviews.Should().Be(1);
        page.Rating.Average.Should().Be(4d);
        page.Items.Should().ContainSingle().Which.Rating.Should().Be(4);

        // Read back out of the database, not out of the request that wrote it. B10's first
        // version of this rollup persisted its buckets correctly and then served a total of
        // zero beside them, because EF materialises an owned type without running the
        // constructor that had done the arithmetic.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var stored = db.Set<LandlordProfile>()
            .AsNoTracking()
            .First(candidate => candidate.Id == landlord.LandlordId);

        stored.ReviewSummary.FourStar.Should().Be(1);
        stored.ReviewSummary.TotalReviews.Should().Be(1);
        stored.ReviewSummary.Average.Should().Be(4d);
    }

    [Fact]
    public async Task Rating_the_same_tenancy_twice_revises_it_rather_than_counting_twice()
    {
        var (_, tenant) = await RegisterVerifiedMemberAsync();
        var landlord = await SeedLandlordAsync();
        var reservation = await SeedReservationAsync(
            landlord, UserId.From(tenant.User.Id), HousingReservationStatus.Occupied);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(tenant.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords/{landlord.LandlordId.Value}/reviews",
            new { reservationId = reservation.Value, rating = 5, body = "Good start." });

        await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords/{landlord.LandlordId.Value}/reviews",
            new { reservationId = reservation.Value, rating = 2, body = "Deposit withheld." });

        var page = await ReadPageAsync(landlord.LandlordId);

        page.Rating.TotalReviews.Should().Be(1, "one tenancy is one review");
        page.Rating.Average.Should().Be(2d, "the revision is what counts");
    }

    /// <summary>
    /// A tenant cannot rate somebody else's tenancy. The reservation id is a client-supplied
    /// guid, and the only thing between that and rating a stranger's let is this handler.
    /// </summary>
    [Fact]
    public async Task A_tenancy_belonging_to_somebody_else_cannot_be_rated()
    {
        var (_, tenant) = await RegisterVerifiedMemberAsync();
        var (_, stranger) = await RegisterVerifiedMemberAsync();
        var landlord = await SeedLandlordAsync();

        var reservation = await SeedReservationAsync(
            landlord, UserId.From(tenant.User.Id), HousingReservationStatus.Occupied);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(stranger.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords/{landlord.LandlordId.Value}/reviews",
            new { reservationId = reservation.Value, rating = 1, body = "Not my tenancy." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_landlord_nobody_has_rated_reports_no_reviews_rather_than_a_score()
    {
        var landlord = await SeedLandlordAsync();

        HttpClient.DefaultRequestHeaders.Authorization = null;

        var page = await ReadPageAsync(landlord.LandlordId);

        page.Rating.TotalReviews.Should().Be(0);
        page.Rating.Average.Should().Be(0d);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task The_reviews_are_readable_without_signing_in()
    {
        var landlord = await SeedLandlordAsync();

        HttpClient.DefaultRequestHeaders.Authorization = null;

        var response = await HttpClient.GetAsync(
            $"{Root}/landlords/{landlord.LandlordId.Value}/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task A_rating_outside_one_to_five_is_refused(int rating)
    {
        var (_, tenant) = await RegisterVerifiedMemberAsync();
        var landlord = await SeedLandlordAsync();
        var reservation = await SeedReservationAsync(
            landlord, UserId.From(tenant.User.Id), HousingReservationStatus.Occupied);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(tenant.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords/{landlord.LandlordId.Value}/reviews",
            new { reservationId = reservation.Value, rating, body = "Out of range." });

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── Seeding ──────────────────────────────────────────────────────────────────────────────

    private static System.Net.Http.Headers.AuthenticationHeaderValue Bearer(string token) =>
        new("Bearer", token);

    private async Task<ReviewPageShape> ReadPageAsync(LandlordProfileId landlordId)
    {
        var response = await HttpClient.GetAsync($"{Root}/landlords/{landlordId.Value}/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = JsonSerializer.Deserialize<Envelope<ReviewPageShape>>(
            await response.Content.ReadAsStringAsync(), JsonOptions);

        envelope!.Data.Should().NotBeNull();
        return envelope.Data;
    }

    private async Task<(LandlordProfileId LandlordId, HousingListingId ListingId)>
        SeedLandlordAsync()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;
        var landlordId = LandlordProfileId.New();
        var suffix = Guid.NewGuid().ToString("N");

        var landlord = LandlordProfile.Register(
            landlordId,
            UserId.New(),
            LandlordIdentity.Create($"Letting {suffix[..8]}", LandlordKind.Individual).Value,
            HousingSlug.Create($"letting-{suffix}"),
            "DE",
            LandlordContact.None,
            now).Value;

        dbContext.Set<LandlordProfile>().Add(landlord);

        await dbContext.SaveChangesAsync();

        // The listing is a bare identifier on purpose: reservations carry no foreign key to one,
        // and seeding a whole property would test the listing factory rather than this feature.
        return (landlordId, HousingListingId.New());
    }

    private async Task<HousingReservationId> SeedReservationAsync(
        (LandlordProfileId LandlordId, HousingListingId ListingId) landlord,
        UserId tenantId,
        HousingReservationStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;

        var reservation = HousingReservation.Open(
            HousingReservationId.New(),
            landlord.ListingId,
            landlord.LandlordId,
            tenantId,
            units: 1,
            ReservationTerms.Create(now.AddDays(7), null, null).Value,
            now,
            Guid.NewGuid()).Value;

        // Walked through the real transitions rather than assigned, so a reservation this suite
        // calls Occupied is one the aggregate agrees could reach Occupied.
        if (status is HousingReservationStatus.Reserved
            or HousingReservationStatus.LeaseSigned
            or HousingReservationStatus.Occupied)
        {
            reservation.Accept(tenantId, null, now, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        }

        if (status is HousingReservationStatus.LeaseSigned or HousingReservationStatus.Occupied)
        {
            reservation.RecordLeaseSigned(tenantId, now, Guid.NewGuid())
                .IsSuccess.Should().BeTrue();
        }

        if (status == HousingReservationStatus.Occupied)
        {
            reservation.RecordOccupied(tenantId, now, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        }

        if (status == HousingReservationStatus.Cancelled)
        {
            reservation.Cancel(tenantId, "Changed my mind.", now, Guid.NewGuid())
                .IsSuccess.Should().BeTrue();
        }

        reservation.Status.Should().Be(status, "the seeded state must be the state under test");

        dbContext.Set<HousingReservation>().Add(reservation);

        await dbContext.SaveChangesAsync();

        return reservation.Id;
    }
}
