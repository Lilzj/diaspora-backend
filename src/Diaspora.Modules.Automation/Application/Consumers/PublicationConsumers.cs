using System.Globalization;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;

namespace Diaspora.Modules.Automation.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-009 · GAP-010 · GAP-011 · GAP-012 · GAP-015 · GAP-020 — publications.
//
// Five publishing modules, one matcher. §30 asks for one implementation shared by jobs, marketplace,
// housing, providers and events, and the way that stays true is that each consumer's whole job is to
// reduce its own contract to a flat bag of the filters that module's own list route already accepts.
// Nothing here knows what a saved search is.
//
// **A saved search must never surface an item the member could not have found by searching**, and
// that is a property of *which contracts these consume* rather than of a visibility check here. Only
// publication contracts reach this file, so a private or unpublished item never becomes a candidate.
// A check in this file would be a second, divergent copy of five modules' visibility rules.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A job was published.
/// <para>
/// <b>Two things follow from one publication, and one consumer does both</b> — a saved search may
/// match it now, and its closing date is a moment the platform must come back to before. Wave 12 adds
/// the second here rather than in a consumer of its own because
/// <c>No_module_consumes_one_integration_event_twice</c> forbids the second: two settlement owners
/// inside one module for one at-least-once fact is two things happening twice on a redelivery.
/// </para>
/// </summary>
public sealed class JobPublishedAutomationConsumer
    : SavedSearchConsumer<JobPublishedIntegrationEvent>
{
    private readonly AutomationSubjectHorizonService _horizons;

    public JobPublishedAutomationConsumer(
        SavedSearchMatcher matcher, AutomationEngine engine, AutomationSubjectHorizonService horizons)
        : base(matcher, engine) => _horizons = horizons;

    protected override SavedSearchSubject Subject => SavedSearchSubject.Jobs;

    /// <summary>
    /// FRONTEND-PHASE18-GAP-010 · W12-GAP-002 — <c>job.closingSoon</c>.
    /// <para>
    /// <b>The owner set is empty, and that is the platform's established reading rather than an
    /// omission.</b> The frontend calls this "a job I saved is about to close", and the honest backend
    /// answer to "which jobs did this member save" lives in the Bookmarks module. Wave 11's
    /// <c>event.created</c>, <c>guide.published</c> and <c>guide.savedChanged</c> consumers all pass an
    /// empty owner set for the same reason: the fact concerns anybody watching for it, and the
    /// member's own conditions — <c>country</c> and <c>category</c>, which are exactly the two the
    /// frontend offers on this trigger — narrow it. Reading another module's bookmarks here would be
    /// the cross-module read the whole arrangement exists without.
    /// </para>
    /// </summary>
    protected override async Task HandleAsync(
        JobPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await base.HandleAsync(integrationEvent, cancellationToken);

        if (integrationEvent.ClosingAt is not { } closingAt)
        {
            // "Null means open until the employer closes it." There is no horizon to remember.
            return;
        }

        var horizonValues = Values();
        horizonValues["country"] = integrationEvent.CountryCode;
        horizonValues["category"] = integrationEvent.CategoryKey;

        await _horizons.ArmAsync(
            "job.closingSoon",
            AutomationSubjectKinds.Job,
            integrationEvent.JobId,
            integrationEvent.Title,
            closingAt,
            null,
            [],
            horizonValues,
            $"{integrationEvent.Title} is about to stop taking applications.",
            cancellationToken);
    }

    protected override SavedSearchCandidate? ToCandidate(JobPublishedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Only a first publication counts. A republished edit is not a new job, and a member told
        // twice about one posting stops trusting the alert.
        if (!integrationEvent.IsFirstPublication)
        {
            return null;
        }

        var fields = Fields();
        fields["country"] = integrationEvent.CountryCode;
        fields["category"] = integrationEvent.CategoryKey;
        fields["city"] = integrationEvent.CityName;
        fields["region"] = integrationEvent.RegionName;
        fields["workplaceType"] = integrationEvent.WorkplaceType;
        fields["jobType"] = integrationEvent.JobType;
        fields["experienceLevel"] = integrationEvent.ExperienceLevel;
        fields["visaSponsorship"] = integrationEvent.OffersVisaSponsorship ? "true" : "false";

        // Only where the employer disclosed it. A salary bound that matched on an undisclosed figure
        // would be the platform inferring a number the employer chose not to publish.
        if (integrationEvent.IsSalaryDisclosed && integrationEvent.SalaryMinimum is { } minimum)
        {
            fields["salary"] = minimum.ToString(CultureInfo.InvariantCulture);
        }

        // Remediation Wave G. Two faults on one line: the prefix was `/jobs`, which is
        // the API's and not the client's, and the identifier was the slug while the
        // client's route is `/job/[id]`. Correcting only the prefix would have produced
        // a link that resolved to a screen and then queried `GET /api/v1/jobs/{slug}`,
        // which is a different route from `GET /api/v1/jobs/slug/{slug}` and 404s.
        return new SavedSearchCandidate(
            integrationEvent.JobId,
            integrationEvent.Title,
            fields,
            $"/job/{integrationEvent.JobId}");
    }
}

