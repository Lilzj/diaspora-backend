using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Challenges;
using Diaspora.Modules.Creator.Application.Consumers;
using Diaspora.Modules.Creator.Application.Milestones;
using Diaspora.Modules.Creator.Application.Referrals;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Domain.Activity;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// BG-10. Who a publication is credited to.
/// <para>
/// <b>The defect these pin.</b> Three consumers passed a <em>profile</em> identifier into
/// <see cref="CreatorActivityRecorder.RecordAsync"/>, which takes a member:
/// <c>SellerId</c> from Marketplace, <c>EmployerId</c> from Jobs and <c>LandlordId</c> from Housing.
/// None of the three is a person. Because the recorder opens a creator record for whatever it is
/// handed, each mis-keyed publication both failed to credit the member and minted a creator profile
/// for a seller, landlord or employer profile id — 53 of them in the release-candidate database,
/// every one of which was sitting on the Contributor leaderboard, while 31 real members who had
/// published read <c>NotACreator</c> on their own profile.
/// </para>
/// <para>
/// <b>Why these assert on the repository rather than on the recorder.</b> The recorder's first act
/// is <see cref="ICreatorProfileRepository.FindByUserAsync"/> with the identifier it was given, so
/// the argument that reaches it <em>is</em> the attribution decision. A test that substituted the
/// recorder itself would assert that a mock was called and prove nothing about which field the
/// consumer read.
/// </para>
/// </summary>
public sealed class CreatorActivityAttributionTests
{
    private static readonly Guid MemberId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SubjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // ── Marketplace ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_published_listing_is_credited_to_the_member_behind_the_seller_profile()
    {
        var creators = Recorder(out var recorder);

        await new ListingPublishedCreatorConsumer(recorder).Handle(
            Notify(Listing(sellerUserId: MemberId)), CancellationToken.None);

        await creators.Received(1).FindByUserAsync(MemberId, Arg.Any<CancellationToken>());
        await creators.DidNotReceive().FindByUserAsync(ProfileId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <b>The fallback is the defect, so there is no fallback.</b> <c>SellerUserId</c> is nullable
    /// because outbox rows written before Wave 12 do not carry it. Recording nothing for those is
    /// right; recording them against <c>SellerId</c> is what produced 53 phantom creators, and a
    /// repair that left the fallback in place would refill the table it had just emptied.
    /// </summary>
    [Fact]
    public async Task A_listing_published_before_the_member_id_was_carried_credits_nobody()
    {
        var creators = Recorder(out var recorder);

        await new ListingPublishedCreatorConsumer(recorder).Handle(
            Notify(Listing(sellerUserId: null)), CancellationToken.None);

        await creators.DidNotReceiveWithAnyArgs().FindByUserAsync(default, default);
    }

    // ── Jobs ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_published_vacancy_is_credited_to_the_member_behind_the_employer()
    {
        var creators = Recorder(out var recorder);

        await new JobPublishedCreatorConsumer(recorder).Handle(
            Notify(Job(employerUserId: MemberId)), CancellationToken.None);

        await creators.Received(1).FindByUserAsync(MemberId, Arg.Any<CancellationToken>());
        await creators.DidNotReceive().FindByUserAsync(ProfileId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_vacancy_published_before_the_member_id_was_carried_credits_nobody()
    {
        var creators = Recorder(out var recorder);

        await new JobPublishedCreatorConsumer(recorder).Handle(
            Notify(Job(employerUserId: null)), CancellationToken.None);

        await creators.DidNotReceiveWithAnyArgs().FindByUserAsync(default, default);
    }

    // ── Housing ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Housing was not named in BG-10's original write-up, which listed Marketplace and Jobs. It was
    /// found by asking the database which action types resolved to a member and was the third.
    /// </summary>
    [Fact]
    public async Task A_published_property_is_credited_to_the_member_behind_the_landlord()
    {
        var creators = Recorder(out var recorder);

        await new HousingPublishedCreatorConsumer(recorder).Handle(
            Notify(Housing(landlordUserId: MemberId)), CancellationToken.None);

        await creators.Received(1).FindByUserAsync(MemberId, Arg.Any<CancellationToken>());
        await creators.DidNotReceive().FindByUserAsync(ProfileId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_property_published_before_the_member_id_was_carried_credits_nobody()
    {
        var creators = Recorder(out var recorder);

        await new HousingPublishedCreatorConsumer(recorder).Handle(
            Notify(Housing(landlordUserId: null)), CancellationToken.None);

        await creators.DidNotReceiveWithAnyArgs().FindByUserAsync(default, default);
    }

    // ── The contracts ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The field cannot quietly go away.</b> Each of the three contracts carries the owning member
    /// alongside the profile, and a consumer has no other way to reach the person. Deleting one would
    /// compile — the consumer would fall back to the profile id or stop building — so it is asserted
    /// here rather than left to a reviewer.
    /// </summary>
    [Fact]
    public void Every_publication_contract_carries_the_owning_member()
    {
        typeof(ListingPublishedIntegrationEvent).GetProperty("SellerUserId")
            .Should().NotBeNull("a listing's creator activity is keyed to the member, not the seller profile");

        typeof(JobPublishedIntegrationEvent).GetProperty("EmployerUserId")
            .Should().NotBeNull("a vacancy's creator activity is keyed to the member, not the employer");

        typeof(HousingPublishedIntegrationEvent).GetProperty("LandlordUserId")
            .Should().NotBeNull("a property's creator activity is keyed to the member, not the landlord profile");
    }

    // ── The migration's two duplications ────────────────────────────────────────────────────

    /// <summary>
    /// <b>Pins one half of a duplication that is real.</b> <c>RepairCreatorActivityAttribution</c>
    /// recomputes <c>TotalContentPublished</c> from a publication list spelled out in SQL, because a
    /// migration is fixed to its own moment in history and must not follow a constant a later release
    /// may extend. That is the right call and it is only safe while somebody notices the two moving
    /// apart. If this fails, the constant grew: leave the migration alone — it has already run — and
    /// check whether a newer repair is needed for the new action type.
    /// </summary>
    [Fact]
    public void The_publication_list_the_repair_migration_recomputes_from_is_still_the_whole_list()
    {
        CreatorActivityTypes.Publications.Should().BeEquivalentTo(
        [
            "guide.published",
            "post.published",
            "event.published",
            "provider.published",
            "listing.published",
            "job.published",
            "housing.published"
        ]);
    }

    /// <summary>
    /// <b>Pins the other half.</b> The migration promotes repaired members from <c>NotACreator</c> to
    /// <c>Contributor</c> in SQL, guarded on every condition
    /// <see cref="CreatorStatusPolicy"/> reaches that branch through. This asserts the branch itself:
    /// one recognised action, nothing else, is a Contributor. If the threshold or the rung changes,
    /// this fails and the SQL is known to be stale rather than discovered to be later.
    /// </summary>
    [Fact]
    public void One_recognised_action_and_nothing_else_is_a_contributor()
    {
        var creator = CreatorProfile.Open(
            CreatorId.From(Guid.NewGuid()), MemberId, "Member", "GB", DateTimeOffset.UtcNow);

        CreatorStatusPolicy.Resolve(creator).Earned.Should().Be(
            CreatorStatus.NotACreator, "nothing has been published yet");

        creator.RecordActivity(CreatorActivityTypes.ListingPublished, 1, DateTimeOffset.UtcNow);

        CreatorStatusPolicy.Resolve(creator).Earned.Should().Be(
            CreatorStatus.Contributor,
            "one recognised action is the Contributor threshold, and the repair migration's "
            + "status update depends on it");
    }

    /// <summary>
    /// A referral qualifies on the invitee's first contribution to the community (RWD-03). The
    /// programme's own records, a referral qualifying or a course finished, are not one: without this
    /// a member's referral would qualify because they referred somebody else.
    /// </summary>
    [Fact]
    public void A_qualifying_contribution_is_a_contribution_to_the_community()
    {
        CreatorActivityTypes.CommunityContributions.Should().NotContain(
            [CreatorActivityTypes.ReferralQualified, CreatorActivityTypes.CourseCompleted]);
        CreatorActivityTypes.CommunityContributions.Should().Contain(CreatorActivityTypes.Publications);
        CreatorActivityTypes.CommunityContributions.Should().HaveCount(CreatorActivityTypes.All.Count - 2);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A real <see cref="CreatorActivityRecorder"/> with substituted collaborators. The returned
    /// repository is the assertion surface: the identifier the consumer chose is the identifier
    /// <see cref="ICreatorProfileRepository.FindByUserAsync"/> is asked for.
    /// </summary>
    private static ICreatorProfileRepository Recorder(out CreatorActivityRecorder recorder)
    {
        var creators = Substitute.For<ICreatorProfileRepository>();
        var activity = Substitute.For<ICreatorActivityRepository>();
        var rewards = Substitute.For<ICreatorRewardRepository>();
        var clock = Substitute.For<IClock>();
        var guids = Substitute.For<IGuidGenerator>();

        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        guids.NewSequentialGuid().Returns(_ => Guid.NewGuid());

        // Nothing exists yet, which is the shape that produced the phantoms: the recorder opens a
        // record for whatever identifier it was handed.
        creators.FindByUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CreatorProfile?)null);
        activity.ExistsAsync(Arg.Any<CreatorId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        recorder = new CreatorActivityRecorder(
            creators,
            activity,
            rewards,
            new MilestoneProgressService(
                Substitute.For<ICreatorMilestoneRepository>(),
                Substitute.For<ICreatorMilestoneProgressRepository>(),
                guids,
                clock),
            new ChallengeProgressService(
                Substitute.For<ICreatorChallengeRepository>(),
                Substitute.For<ICreatorChallengeEnrollmentRepository>(),
                clock),
            new ReferralQualificationService(
                Substitute.For<ICreatorReferralRepository>(),
                activity,
                creators,
                NullLogger<ReferralQualificationService>.Instance,
                guids,
                clock),
            new CreatorStatusService(
                creators,
                Substitute.For<ICreatorVerificationRepository>(),
                Substitute.For<ITrustService>(),
                clock),
            Substitute.For<IUnitOfWork>(),
            guids,
            clock);

        return creators;
    }

    private static IntegrationEventNotification<T> Notify<T>(T integrationEvent)
        where T : Diaspora.Contracts.Events.IIntegrationEvent => new(integrationEvent);

    private static ListingPublishedIntegrationEvent Listing(Guid? sellerUserId) => new()
    {
        ListingId = SubjectId,
        SellerId = ProfileId,
        SellerUserId = sellerUserId,
        SellerName = "A seller",
        SellerType = "Individual",
        IsSellerVerified = false,
        Title = "A listing",
        Slug = "a-listing",
        CategoryKey = "furniture",
        Tags = [],
        ListingType = "FixedPrice",
        Condition = "Good",
        IsPriceDisclosed = true,
        PriceAmount = 10m,
        CurrencyCode = "GBP",
        IsNegotiable = false,
        Quantity = 1,
        CountryCode = "GB",
        OffersPickup = true,
        OffersDelivery = false,
        OffersShipping = false,
        IsFirstPublication = true,
        PublishedAt = DateTimeOffset.UtcNow
    };

    private static JobPublishedIntegrationEvent Job(Guid? employerUserId) => new()
    {
        JobId = SubjectId,
        EmployerId = ProfileId,
        EmployerUserId = employerUserId,
        EmployerName = "An employer",
        IsEmployerVerified = false,
        Title = "A vacancy",
        Slug = "a-vacancy",
        CategoryKey = "engineering",
        Tags = [],
        SkillKeys = [],
        CountryCode = "GB",
        WorkplaceType = "Remote",
        JobType = "FullTime",
        ExperienceLevel = "Mid",
        OffersVisaSponsorship = false,
        OffersRelocationAssistance = false,
        IsSalaryDisclosed = false,
        VacancyCount = 1,
        IsFirstPublication = true,
        PublishedAt = DateTimeOffset.UtcNow
    };

    private static HousingPublishedIntegrationEvent Housing(Guid? landlordUserId) => new()
    {
        ListingId = SubjectId,
        LandlordId = ProfileId,
        LandlordUserId = landlordUserId,
        LandlordName = "A landlord",
        LandlordKind = "Individual",
        IsLandlordVerified = false,
        Title = "A property",
        Slug = "a-property",
        AccommodationTypeKey = "flat",
        IsRentDisclosed = true,
        RentAmount = 900m,
        CurrencyCode = "GBP",
        RentPeriod = "PerMonth",
        Bedrooms = 2,
        Bathrooms = 1m,
        FurnishedStatus = "Unfurnished",
        TotalUnits = 1,
        AcceptsShortStay = false,
        AcceptsLongStay = true,
        IsStudentFriendly = false,
        IsFamilyFriendly = true,
        IsSharedAccommodation = false,
        CountryCode = "GB",
        IsFirstPublication = true,
        PublishedAt = DateTimeOffset.UtcNow
    };
}
