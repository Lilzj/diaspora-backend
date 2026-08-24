using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Seller ratings, and the completed purchase that has to exist behind one.
/// <para>
/// The client rendered <c>seller.rating != null ? seller.rating.toFixed(1) : '4.8'</c> above
/// "Pay with Diaspora" — and since no rating field existed anywhere in the module, every seller
/// on the platform showed the same invented 4.8. These assert the thing that replaced it: a star
/// that a completed reservation had to produce.
/// </para>
/// <para>
/// The purchase is seeded through the domain rather than driven over HTTP. Driving it would test
/// the reservation lifecycle, which has its own suite; what needs proving here is what the review
/// endpoint does when a purchase does or does not exist.
/// </para>
/// </summary>
public sealed class MarketplaceSellerReviewTests : IdentityTestBase
{
    private const string Root = "/api/v1/marketplace";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MarketplaceSellerReviewTests(CustomWebApplicationFactory factory) : base(factory) { }

    private sealed record Envelope<T>(T Data);

    private sealed record RatingShape(double Average, int TotalReviews);

    private sealed record ReviewShape(
        Guid ReviewId, Guid ReservationId, int Rating, string? Title, string? Body, bool CanEdit);

    private sealed record ReviewPageShape(RatingShape Rating, List<ReviewShape> Items);