/// <summary>
/// An application was submitted.
/// <para>
/// One consumer, two trigger keys. <c>applicationSubmitted</c> and <c>applicationThreshold</c> are two
/// rules over one fact, and two consumers over one contract would be two settlement owners inside one
/// module — which is what DSP1003 reports and what makes an at-least-once consequence apply twice.
/// </para>
/// </summary>
public sealed class ApplicationSubmittedAutomationConsumer
    : AutomationEventConsumer<ApplicationSubmittedIntegrationEvent>
{
    public ApplicationSubmittedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        ApplicationSubmittedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["country"] = integrationEvent.CountryCode;
        values["category"] = integrationEvent.CategoryKey;

        // The employer is the owner. The applicant's identity is deliberately not carried: an
        // employer's automation is about their posting, not about who applied to it.
        var owners = Owners(integrationEvent.EmployerId, integrationEvent.EmployerUserId);

        yield return Context(
            "job.applicationSubmitted",
            integrationEvent.EventId,
            owners,
            "Somebody applied to one of your postings.",
            integrationEvent.OccurredAt,
            "job",
            integrationEvent.JobId,
            null,
            null,
            null,
            values);

        yield return Context(
            "job.applicationThreshold",
            integrationEvent.EventId,
            owners,
            "One of your postings reached a number of applications.",
            integrationEvent.OccurredAt,
            "job",
            integrationEvent.JobId,
            null,
            null,
            null,
            values);
    }
}

/// <summary>An application's status moved. Concerns the candidate.</summary>
public sealed class ApplicationStatusChangedAutomationConsumer
    : AutomationEventConsumer<ApplicationStatusChangedIntegrationEvent>
{
    public ApplicationStatusChangedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        ApplicationStatusChangedIntegrationEvent integrationEvent)
    {
        if (integrationEvent.CandidateUserId is not { } candidate)
        {
            // No member the platform can identify. An ordinary outcome; nothing runs.
            yield break;
        }

        var values = Values();

        // Server-evaluated, and this is why: the client's copy of an application status can be stale,
        // and a rule firing on a stale copy is a member told they were shortlisted when they were not.
        values["applicationStatus"] = integrationEvent.CurrentStatus;

        yield return Context(
            "job.applicationStatusChanged",
            integrationEvent.EventId,
            Owners(candidate),

            // The status is a public fact about the member's own application, so the summary may name
            // it. A sensitive *condition's* comparison is what never appears — see the run record.
            $"Your application moved to {integrationEvent.CurrentStatus}.",
            integrationEvent.OccurredAt,
            "application",
            integrationEvent.ApplicationId,
            null,
            null,
            null,
            values);
    }
}

