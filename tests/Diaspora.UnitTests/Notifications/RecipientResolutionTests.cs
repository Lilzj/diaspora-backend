using Diaspora.Application.Abstractions;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Notifications.Application.Consumers;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;
using CommunityLifeConsumers = Diaspora.Modules.Notifications.Application.Consumers.CommunityLife;
using GuideConsumers = Diaspora.Modules.Notifications.Application.Consumers.Guides;
using JobConsumers = Diaspora.Modules.Notifications.Application.Consumers.Jobs;
using ProviderConsumers = Diaspora.Modules.Notifications.Application.Consumers.Providers;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// The Communication Platform resolving recipients from published contracts alone — B13.1's
/// success criterion, tested at the only place it can be observed.
/// <para>
/// <b>Every consumer here is constructed with a dispatcher that does nothing.</b> That is the
/// point: a consumer's whole job is to turn a payload into triggers, and if it needed anything
/// other than the payload to decide who to address, it could not be built this way. There is no
/// repository, no module reference and no fixture — a published event goes in and a list of
/// recipients comes out.
/// </para>
/// <para>
/// <b>The null cases are not edge cases.</b> Every one of these events exists in outbox tables in
/// its pre-B13.1 form, and every one of them can be replayed. A consumer that treated a missing
/// recipient as an identifier would create inboxes for a member that does not exist, months after
/// the fact.
/// </para>
/// </summary>
public sealed class RecipientResolutionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid Candidate = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Employer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Organizer = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Attendee = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Author = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Owner = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Reviewer = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid Editor = Guid.Parse("88888888-8888-8888-8888-888888888888");

    // ── Jobs → Communication ─────────────────────────────────────────────

    /// <summary>
    /// <b>The message B13 could not send.</b> An application produced one trigger before B13.1 —
    /// the acknowledgement to the candidate — because the payload named nobody at the employer.
    /// </summary>
    [Fact]
    public void An_application_now_addresses_the_employer_as_well_as_the_candidate()
    {
        var triggers = Translate(
            dispatcher => new JobConsumers.ApplicationSubmittedConsumer(dispatcher),
            Submitted(employerUserId: Employer));

        triggers.Should().HaveCount(2);
        triggers.Should().ContainSingle(trigger => trigger.Audience == "candidate")
            .Which.RecipientUserId.Value.Should().Be(Candidate);
        triggers.Should().ContainSingle(trigger => trigger.Audience == "employer")
            .Which.RecipientUserId.Value.Should().Be(Employer);
    }

    [Fact]
    public void An_application_replayed_without_the_employer_still_reaches_the_candidate()
    {
        var triggers = Translate(
            dispatcher => new JobConsumers.ApplicationSubmittedConsumer(dispatcher),
            Submitted(employerUserId: null));

        triggers.Should().ContainSingle()
            .Which.Audience.Should().Be(
                "candidate",
                "a historical payload must lose the new message and keep the old one");
    }

    /// <summary>
    /// The three contracts that carried <see cref="Guid.Empty"/> before B13.1. A replay of one of
    /// those must produce nothing at all — an inbox keyed on the empty identifier belongs to
    /// nobody and can never be read.
    /// </summary>
    [Fact]
    public void A_shortlisting_addressed_to_the_empty_identifier_produces_nothing()
    {
        Translate(
                dispatcher => new JobConsumers.CandidateShortlistedConsumer(dispatcher),
                Shortlisted(applicantUserId: Guid.Empty))
            .Should().BeEmpty();
    }

    [Fact]
    public void A_shortlisting_with_a_real_member_addresses_them()
    {
        Translate(
                dispatcher => new JobConsumers.CandidateShortlistedConsumer(dispatcher),
                Shortlisted(applicantUserId: Candidate))
            .Should().ContainSingle()
            .Which.RecipientUserId.Value.Should().Be(Candidate);
    }

    [Fact]
    public void An_interview_addressed_to_the_empty_identifier_produces_nothing()
    {
        Translate(
                dispatcher => new JobConsumers.InterviewScheduledConsumer(dispatcher),
                Interview(candidateUserId: Guid.Empty))
            .Should().BeEmpty();
    }

    /// <summary>
    /// And with a real member, both the confirmation and the reminder — the pair that made this
    /// defect matter more than the others.
    /// </summary>
    [Fact]
    public void An_interview_with_a_real_member_produces_the_confirmation_and_the_reminder()
    {
        var triggers = Translate(
            dispatcher => new JobConsumers.InterviewScheduledConsumer(dispatcher),
            Interview(candidateUserId: Candidate));

        triggers.Should().HaveCount(2);
        triggers.Should().OnlyContain(trigger => trigger.RecipientUserId.Value == Candidate);
        triggers.Select(trigger => trigger.Audience)
            .Should().BeEquivalentTo(["candidate", "candidate-reminder"]);
    }

    [Fact]
    public void An_offer_addressed_to_the_empty_identifier_produces_nothing()
    {
        Translate(
                dispatcher => new JobConsumers.OfferExtendedConsumer(dispatcher),
                Offer(candidateUserId: Guid.Empty))
            .Should().BeEmpty();
    }

    [Fact]
    public void An_offer_with_a_real_member_produces_the_offer_and_the_reminder()
    {
        Translate(
                dispatcher => new JobConsumers.OfferExtendedConsumer(dispatcher),
                Offer(candidateUserId: Candidate))
            .Should().HaveCount(2)
            .And.OnlyContain(trigger => trigger.RecipientUserId.Value == Candidate);
    }

    // ── Community Life → Communication ───────────────────────────────────

    [Fact]
    public void An_rsvp_now_addresses_the_organizer_as_well_as_the_attendee()
    {
        var triggers = Translate(
            dispatcher => new CommunityLifeConsumers.RsvpSubmittedConsumer(dispatcher),
            Rsvp(organizerUserId: Organizer));

        triggers.Should().HaveCount(2);
        triggers.Should().ContainSingle(trigger => trigger.Audience == "attendee")
            .Which.RecipientUserId.Value.Should().Be(Attendee);
        triggers.Should().ContainSingle(trigger => trigger.Audience == "organizer")
            .Which.RecipientUserId.Value.Should().Be(Organizer);
    }

    [Fact]
    public void An_rsvp_replayed_without_the_organizer_still_reaches_the_attendee()
    {
        Translate(
                dispatcher => new CommunityLifeConsumers.RsvpSubmittedConsumer(dispatcher),
                Rsvp(organizerUserId: null))
            .Should().ContainSingle()
            .Which.Audience.Should().Be("attendee");
    }

    /// <summary>
    /// An organizer who responds to their own event is told once, as an attendee. Telling somebody
    /// that they are coming to their own event is the kind of message that makes people turn
    /// notifications off.
    /// </summary>
    [Fact]
    public void An_organizer_responding_to_their_own_event_is_not_told_twice()
    {
        Translate(
                dispatcher => new CommunityLifeConsumers.RsvpSubmittedConsumer(dispatcher),
                Rsvp(organizerUserId: Attendee))
            .Should().ContainSingle()
            .Which.Audience.Should().Be("attendee");
    }

    // ── Guides → Communication ───────────────────────────────────────────

    /// <summary>
    /// <b>The clearest workaround removal in the phase.</b> This rule used to congratulate the
    /// staff editor who featured a guide, because the contract named nobody else.
    /// </summary>
    [Fact]
    public void Featuring_a_guide_addresses_its_author_and_not_the_editor()
    {
        var trigger = Translate(
                dispatcher => new GuideConsumers.GuideFeaturedConsumer(dispatcher),
                Featured(authorUserId: Author))
            .Should().ContainSingle().Subject;

        trigger.Audience.Should().Be("author");
        trigger.RecipientUserId.Value.Should().Be(Author);
        trigger.RecipientUserId.Value.Should().NotBe(Editor);
    }

    [Fact]
    public void Featuring_a_guide_whose_author_is_unknown_produces_nothing()
    {
        Translate(
                dispatcher => new GuideConsumers.GuideFeaturedConsumer(dispatcher),
                Featured(authorUserId: null))
            .Should().BeEmpty();
    }

    /// <summary>Unfeaturing still produces nothing, author or no author.</summary>
    [Fact]
    public void Unfeaturing_a_guide_produces_nothing()
    {
        Translate(
                dispatcher => new GuideConsumers.GuideFeaturedConsumer(dispatcher),
                Featured(authorUserId: Author) with { Featured = false })
            .Should().BeEmpty();
    }

    // ── Providers → Communication ────────────────────────────────────────

    [Fact]
    public void A_review_now_addresses_the_business_owner_as_well_as_the_reviewer()
    {
        var triggers = Translate(
            dispatcher => new ProviderConsumers.ProviderReviewReceivedConsumer(dispatcher),
            Review(ownerUserId: Owner));

        triggers.Should().HaveCount(2);
        triggers.Should().ContainSingle(trigger => trigger.Audience == "reviewer")
            .Which.RecipientUserId.Value.Should().Be(Reviewer);
        triggers.Should().ContainSingle(trigger => trigger.Audience == "owner")
            .Which.RecipientUserId.Value.Should().Be(Owner);
    }

    /// <summary>
    /// The owner's trigger carries the rating and never the review text. A one-star review quoted
    /// in a push notification is the platform delivering an insult rather than a fact.
    /// </summary>
    [Fact]
    public void The_owner_is_told_the_rating_and_not_the_words()
    {
        var trigger = Translate(
                dispatcher => new ProviderConsumers.ProviderReviewReceivedConsumer(dispatcher),
                Review(ownerUserId: Owner))
            .Single(trigger => trigger.Audience == "owner");

        trigger.Values.Should().ContainKey("rating");
        trigger.Values.Keys.Should().NotContain(key =>
            key.Contains("body", StringComparison.OrdinalIgnoreCase) && key != "hasBody");
    }

    [Fact]
    public void A_review_replayed_without_the_owner_still_reaches_the_reviewer()
    {
        Translate(
                dispatcher => new ProviderConsumers.ProviderReviewReceivedConsumer(dispatcher),
                Review(ownerUserId: null))
            .Should().ContainSingle()
            .Which.Audience.Should().Be("reviewer");
    }

    [Fact]
    public void A_recommendation_now_addresses_the_business_owner_too()
    {
        Translate(
                dispatcher => new ProviderConsumers.ProviderRecommendationReceivedConsumer(dispatcher),
                Recommendation(ownerUserId: Owner))
            .Should().HaveCount(2)
            .And.ContainSingle(trigger => trigger.Audience == "owner");
    }

    [Fact]
    public void A_recommendation_replayed_without_the_owner_still_reaches_the_recommender()
    {
        Translate(
                dispatcher => new ProviderConsumers.ProviderRecommendationReceivedConsumer(dispatcher),
                Recommendation(ownerUserId: null))
            .Should().ContainSingle()
            .Which.Audience.Should().Be("recommender");
    }

    // ── Fixtures ─────────────────────────────────────────────────────────

    private static ApplicationSubmittedIntegrationEvent Submitted(Guid? employerUserId) => new()
    {
        ApplicationId = Guid.NewGuid(),
        JobId = Guid.NewGuid(),
        EmployerId = Guid.NewGuid(),
        CandidateId = Guid.NewGuid(),
        ApplicantUserId = Candidate,
        SubmittedAt = Now,
        EmployerUserId = employerUserId
    };

    private static CandidateShortlistedIntegrationEvent Shortlisted(Guid applicantUserId) => new()
    {
        ApplicationId = Guid.NewGuid(),
        JobId = Guid.NewGuid(),
        EmployerId = Guid.NewGuid(),
        CandidateId = Guid.NewGuid(),
        ApplicantUserId = applicantUserId,
        JobTitle = "Senior React Native Developer",
        EmployerName = "Kite Labs",
        ShortlistedAt = Now,
        EmployerUserId = Employer
    };

    private static InterviewScheduledIntegrationEvent Interview(Guid candidateUserId) => new()
    {
        InterviewId = Guid.NewGuid(),
        ApplicationId = Guid.NewGuid(),
        JobId = Guid.NewGuid(),
        EmployerId = Guid.NewGuid(),
        CandidateId = Guid.NewGuid(),
        CandidateUserId = candidateUserId,
        JobTitle = "Senior React Native Developer",
        EmployerName = "Kite Labs",
        Mode = "Video",
        ScheduledAt = Now.AddDays(3),
        DurationMinutes = 45,
        TimeZoneId = "Europe/Berlin",
        RoundNumber = 1,
        IsReschedule = false,
        EmployerUserId = Employer
    };

    private static OfferExtendedIntegrationEvent Offer(Guid candidateUserId) => new()
    {
        OfferId = Guid.NewGuid(),
        ApplicationId = Guid.NewGuid(),
        JobId = Guid.NewGuid(),
        EmployerId = Guid.NewGuid(),
        CandidateId = Guid.NewGuid(),
        CandidateUserId = candidateUserId,
        JobTitle = "Senior React Native Developer",
        EmployerName = "Kite Labs",
        SalaryAmount = 65_000m,
        CurrencyCode = "EUR",
        SalaryPeriod = "Yearly",
        ExpiresAt = Now.AddDays(7),
        ExtendedAt = Now,
        EmployerUserId = Employer
    };

    private static RsvpSubmittedIntegrationEvent Rsvp(Guid? organizerUserId) => new()
    {
        RsvpId = Guid.NewGuid(),
        CommunityEventId = Guid.NewGuid(),
        AttendeeUserId = Attendee,
        Status = "Going",
        PartySize = 2,
        GoingCount = 48,
        RemainingPlaces = 12,
        RespondedAt = Now,
        OrganizerUserId = organizerUserId
    };

    private static GuideFeaturedIntegrationEvent Featured(Guid? authorUserId) => new()
    {
        GuideId = Guid.NewGuid(),
        Featured = true,
        ChangedBy = Editor,
        ChangedAt = Now,
        AuthorUserId = authorUserId
    };

    private static ProviderReviewReceivedIntegrationEvent Review(Guid? ownerUserId) => new()
    {
        ReviewId = Guid.NewGuid(),
        ProviderId = Guid.NewGuid(),
        ReviewerId = Reviewer,
        Rating = 5,
        HasBody = true,
        ReceivedAt = Now,
        OwnerUserId = ownerUserId
    };

    private static ProviderRecommendationReceivedIntegrationEvent Recommendation(Guid? ownerUserId) =>
        new()
        {
            RecommendationId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            RecommenderId = Reviewer,
            Kind = "Recommend",
            ReceivedAt = Now,
            OwnerUserId = ownerUserId
        };

    /// <summary>
    /// Builds a consumer around a recording dispatcher, runs the event through it, and returns
    /// the triggers.
    /// <para>
    /// Driven through <c>Handle</c> — the message-bus entry point — rather than through the
    /// protected translation method, so a consumer that produced triggers and failed to dispatch
    /// them would still fail here.
    /// </para>
    /// </summary>
    private static IReadOnlyList<NotificationTrigger> Translate<TEvent>(
        Func<INotificationDispatcher, ModuleEventConsumer<TEvent>> build, TEvent integrationEvent)
        where TEvent : Diaspora.Contracts.Events.IIntegrationEvent
    {
        var dispatcher = new RecordingDispatcher();

        build(dispatcher)
            .Handle(
                new IntegrationEventNotification<TEvent>(integrationEvent),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return dispatcher.Triggers;
    }

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        private readonly List<NotificationTrigger> _triggers = [];

        public IReadOnlyList<NotificationTrigger> Triggers => _triggers;

        public Task<Result<NotificationDispatchResult>> DispatchAsync(
            NotificationTrigger trigger, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(trigger);

            _triggers.Add(trigger);

            return Task.FromResult(
                Result.Success(NotificationDispatchResult.Nothing(trigger.RecipientUserId)));
        }
    }
}