    /// <summary>
    /// The gate. Without this the feature is the hardcoded 4.8 with extra steps.
    /// </summary>
    [Fact]
    public async Task A_member_who_has_bought_nothing_cannot_rate_a_seller()
    {
        var (_, buyer) = await RegisterVerifiedMemberAsync();
        var seller = await SeedSellerAsync();

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(buyer.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers/{seller.SellerId.Value}/reviews",
            new { reservationId = Guid.NewGuid(), rating = 5, body = "Never bought anything." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("marketplace.review.no-completed-purchase");
    }

    /// <summary>
    /// A reservation that exists but has not been handed over is not a purchase. B10 is explicit
    /// that <c>Completed</c> is "the only state that turns held stock into sold stock".
    /// </summary>
    [Theory]
    [InlineData(ReservationStatus.Pending)]
    [InlineData(ReservationStatus.AwaitingCollection)]
    [InlineData(ReservationStatus.Cancelled)]
    public async Task An_unfinished_reservation_does_not_earn_the_right_to_rate(
        ReservationStatus status)
    {
        var (_, buyer) = await RegisterVerifiedMemberAsync();
        var seller = await SeedSellerAsync();
        var reservation = await SeedReservationAsync(
            seller, UserId.From(buyer.User.Id), status);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(buyer.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers/{seller.SellerId.Value}/reviews",
            new { reservationId = reservation.Value, rating = 5, body = "Not collected yet." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_completed_purchase_earns_a_rating_that_the_rollup_then_carries()
    {
        var (_, buyer) = await RegisterVerifiedMemberAsync();
        var seller = await SeedSellerAsync();
        var reservation = await SeedReservationAsync(
            seller, UserId.From(buyer.User.Id), ReservationStatus.Completed);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(buyer.Tokens.AccessToken);

        var written = await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers/{seller.SellerId.Value}/reviews",
            new
            {
                reservationId = reservation.Value,
                rating = 4,
                title = "Straightforward",
                body = "Met at the station, item as described."
            });

        written.StatusCode.Should().Be(HttpStatusCode.Created);

        var page = await ReadPageAsync(seller.SellerId);

        page.Rating.TotalReviews.Should().Be(1);
        page.Rating.Average.Should().Be(4d);
        page.Items.Should().ContainSingle().Which.Rating.Should().Be(4);

        // The rollup is read back out of the database, not out of the request that wrote it.
        // The first version of this passed its buckets to the database correctly and then served
        // a total of zero beside them, because EF materialises an owned type without running the
        // constructor that had done the arithmetic.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var stored = db.Set<SellerProfile>()
            .AsNoTracking()
            .First(candidate => candidate.Id == seller.SellerId);

        stored.ReviewSummary.FourStar.Should().Be(1);
        stored.ReviewSummary.TotalReviews.Should().Be(1);
        stored.ReviewSummary.Average.Should().Be(4d);
    }

    /// <summary>
    /// One review per purchase. A second write revises rather than stacking, which is also what
    /// the unique index on the reservation enforces underneath.
    /// </summary>
    [Fact]
    public async Task Rating_the_same_purchase_twice_revises_it_rather_than_counting_twice()
    {
        var (_, buyer) = await RegisterVerifiedMemberAsync();
        var seller = await SeedSellerAsync();
        var reservation = await SeedReservationAsync(
            seller, UserId.From(buyer.User.Id), ReservationStatus.Completed);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(buyer.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers/{seller.SellerId.Value}/reviews",
            new { reservationId = reservation.Value, rating = 5, body = "First impression." });

        await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers/{seller.SellerId.Value}/reviews",
            new { reservationId = reservation.Value, rating = 2, body = "It broke a week later." });

        var page = await ReadPageAsync(seller.SellerId);

        page.Rating.TotalReviews.Should().Be(1, "one purchase is one review");
        page.Rating.Average.Should().Be(2d, "the revision is what counts, not the first attempt");
    }

    /// <summary>
    /// A buyer cannot rate somebody else's purchase. Checked because the reservation id is a
    /// client-supplied guid, and the only thing between that and rating a stranger's transaction
    /// is this handler.
    /// </summary>
    [Fact]
    public async Task A_purchase_belonging_to_somebody_else_cannot_be_rated()
    {
        var (_, buyer) = await RegisterVerifiedMemberAsync();
        var (_, stranger) = await RegisterVerifiedMemberAsync();
        var seller = await SeedSellerAsync();

        var reservation = await SeedReservationAsync(
            seller, UserId.From(buyer.User.Id), ReservationStatus.Completed);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(stranger.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers/{seller.SellerId.Value}/reviews",
            new { reservationId = reservation.Value, rating = 1, body = "Not my purchase." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// An unrated seller reads as unrated, and the rollup says zero of zero rather than a star.
    /// This is the state the client used to paper over with 4.8.
    /// </summary>
    [Fact]
    public async Task A_seller_nobody_has_rated_reports_no_reviews_rather_than_a_score()
    {
        var seller = await SeedSellerAsync();

        HttpClient.DefaultRequestHeaders.Authorization = null;

        var page = await ReadPageAsync(seller.SellerId);

        page.Rating.TotalReviews.Should().Be(0);
        page.Rating.Average.Should().Be(0d);
        page.Items.Should().BeEmpty();
    }

    /// <summary>Readable without an account — see the query's note on why.</summary>
    [Fact]
    public async Task The_reviews_are_readable_without_signing_in()
    {
        var seller = await SeedSellerAsync();

        HttpClient.DefaultRequestHeaders.Authorization = null;

        var response = await HttpClient.GetAsync(
            $"{Root}/sellers/{seller.SellerId.Value}/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task A_rating_outside_one_to_five_is_refused(int rating)
    {
        var (_, buyer) = await RegisterVerifiedMemberAsync();
        var seller = await SeedSellerAsync();
        var reservation = await SeedReservationAsync(
            seller, UserId.From(buyer.User.Id), ReservationStatus.Completed);

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(buyer.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers/{seller.SellerId.Value}/reviews",
            new { reservationId = reservation.Value, rating, body = "Out of range." });

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── Seeding ──────────────────────────────────────────────────────────────────────────────

    private static System.Net.Http.Headers.AuthenticationHeaderValue Bearer(string token) =>
        new("Bearer", token);

    private async Task<ReviewPageShape> ReadPageAsync(SellerProfileId sellerId)
    {
        var response = await HttpClient.GetAsync($"{Root}/sellers/{sellerId.Value}/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = JsonSerializer.Deserialize<Envelope<ReviewPageShape>>(
            await response.Content.ReadAsStringAsync(), JsonOptions);

        envelope!.Data.Should().NotBeNull();
        return envelope.Data;
    }

    private async Task<(SellerProfileId SellerId, MarketplaceListingId ListingId)> SeedSellerAsync()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;
        var sellerId = SellerProfileId.New();
        var suffix = Guid.NewGuid().ToString("N");

        var seller = SellerProfile.Create(
            sellerId,
            UserId.New(),
            SellerIdentity.Create($"Storefront {suffix[..8]}", null, null, SellerKind.Individual).Value,
            ListingSlug.Create($"storefront-{suffix}").Value,
            "DE",
            now).Value;

        var listing = MarketplaceListing.Create(
            MarketplaceListingId.New(),
            sellerId,
            seller.OwnerUserId,
            ListingTitle.Create("A reviewable item").Value,
            ListingSlug.Create($"reviewable-item-{suffix}").Value,
            ListingDescription.Create("Something a buyer can collect and then rate.").Value,
            "furniture",
            ListingType.FixedPrice,
            ProductCondition.Good,
            ListingPrice.Create(120m, "EUR", isNegotiable: false, minimumOffer: null).Value,
            ListingLocationContext.Create("DE", cityName: "Berlin").Value,
            now).Value;

        listing.Publish(now);

        dbContext.Set<SellerProfile>().Add(seller);
        dbContext.Set<MarketplaceListing>().Add(listing);

        await dbContext.SaveChangesAsync();

        return (sellerId, listing.Id);
    }

    private async Task<ListingReservationId> SeedReservationAsync(
        (SellerProfileId SellerId, MarketplaceListingId ListingId) seller,
        UserId buyerId,
        ReservationStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;

        var reservation = ListingReservation.Open(
            ListingReservationId.New(),
            seller.ListingId,
            seller.SellerId,
            buyerId,
            quantity: 1,
            CollectionWindow.Unscheduled,
            now,
            Guid.NewGuid()).Value;

        // Walked through the real transitions rather than assigned, so a reservation that this
        // suite says is Completed is one the aggregate agrees could reach Completed.
        if (status is ReservationStatus.AwaitingCollection or ReservationStatus.Completed)
        {
            reservation.Acknowledge(buyerId, null, now, Guid.NewGuid())
                .IsSuccess.Should().BeTrue();
        }

        if (status == ReservationStatus.Completed)
        {
            reservation.Complete(buyerId, now, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        }

        if (status == ReservationStatus.Cancelled)
        {
            reservation.Cancel(buyerId, "Changed my mind.", now, Guid.NewGuid())
                .IsSuccess.Should().BeTrue();
        }

        reservation.Status.Should().Be(status, "the seeded state must be the state under test");

        dbContext.Set<ListingReservation>().Add(reservation);

        await dbContext.SaveChangesAsync();

        return reservation.Id;
    }
}