/// <summary>
/// A marketplace listing was published.
/// <para>
/// Saved-search matching, and — Wave 12 — the expiry horizon. One consumer for the same reason the
/// job publication has one: a second over the same contract is a second settlement owner.
/// </para>
/// </summary>
public sealed class ListingPublishedAutomationConsumer
    : SavedSearchConsumer<ListingPublishedIntegrationEvent>
{
    private readonly AutomationSubjectHorizonService _horizons;

    public ListingPublishedAutomationConsumer(
        SavedSearchMatcher matcher, AutomationEngine engine, AutomationSubjectHorizonService horizons)
        : base(matcher, engine) => _horizons = horizons;

    protected override SavedSearchSubject Subject => SavedSearchSubject.Marketplace;

    /// <summary>
    /// FRONTEND-PHASE18-GAP-011 · W12-GAP-003 — <c>listing.expiring</c>.
    /// <para>
    /// Unlike the job case the owner <i>is</i> specific — the frontend says "one of <b>my</b>
    /// listings" and scopes the trigger to <c>seller</c> — so both the seller profile and the member
    /// behind it are named. <c>SellerUserId</c> is Wave 12's one appended field on this contract, and
    /// it exists for this: a seller-scoped automation's owner is the <em>member</em>, so a horizon
    /// naming only the profile would match nobody.
    /// </para>
    /// </summary>
    protected override async Task HandleAsync(
        ListingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await base.HandleAsync(integrationEvent, cancellationToken);

        if (integrationEvent.ExpiresAt is not { } expiresAt)
        {
            return;
        }

        var horizonValues = Values();
        horizonValues["category"] = integrationEvent.CategoryKey;
        horizonValues["country"] = integrationEvent.CountryCode;
        horizonValues["listingStatus"] = "active";

        await _horizons.ArmAsync(
            "listing.expiring",
            AutomationSubjectKinds.Listing,
            integrationEvent.ListingId,
            integrationEvent.Title,
            expiresAt,
            null,
            Owners(integrationEvent.SellerId, integrationEvent.SellerUserId),
            horizonValues,
            $"{integrationEvent.Title} is about to stop being shown.",
            cancellationToken);
    }

    protected override SavedSearchCandidate? ToCandidate(
        ListingPublishedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var fields = Fields();
        fields["country"] = integrationEvent.CountryCode;
        fields["category"] = integrationEvent.CategoryKey;
        fields["city"] = integrationEvent.CityName;
        fields["region"] = integrationEvent.RegionName;
        fields["listingType"] = integrationEvent.ListingType;
        fields["condition"] = integrationEvent.Condition;

        if (integrationEvent.IsPriceDisclosed && integrationEvent.PriceAmount is { } price)
        {
            fields["price"] = price.ToString(CultureInfo.InvariantCulture);
        }

        return new SavedSearchCandidate(
            integrationEvent.ListingId,
            integrationEvent.Title,
            fields,
            $"/marketplace/{integrationEvent.Slug}");
    }
}

/// <summary>An offer arrived on a listing. Concerns the seller.</summary>
public sealed class OfferCreatedAutomationConsumer
    : AutomationEventConsumer<OfferCreatedIntegrationEvent>
{
    public OfferCreatedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        OfferCreatedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["amountAbove"] = integrationEvent.Amount.ToString(CultureInfo.InvariantCulture);
        values["priceAbove"] = integrationEvent.Amount.ToString(CultureInfo.InvariantCulture);
        values["priceBelow"] = integrationEvent.Amount.ToString(CultureInfo.InvariantCulture);

        yield return Context(
            "listing.offerReceived",
            integrationEvent.EventId,

            // The seller. The buyer's identity is never carried: an offer is between two people and
            // an automation about it is the seller's own.
            Owners(integrationEvent.SellerId, integrationEvent.SellerUserId),
            $"You received an offer on {integrationEvent.ListingTitle}.",
            integrationEvent.OccurredAt,
            "listing",
            integrationEvent.ListingId,
            integrationEvent.ListingTitle,
            integrationEvent.ExpiresAt,
            null,
            values);
    }
}

