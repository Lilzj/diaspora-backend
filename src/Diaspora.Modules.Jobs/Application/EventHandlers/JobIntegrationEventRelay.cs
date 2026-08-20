using Diaspora.Contracts.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// Turns B9's domain events into the published integration events other modules consume.
/// <para>
/// <b>The one translation seam, and the reason B9's domain stays private.</b> A domain event
/// carries strongly typed identifiers and domain enums; an integration event carries flat
/// primitives, so a consumer can parse it without referencing this module. Doing the translation
/// in a single class means the two never drift, and means a consumer's contract cannot be changed
/// by a refactor inside an aggregate.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The aggregate is already saved by the time
/// this runs; throwing here would roll back a successful hire because a notification did not go
/// out. Failures are logged, and at-least-once delivery is the contract — consumers treat
/// <c>EventId</c> as the idempotency key.
/// </para>
/// <para>
/// <b>Publication and shortlisting read the aggregates back.</b> Their contracts carry the
/// employer's name, the badge and the card payload, because the consumers that matter — a search
/// index, an alert sweep, a notification about a shortlisting — need all of it and would
/// otherwise each call back into B9 to get it. The read is inline, inside the same unit of work
/// that raised the event, so it is the identity map rather than a round trip.
/// </para>
/// <para>
/// <b>B13.1 fixed three contracts that had been addressing nobody.</b> Shortlisting, interview
/// scheduling and offer extension all declared a required candidate member and all wrote
/// <c>Guid.Empty</c> into it, because the domain events those contracts are derived from are
/// raised by the interview and offer aggregates, which hold a candidate <em>profile</em> and not
/// a member. Nothing failed: the events were published, consumers parsed them, and the
/// highest-stakes notification the platform sends went to an identifier no member has. The three
/// are now resolved through <see cref="CandidateUserOfAsync"/>, and the employer side of each
/// conversation is named for the first time through <see cref="EmployerUserOfAsync"/>.
/// </para>
/// <para>
/// <b>Both resolutions are reads, and both are allowed to come back empty.</b> A null recipient
/// produces no notification, which is the correct outcome for an application or an employer that
/// no longer exists — and a far better one than failing a publish for a hire that has already
/// been saved.
/// </para>
/// </summary>
public sealed class JobIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly IJobApplicationRepository _applications;
    private readonly ILogger<JobIntegrationEventRelay> _logger;

    public JobIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        IJobApplicationRepository applications,
        ILogger<JobIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _postings = postings;
        _employers = employers;
        _applications = applications;
        _logger = logger;
    }

    public Task PublishAsync(
        JobPostingCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new JobCreatedIntegrationEvent
            {
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                CreatedBy = domainEvent.CreatedBy.Value,
                Title = domainEvent.Title,
                Slug = domainEvent.Slug,
                CategoryKey = domainEvent.CategoryKey,
                CountryCode = domainEvent.CountryCode,
                CreatedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// The module's most consequential contract. Reads the posting and employer back so the
    /// payload carries everything a card-level index needs.
    /// </summary>
    public async Task PublishAsync(
        JobPostingPublishedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var posting = await _postings.GetAsync(domainEvent.PostingId, cancellationToken);
        var employer = await _employers.GetAsync(domainEvent.EmployerId, cancellationToken);

        await PublishSafelyAsync(
            new JobPublishedIntegrationEvent
            {
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                EmployerName = employer?.Identity.DisplayName ?? string.Empty,
                IsEmployerVerified = employer?.IsVerified ?? false,
                Title = domainEvent.Title,
                Slug = domainEvent.Slug,
                Summary = posting?.Description.Summary,
                CategoryKey = domainEvent.CategoryKey,
                Tags = posting is null ? [] : [.. posting.Tags.Select(tag => tag.Slug)],
                SkillKeys = posting is null
                    ? []
                    : [.. posting.Skills.Select(skill => skill.SkillKey)],
                CountryCode = domainEvent.CountryCode,
                RegionName = posting?.Location.RegionName,
                CityName = posting?.Location.CityName,
                WorkplaceType = posting is null
                    ? nameof(WorkplaceType.OnSite)
                    : JobWireNames.ToWire(posting.WorkArrangement.WorkplaceType),
                JobType = posting is null
                    ? nameof(JobType.FullTime)
                    : JobWireNames.ToWire(posting.JobType),
                ExperienceLevel = posting is null
                    ? nameof(ExperienceLevel.Mid)
                    : JobWireNames.ToWire(posting.ExperienceLevel),
                OffersVisaSponsorship =
                    posting?.WorkArrangement.OffersVisaSponsorship ?? false,
                OffersRelocationAssistance =
                    posting?.WorkArrangement.OffersRelocationAssistance ?? false,
                IsSalaryDisclosed = posting?.Salary.IsDisclosed ?? false,
                SalaryMinimum = posting?.Salary.Minimum,
                SalaryMaximum = posting?.Salary.Maximum,
                SalaryCurrency = posting?.Salary.CurrencyCode,
                SalaryPeriod = posting?.Salary.Period is { } period
                    ? JobWireNames.ToWire(period)
                    : null,
                ClosingAt = posting?.ClosingAt,
                VacancyCount = posting?.VacancyCount ?? 1,
                IsFirstPublication = domainEvent.IsFirstPublication,
                PublishedAt = domainEvent.PublishedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        JobPostingUpdatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new JobUpdatedIntegrationEvent
            {
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedFields = domainEvent.ChangedFields,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A lifecycle transition, translated into whichever published contract it corresponds to.
    /// <para>
    /// <b>This is why the aggregate raises one status-changed event rather than nine.</b> The
    /// mapping from "moved to Filled" to "publish JobClosed with reason=filled" lives here, so
    /// the domain never learns what the wire contracts are called.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        JobPostingStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        switch (domainEvent.CurrentStatus)
        {
            case JobStatus.Filled:
                await PublishClosedAsync(domainEvent, "filled", cancellationToken);
                return;

            case JobStatus.Expired:
                await PublishClosedAsync(domainEvent, "expired", cancellationToken);
                return;

            case JobStatus.Archived:
                await PublishSafelyAsync(
                    new JobArchivedIntegrationEvent
                    {
                        JobId = domainEvent.PostingId.Value,
                        EmployerId = domainEvent.EmployerId.Value,
                        ArchivedBy = domainEvent.ChangedBy?.Value,
                        ArchivedAt = domainEvent.ChangedAt,
                        EmployerUserId =
                            await EmployerUserOfAsync(domainEvent.EmployerId, cancellationToken)
                    },
                    cancellationToken);
                return;

            default:
                // Every other transition is forward progress through hiring, which no consumer
                // subscribes to as a separate contract. The update event already reported it.
                return;
        }
    }

    public async Task PublishAsync(
        JobPostingDeletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // A deletion is a closure to every consumer that matters: de-index it.
        await PublishSafelyAsync(
            new JobClosedIntegrationEvent
            {
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                Reason = "withdrawn",
                Message = null,
                ClosedBy = null,
                ClosedAt = domainEvent.DeletedAt,
                EmployerUserId = await EmployerUserOfAsync(domainEvent.EmployerId, cancellationToken)
            },
            cancellationToken);
    }

    public Task PublishAsync(
        EmployerRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new EmployerRegisteredIntegrationEvent
            {
                EmployerId = domainEvent.EmployerId.Value,
                OwnerUserId = domainEvent.OwnerId.Value,
                DisplayName = domainEvent.DisplayName,
                Slug = domainEvent.Slug,
                OrganizationType = JobWireNames.ToWire(domainEvent.OrganizationType),
                CountryCode = domainEvent.CountryCode,
                RegisteredAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        EmployerVerificationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var employer = await _employers.GetAsync(domainEvent.EmployerId, cancellationToken);

        await PublishSafelyAsync(
            new EmployerVerifiedIntegrationEvent
            {
                EmployerId = domainEvent.EmployerId.Value,
                EmployerName = employer?.Identity.DisplayName ?? string.Empty,
                PreviousState = JobWireNames.ToWire(domainEvent.PreviousState),
                CurrentState = JobWireNames.ToWire(domainEvent.CurrentState),
                IsVerified = domainEvent.CurrentState == JobVerificationState.Verified,
                VerificationId = domainEvent.VerificationId,
                Badge = domainEvent.Badge,
                Reason = domainEvent.Reason,
                ChangedAt = domainEvent.ChangedAt,

                // The employer is already loaded above for its name. Trust decided this and the
                // owner is the one person it is about.
                EmployerUserId = employer?.NotifiableOwnerUserId.Value
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        JobApplicationSubmittedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Deliberately no profile content. An application is personal data and the bus is not
        // the place for it. The employer's member is an address, not content.
        await PublishSafelyAsync(
            new ApplicationSubmittedIntegrationEvent
            {
                ApplicationId = domainEvent.ApplicationId.Value,
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                CandidateId = domainEvent.CandidateId.Value,
                ApplicantUserId = domainEvent.ApplicantUserId.Value,
                SubmittedAt = domainEvent.SubmittedAt,
                EmployerUserId = await EmployerUserOfAsync(domainEvent.EmployerId, cancellationToken),

                // Stamped on the domain event by the aggregate, so this is a pass-through
                // rather than a second read. PSR-1 / DEBT-006.
                CategoryKey = domainEvent.CategoryKey,
                CountryCode = domainEvent.CountryCode
            },
            cancellationToken);
    }

    /// <summary>
    /// A workflow move. Publishes the general contract always, and the shortlisting contract as
    /// well for the one transition worth its own subscription.
    /// </summary>
    public async Task PublishAsync(
        JobApplicationStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Resolved once and used by both contracts below. A shortlisting publishes two events
        // about the same two people, and asking twice would be two round trips for one answer.
        var candidateUserId =
            await CandidateUserOfAsync(domainEvent.ApplicationId, cancellationToken);

        var employer = await _employers.GetAsync(domainEvent.EmployerId, cancellationToken);

        await PublishSafelyAsync(
            new ApplicationStatusChangedIntegrationEvent
            {
                ApplicationId = domainEvent.ApplicationId.Value,
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                CandidateId = domainEvent.CandidateId.Value,
                PreviousStatus = JobWireNames.ToWire(domainEvent.PreviousStatus),
                CurrentStatus = JobWireNames.ToWire(domainEvent.CurrentStatus),
                ChangedBy = domainEvent.ChangedBy?.Value,
                Reason = domainEvent.Reason,
                IsFirstEmployerResponse = domainEvent.IsFirstEmployerResponse,
                ChangedAt = domainEvent.ChangedAt,
                CandidateUserId = candidateUserId,
                EmployerUserId = employer?.NotifiableOwnerUserId.Value
            },
            cancellationToken);

        if (domainEvent.CurrentStatus != ApplicationStatus.Shortlisted)
        {
            return;
        }

        // The first genuinely good news in a job hunt, and the one moment a notification is
        // unambiguously worth sending.
        var posting = await _postings.GetAsync(domainEvent.PostingId, cancellationToken);

        await PublishSafelyAsync(
            new CandidateShortlistedIntegrationEvent
            {
                ApplicationId = domainEvent.ApplicationId.Value,
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                CandidateId = domainEvent.CandidateId.Value,

                // B13.1. This was Guid.Empty — the contract required a member and the relay had
                // no route to one, so the good news went to an identifier nobody holds.
                ApplicantUserId = candidateUserId ?? Guid.Empty,
                JobTitle = posting?.Title.Value ?? string.Empty,
                EmployerName = employer?.Identity.DisplayName ?? string.Empty,
                ShortlistedAt = domainEvent.ChangedAt,
                EmployerUserId = employer?.NotifiableOwnerUserId.Value
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        JobApplicationWithdrawnDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new ApplicationWithdrawnIntegrationEvent
            {
                ApplicationId = domainEvent.ApplicationId.Value,
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                CandidateId = domainEvent.CandidateId.Value,
                PreviousStatus = JobWireNames.ToWire(domainEvent.PreviousStatus),
                Reason = domainEvent.Reason,
                WithdrawnAt = domainEvent.WithdrawnAt,
                CandidateUserId =
                    await CandidateUserOfAsync(domainEvent.ApplicationId, cancellationToken),
                EmployerUserId = await EmployerUserOfAsync(domainEvent.EmployerId, cancellationToken)
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        InterviewScheduledDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var posting = await _postings.GetAsync(domainEvent.PostingId, cancellationToken);
        var employer = await _employers.GetAsync(domainEvent.EmployerId, cancellationToken);

        await PublishSafelyAsync(
            new InterviewScheduledIntegrationEvent
            {
                InterviewId = domainEvent.InterviewId.Value,
                ApplicationId = domainEvent.ApplicationId.Value,
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                CandidateId = domainEvent.CandidateId.Value,

                // B13.1. This was Guid.Empty, on the highest-stakes message the platform sends.
                CandidateUserId =
                    await CandidateUserOfAsync(domainEvent.ApplicationId, cancellationToken)
                    ?? Guid.Empty,
                JobTitle = posting?.Title.Value ?? string.Empty,
                EmployerName = employer?.Identity.DisplayName ?? string.Empty,
                Mode = JobWireNames.ToWire(domainEvent.Mode),
                ScheduledAt = domainEvent.ScheduledAt,
                DurationMinutes = domainEvent.DurationMinutes,
                TimeZoneId = domainEvent.TimeZoneId,
                RoundNumber = domainEvent.RoundNumber,
                IsReschedule = domainEvent.IsReschedule,
                EmployerUserId = employer?.NotifiableOwnerUserId.Value
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        OfferExtendedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var posting = await _postings.GetAsync(domainEvent.PostingId, cancellationToken);
        var employer = await _employers.GetAsync(domainEvent.EmployerId, cancellationToken);

        await PublishSafelyAsync(
            new OfferExtendedIntegrationEvent
            {
                OfferId = domainEvent.OfferId.Value,
                ApplicationId = domainEvent.ApplicationId.Value,
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                CandidateId = domainEvent.CandidateId.Value,

                // B13.1. This was Guid.Empty, on an offer somebody may move country for.
                CandidateUserId =
                    await CandidateUserOfAsync(domainEvent.ApplicationId, cancellationToken)
                    ?? Guid.Empty,
                JobTitle = posting?.Title.Value ?? string.Empty,
                EmployerName = employer?.Identity.DisplayName ?? string.Empty,
                SalaryAmount = domainEvent.SalaryAmount,
                CurrencyCode = domainEvent.CurrencyCode,
                SalaryPeriod = JobWireNames.ToWire(domainEvent.Period),
                StartDate = domainEvent.StartDate,
                ExpiresAt = domainEvent.ExpiresAt,
                ExtendedAt = domainEvent.ExtendedAt,
                EmployerUserId = employer?.NotifiableOwnerUserId.Value
            },
            cancellationToken);
    }

    /// <summary>
    /// An offer ending. Publishes the general contract always, and the acceptance contract as
    /// well — so a consumer that only cares about hires need not subscribe to every rejection
    /// and expiry on the platform to find them.
    /// </summary>
    public async Task PublishAsync(
        OfferAnsweredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Resolved once for both contracts. See the shortlisting path for the same reasoning.
        var candidateUserId =
            await CandidateUserOfAsync(domainEvent.ApplicationId, cancellationToken);

        var employerUserId = await EmployerUserOfAsync(domainEvent.EmployerId, cancellationToken);

        await PublishSafelyAsync(
            new OfferAnsweredIntegrationEvent
            {
                OfferId = domainEvent.OfferId.Value,
                ApplicationId = domainEvent.ApplicationId.Value,
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                CandidateId = domainEvent.CandidateId.Value,
                Status = JobWireNames.ToWire(domainEvent.Status),
                AnsweredBy = domainEvent.AnsweredBy?.Value,
                Reason = domainEvent.Reason,
                AnsweredAt = domainEvent.AnsweredAt,

                // Both parties, because two of the four endings — a withdrawal and an expiry —
                // are nobody's decision and are news to whoever did not cause them.
                CandidateUserId = candidateUserId,
                EmployerUserId = employerUserId
            },
            cancellationToken);

        if (domainEvent.Status != OfferStatus.Accepted)
        {
            return;
        }

        await PublishSafelyAsync(
            new OfferAcceptedIntegrationEvent
            {
                OfferId = domainEvent.OfferId.Value,
                ApplicationId = domainEvent.ApplicationId.Value,
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                CandidateId = domainEvent.CandidateId.Value,
                StartDate = null,
                TimeToHire = TimeSpan.Zero,
                AcceptedAt = domainEvent.AnsweredAt,
                CandidateUserId = candidateUserId,
                EmployerUserId = employerUserId
            },
            cancellationToken);
    }

    private async Task PublishClosedAsync(
        JobPostingStatusChangedDomainEvent domainEvent,
        string reason,
        CancellationToken cancellationToken) =>
        await PublishSafelyAsync(
            new JobClosedIntegrationEvent
            {
                JobId = domainEvent.PostingId.Value,
                EmployerId = domainEvent.EmployerId.Value,
                Reason = reason,
                Message = domainEvent.Reason,
                ClosedBy = domainEvent.ChangedBy?.Value,
                ClosedAt = domainEvent.ChangedAt,

                // Carried for the expired case: the clock closed it and nobody at the employer
                // knows unless somebody tells them.
                EmployerUserId = await EmployerUserOfAsync(domainEvent.EmployerId, cancellationToken)
            },
            cancellationToken);

    /// <summary>
    /// The member behind a candidate profile, resolved through the application. Null when the
    /// application is gone.
    /// <para>
    /// <b>The application is the only aggregate in the module that holds both.</b> An interview
    /// and an offer are keyed by application and carry a <c>CandidateProfileId</c>, which is a
    /// profile rather than a person — so a contract that has to address somebody has to come
    /// through here. It is a single projected column, not the application graph: the history is
    /// unbounded and this needs one identifier.
    /// </para>
    /// </summary>
    private async Task<Guid?> CandidateUserOfAsync(
        JobApplicationId applicationId, CancellationToken cancellationToken)
    {
        var applicant = await _applications.FindApplicantUserIdAsync(applicationId, cancellationToken);

        return applicant?.Value;
    }

    /// <summary>
    /// The employer's notifiable member. Null when the employer is gone.
    /// <para>
    /// The current longest-standing owner rather than the member recorded at registration, for
    /// the reason <c>EmployerProfile.NotifiableOwnerUserId</c> gives: after an ownership
    /// transfer, the registration field names somebody who may no longer be an owner, and a
    /// notification is delivery rather than attribution.
    /// </para>
    /// </summary>
    private async Task<Guid?> EmployerUserOfAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken)
    {
        var employer = await _employers.GetAsync(employerId, cancellationToken);

        return employer?.NotifiableOwnerUserId.Value;
    }

    private async Task PublishSafelyAsync<TEvent>(
        TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed publish must not roll back a successful write.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Publishing {EventType} failed. The write succeeded; the notification did not.",
                typeof(TEvent).Name);
        }
#pragma warning restore CA1031
    }
}