/// <summary>
/// A listing sold.
/// <para>
/// Feeds the availability half of a saved search: the frontend's
/// <c>savedSearch.availabilityChanged</c> names a <c>ListingStatusChanged</c> contract Marketplace does
/// not publish, and this is the published fact that answers the same question for a member watching
/// something they wanted.
/// </para>
/// </summary>
public sealed class ListingSoldAutomationConsumer
    : SavedSearchConsumer<ListingSoldIntegrationEvent>
{
    private readonly AutomationSubjectHorizonService _horizons;

    public ListingSoldAutomationConsumer(
        SavedSearchMatcher matcher, AutomationEngine engine, AutomationSubjectHorizonService horizons)
        : base(matcher, engine) => _horizons = horizons;

    protected override SavedSearchSubject Subject => SavedSearchSubject.Marketplace;

    protected override string Change => "availabilityChange";

    /// <summary>
    /// Wave 12: a sale ends two horizons, and both must be cancelled.
    /// <para>
    /// <b>The reservation's, whether or not the listing sold out.</b> Marketplace deliberately does
    /// not publish a cancellation when a reservation completes in a sale — "completion is not
    /// published from here; the listing's own sold event reports it" — so this is the only fact
    /// saying the hold is over, and a reservation-ending horizon left armed would warn a seller about
    /// a reservation that completed.
    /// </para>
    /// <para>
    /// <b>The listing's, only when it sold out.</b> One unit of twenty going is not a listing that
    /// has stopped being shown, and its expiry still matters.
    /// </para>
    /// </summary>
    protected override async Task HandleAsync(
        ListingSoldIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await base.HandleAsync(integrationEvent, cancellationToken);

        if (integrationEvent.ReservationId is { } reservationId)
        {
            await _horizons.CancelForSubjectAsync(
                reservationId, "The reservation completed in a sale.", cancellationToken);
        }

        if (integrationEvent.IsSoldOut)
        {
            await _horizons.CancelForSubjectAsync(
                integrationEvent.ListingId, "The listing sold.", cancellationToken);
        }
    }

    protected override SavedSearchCandidate? ToCandidate(ListingSoldIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Only a sold-out listing is an availability change worth telling somebody about. One unit of
        // twenty going is not.
        if (!integrationEvent.IsSoldOut)
        {
            return null;
        }

        var fields = Fields();
        fields["country"] = integrationEvent.CountryCode;
        fields["category"] = integrationEvent.CategoryKey;
        fields["listingStatus"] = "sold";

        return new SavedSearchCandidate(
            integrationEvent.ListingId, "Something you were watching has sold", fields, null);
    }
}

/// <summary>A housing listing was published.</summary>
public sealed class HousingPublishedAutomationConsumer
    : SavedSearchConsumer<HousingPublishedIntegrationEvent>
{
    public HousingPublishedAutomationConsumer(SavedSearchMatcher matcher, AutomationEngine engine)
        : base(matcher, engine) { }

    protected override SavedSearchSubject Subject => SavedSearchSubject.Housing;

    protected override SavedSearchCandidate? ToCandidate(
        HousingPublishedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var fields = Fields();
        fields["accommodationType"] = integrationEvent.AccommodationTypeKey;
        fields["bedrooms"] = integrationEvent.Bedrooms.ToString(CultureInfo.InvariantCulture);
        fields["furnished"] = integrationEvent.FurnishedStatus;
        fields["shortStay"] = integrationEvent.AcceptsShortStay ? "true" : "false";
        fields["studentFriendly"] = integrationEvent.IsStudentFriendly ? "true" : "false";

        if (integrationEvent.IsRentDisclosed && integrationEvent.RentAmount is { } rent)
        {
            fields["rent"] = rent.ToString(CultureInfo.InvariantCulture);
            fields["price"] = rent.ToString(CultureInfo.InvariantCulture);
        }

        return new SavedSearchCandidate(
            integrationEvent.ListingId,
            integrationEvent.Title,
            fields,
            $"/housing/{integrationEvent.Slug}");
    }
}

/// <summary>A housing listing was let. The availability half, as above.</summary>
public sealed class HousingOccupiedAutomationConsumer
    : SavedSearchConsumer<HousingOccupiedIntegrationEvent>
{
    public HousingOccupiedAutomationConsumer(SavedSearchMatcher matcher, AutomationEngine engine)
        : base(matcher, engine) { }

    protected override SavedSearchSubject Subject => SavedSearchSubject.Housing;

    protected override string Change => "availabilityChange";

    protected override SavedSearchCandidate? ToCandidate(
        HousingOccupiedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!integrationEvent.IsFullyOccupied)
        {
            return null;
        }

        var fields = Fields();
        fields["country"] = integrationEvent.CountryCode;
        fields["accommodationType"] = integrationEvent.AccommodationTypeKey;
        fields["neighbourhood"] = integrationEvent.NeighborhoodAreaId?.ToString("D");
        fields["listingStatus"] = "let";

        return new SavedSearchCandidate(
            integrationEvent.ListingId, "Somewhere you were watching has been let", fields, null);
    }
}

/// <summary>A provider was published.</summary>
public sealed class ProviderPublishedAutomationConsumer
    : SavedSearchConsumer<ProviderPublishedIntegrationEvent>
{
    public ProviderPublishedAutomationConsumer(SavedSearchMatcher matcher, AutomationEngine engine)
        : base(matcher, engine) { }

    protected override SavedSearchSubject Subject => SavedSearchSubject.Providers;

    protected override SavedSearchCandidate? ToCandidate(
        ProviderPublishedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // A provider a member could not have found by searching must not reach a saved search. The
        // publication contract carries the visibility it was published at, so this is the one place a
        // check is not a second copy of somebody else's rule.
        if (!string.Equals(integrationEvent.Visibility, "Public", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fields = Fields();
        fields["country"] = integrationEvent.CountryCode;
        fields["category"] = integrationEvent.CategoryKey;
        fields["providerType"] = integrationEvent.ProviderType;
        fields["neighbourhood"] = integrationEvent.AreaId?.ToString("D");

        return new SavedSearchCandidate(
            integrationEvent.ProviderId, integrationEvent.Name, fields, null);
    }
}

/// <summary>A provider received a review. Concerns the provider's owner.</summary>
public sealed class ProviderReviewAutomationConsumer
    : AutomationEventConsumer<ProviderReviewReceivedIntegrationEvent>
{
    public ProviderReviewAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        ProviderReviewReceivedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["ratingBelow"] = integrationEvent.Rating.ToString(CultureInfo.InvariantCulture);

        yield return Context(
            "provider.reviewReceived",
            integrationEvent.EventId,
            Owners(integrationEvent.ProviderId, integrationEvent.OwnerUserId),

            // The rating, and not the reviewer. Who left it is between them and the provider.
            $"You received a {integrationEvent.Rating}-star review.",
            integrationEvent.OccurredAt,
            "provider",
            integrationEvent.ProviderId,
            null,
            null,
            null,
            values);
    }
}

/// <summary>An event was published. Feeds saved searches over events.</summary>
public sealed class EventPublishedAutomationConsumer
    : SavedSearchConsumer<EventPublishedIntegrationEvent>
{
    public EventPublishedAutomationConsumer(SavedSearchMatcher matcher, AutomationEngine engine)
        : base(matcher, engine) { }

    protected override SavedSearchSubject Subject => SavedSearchSubject.Events;

    protected override SavedSearchCandidate? ToCandidate(
        EventPublishedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!integrationEvent.IsFirstPublication)
        {
            return null;
        }

        var fields = Fields();
        fields["country"] = integrationEvent.CountryCode;
        fields["category"] = integrationEvent.CategoryKey;
        fields["city"] = integrationEvent.CityName;
        fields["region"] = integrationEvent.RegionName;
        fields["neighbourhood"] = integrationEvent.NeighborhoodAreaId?.ToString("D");
        fields["attendanceMode"] = integrationEvent.AttendanceMode;
        fields["free"] = integrationEvent.IsFree ? "true" : "false";
        fields["familyFriendly"] = integrationEvent.IsFamilyFriendly ? "true" : "false";
        fields["dayOfWeek"] = ((int)integrationEvent.StartsAt.DayOfWeek)
            .ToString(CultureInfo.InvariantCulture);

        return new SavedSearchCandidate(
            integrationEvent.CommunityEventId,
            integrationEvent.Title,
            fields,
            $"/community-life/event/{integrationEvent.Slug}");
    }
}

/// <summary>A guide was published.</summary>
public sealed class GuidePublishedAutomationConsumer
    : AutomationEventConsumer<GuidePublishedIntegrationEvent>
{
    public GuidePublishedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        GuidePublishedIntegrationEvent integrationEvent)
    {
        if (!integrationEvent.IsFirstPublication)
        {
            yield break;
        }

        var values = Values();
        values["country"] = integrationEvent.CountryCode;
        values["category"] = integrationEvent.CategoryKey;

        yield return Context(
            "guide.published",
            integrationEvent.EventId,
            [],
            $"A new guide: {integrationEvent.Title}",
            integrationEvent.OccurredAt,
            "guide",
            integrationEvent.GuideId,
            integrationEvent.Title,
            null,
            null,
            values);
    }
}

/// <summary>A guide changed.</summary>
public sealed class GuideUpdatedAutomationConsumer
    : AutomationEventConsumer<GuideUpdatedIntegrationEvent>
{
    public GuideUpdatedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        GuideUpdatedIntegrationEvent integrationEvent)
    {
        yield return Context(
            "guide.savedChanged",
            integrationEvent.EventId,
            [],
            "A guide you saved was updated.",
            integrationEvent.OccurredAt,
            "guide",
            integrationEvent.GuideId,
            null,
            null,
            null,
            Values());
    }
}

/// <summary>A guide reached a view milestone. Concerns its author.</summary>
public sealed class GuideMilestoneAutomationConsumer
    : AutomationEventConsumer<GuideViewMilestoneReachedIntegrationEvent>
{
    public GuideMilestoneAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        GuideViewMilestoneReachedIntegrationEvent integrationEvent)
    {
        if (integrationEvent.AuthorUserId is not { } author)
        {
            yield break;
        }

        var values = Values();

        // The count is a figure the publishing module computed and published. It is the only analytics
        // condition this module can answer, and it can answer it because it did not have to ask B19.
        values["countAbove"] = integrationEvent.TotalViews.ToString(CultureInfo.InvariantCulture);

        yield return Context(
            "guide.milestoneReached",
            integrationEvent.EventId,
            Owners(author),
            $"Your guide passed {integrationEvent.TotalViews:N0} views.",
            integrationEvent.OccurredAt,
            "guide",
            integrationEvent.GuideId,
            null,
            null,
            null,
            values);
    }
}

/// <summary>A community post was published — the nearest published fact to a community update.</summary>
public sealed class CommunityPostPublishedAutomationConsumer
    : AutomationEventConsumer<CommunityPostPublishedIntegrationEvent>
{
    public CommunityPostPublishedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        CommunityPostPublishedIntegrationEvent integrationEvent)
    {
        // A post the member could not have seen must not reach an automation. Public only.
        if (!string.Equals(integrationEvent.Visibility, "Public", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var values = Values();
        values["country"] = integrationEvent.CountryCode;
        values["category"] = integrationEvent.Category;
        values["urgency"] = integrationEvent.Urgency;
        values["neighbourhood"] = integrationEvent.AreaId?.ToString("D");

        yield return Context(
            "community.updatePublished",
            integrationEvent.EventId,
            [],
            "A new community update was posted.",
            integrationEvent.OccurredAt,
            "post",
            integrationEvent.PostId,
            null,
            null,
            null,
            values);
    }
}
