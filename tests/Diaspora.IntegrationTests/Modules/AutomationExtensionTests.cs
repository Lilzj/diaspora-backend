using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.CommunityProgramme.Extensibility.Automation;
using Diaspora.Platform.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B23.x Wave 12 — the triggers, conditions and actions the foundation left open.
///
/// <para><b>Every trigger here is driven by a real published contract through the real outbox.</b>
/// Nothing writes a run, a horizon row or a resolved condition value directly: a suite that did would
/// be asserting its own fixture reads back, which is what Wave 4's severest finding was and what the
/// B23 suite's own header records.</para>
///
/// <para><b>Both sides of every predicate.</b> A price edit that moved the price and one that did
/// not; an enquiry from a stranger and one the member sent themselves; a horizon that is armed and
/// one that is cancelled. A query asserted only against zero rows is unproved.</para>
/// </summary>
public sealed class AutomationExtensionTests : IdentityTestBase
{
    private const string Automations = "/api/v1/automations";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AutomationExtensionTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // W12-GAP-001 — savedSearch.priceChanged, over the contract Wave 11 said did not exist.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A listing whose price moved runs the rule; one whose caption moved does not.
    /// <para>
    /// The second half is the whole reason the publishing contract carries a changed-field list. A
    /// consumer that could only see "something changed" would notify on every typo, and a member
    /// watching a price would learn to ignore it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_price_that_moved_runs_the_rule_and_a_caption_edit_does_not()
    {
        await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Tell me when the price drops",
            trigger: "savedSearch.priceChanged",
            activate: true);

        // The negative half first, so a rule that fires on everything cannot pass by accident.
        await PublishAsync(new ListingUpdatedIntegrationEvent
        {
            ListingId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            ChangedFields = ["title", "description"],
            ChangedAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();

        var afterCaption = await RunsForAsync(automation.Id);
        afterCaption.Items.Should().BeEmpty("a caption edit is not a price change");

        await PublishAsync(new ListingUpdatedIntegrationEvent
        {
            ListingId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            ChangedFields = ["price"],
            PriceAmount = 420m,
            PreviousPriceAmount = 500m,
            CurrencyCode = "GBP",
            ChangedAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();

        var afterPrice = await RunsForAsync(automation.Id);
        var run = afterPrice.Items.Should().ContainSingle().Subject;

        run.TriggerSummary.Should().Contain("dropped", "the payload said what direction it moved");
        run.ActionOutcomes.Should().ContainSingle();
    }

    /// <summary>
    /// The housing half of the same trigger, over Wave 12's own contract.
    /// <para>
    /// <b>An undisclosed rent produces nothing</b>, which is the guard that keeps the platform from
    /// reporting a number the landlord chose not to publish.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_rent_that_moved_runs_the_rule_only_when_the_landlord_disclosed_it()
    {
        await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Tell me when the rent drops",
            trigger: "savedSearch.priceChanged",
            activate: true);

        await PublishAsync(new HousingUpdatedIntegrationEvent
        {
            ListingId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            ChangedFields = ["Price"],
            IsRentDisclosed = false,
            RentAmount = null,
            ChangedAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();
        (await RunsForAsync(automation.Id)).Items
            .Should().BeEmpty("an undisclosed rent is not a figure the platform may report");

        await PublishAsync(new HousingUpdatedIntegrationEvent
        {
            ListingId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            ChangedFields = ["Price"],
            IsRentDisclosed = true,
            RentAmount = 1_200m,
            CurrencyCode = "GBP",
            CountryCode = "GB",
            ChangedAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();
        (await RunsForAsync(automation.Id)).Items.Should().ContainSingle();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // W12-GAP-006 · 007 · 008 — the three enquiry triggers, over one Conversations contract.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A buyer's enquiry runs the seller's rule and never the buyer's.
    /// <para>
    /// The second half is the property that makes the trigger usable at all: the initiator's own
    /// automation must not fire on their own message, or a member who wrote "tell me when I get an
    /// enquiry" would be told about every enquiry they sent.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_enquiry_runs_the_recipients_rule_and_never_the_senders()
    {
        var (seller, sellerToken) = await NewMemberAsync();

        var sellerRule = await CreateAutomationAsync(
            "Tell me about enquiries",
            trigger: "listing.inquiryReceived",
            ownerKind: "seller",
            activate: true);

        var (buyer, buyerToken) = await NewMemberAsync();

        var buyerRule = await CreateAutomationAsync(
            "Tell me about enquiries too",
            trigger: "listing.inquiryReceived",
            ownerKind: "seller",
            activate: true);

        await PublishAsync(new ConversationCreatedIntegrationEvent
        {
            ConversationId = Guid.NewGuid(),
            ContextType = "MarketplaceListing",
            EntityId = Guid.NewGuid().ToString("D"),
            InitiatorUserId = buyer,
            ParticipantUserIds = [buyer, seller],
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();

        // History is owner-filtered, so each rule is read as the member who owns it. Reading both as
        // one caller would prove the filter rather than the trigger.
        Api.Authenticate(sellerToken);
        var sellerRuns = await RunsForAsync(sellerRule.Id);
        sellerRuns.Items.Should().ContainSingle("the seller received the enquiry");

        sellerRuns.Items[0].TriggerSummary.Should().NotContain(
            buyer.ToString(), "who it is from stays in the member's inbox");

        Api.Authenticate(buyerToken);
        var buyerRuns = await RunsForAsync(buyerRule.Id);
        buyerRuns.Items.Should().BeEmpty("the buyer sent it; it is not an enquiry they received");
    }

    /// <summary>
    /// A pending message request produces nothing.
    /// <para>
    /// Firing on one would route an automation around the acceptance gate the context type exists to
    /// impose.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_conversation_still_awaiting_acceptance_produces_no_enquiry_run()
    {
        var landlord = await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Tell me about enquiries",
            trigger: "housing.inquiryReceived",
            ownerKind: "landlord",
            activate: true);

        await PublishAsync(new ConversationCreatedIntegrationEvent
        {
            ConversationId = Guid.NewGuid(),
            ContextType = "HousingListing",
            EntityId = Guid.NewGuid().ToString("D"),
            InitiatorUserId = Guid.NewGuid(),
            ParticipantUserIds = [Guid.NewGuid(), landlord],
            Status = "PendingRequest",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();

        (await RunsForAsync(automation.Id)).Items.Should().BeEmpty(
            "a request nobody has accepted is not yet a conversation");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // W12-GAP-002 · 003 · 004 · 005 — the dated-subject store.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A publication with a closing date is remembered, and a republication does not remember it twice.
    /// <para>
    /// The uniqueness is a database constraint on <c>(TriggerKey, SubjectId)</c> rather than a read,
    /// because two instances consuming a redelivered message would both pass a read.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_closing_date_is_remembered_once_however_many_times_it_is_published()
    {
        await NewVerifiedMemberAsync();

        var jobId = Guid.NewGuid();
        var closingAt = DateTimeOffset.UtcNow.AddDays(30);

        // Two publications naming one subject — a republished posting, which is what an employer
        // correcting a typo produces and what a redelivery looks like to the horizon store. Sending
        // one instance twice is not the test: an outbox row carries its own identity.
        await PublishAsync(JobPublication(jobId, closingAt));
        await PublishAsync(JobPublication(jobId, closingAt));
        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();
        var horizons = scope.ServiceProvider.GetRequiredService<IAutomationSubjectHorizonRepository>();

        var found = await horizons.FindAsync("job.closingSoon", jobId);

        found.Should().NotBeNull("a published closing date is a moment to come back to");
        found!.SubjectKind.Should().Be(AutomationSubjectKinds.Job);
        found.DueAt.Should().BeBefore(closingAt, "the platform comes back before the date, not on it");

        var everything = await horizons.ListForSubjectAsync(jobId);
        everything.Should().ContainSingle("a republication must not arm a second horizon");
    }

    /// <summary>
    /// A posting that closed early cancels the horizon rather than leaving it to fire.
    /// <para>
    /// The cancellation half of every horizon. Without it the platform reminds a member about
    /// something that is no longer going to happen, which is worse than not reminding them at all.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_subject_that_goes_away_cancels_its_horizon()
    {
        await NewVerifiedMemberAsync();

        var jobId = Guid.NewGuid();

        await PublishAsync(JobPublication(jobId, DateTimeOffset.UtcNow.AddDays(30)));
        await Factory.DrainOutboxAsync();

        using (var armed = Factory.Services.CreateScope())
        {
            var horizons = armed.ServiceProvider
                .GetRequiredService<IAutomationSubjectHorizonRepository>();

            (await horizons.ListForSubjectAsync(jobId)).Should().ContainSingle(
                "the horizon is armed before the posting closes");
        }

        await PublishAsync(new JobClosedIntegrationEvent
        {
            JobId = jobId,
            EmployerId = Guid.NewGuid(),
            Reason = "filled",
            ClosedAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IAutomationSubjectHorizonRepository>();

        (await repository.ListForSubjectAsync(jobId)).Should().BeEmpty(
            "a closed posting has nothing left to warn about");
    }

    /// <summary>
    /// A closing date already inside the notice period arms nothing.
    /// <para>
    /// The frontend's wording for all four horizon triggers is "a set time before". Firing now would
    /// tell somebody a job closes "soon" when it closes in an hour, or has already closed.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_date_inside_the_notice_period_arms_nothing()
    {
        await NewVerifiedMemberAsync();

        var jobId = Guid.NewGuid();

        await PublishAsync(JobPublication(jobId, DateTimeOffset.UtcNow.AddHours(2)));
        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();
        var horizons = scope.ServiceProvider
            .GetRequiredService<IAutomationSubjectHorizonRepository>();

        (await horizons.ListForSubjectAsync(jobId)).Should().BeEmpty(
            "there is no longer time to give the member the notice the trigger promises");
    }

    /// <summary>
    /// A viewing appointment is remembered against the seeker, and the landlord is not named.
    /// <para>
    /// The frontend scopes <c>housing.viewingReminder</c> to <c>user</c> and describes it as "I have
    /// a viewing coming up". The landlord arranged it; reminding them of their own diary is a
    /// different feature nobody asked for.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_viewing_is_remembered_against_the_seeker_alone()
    {
        var seeker = await NewVerifiedMemberAsync();

        var viewingId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();

        await PublishAsync(new ViewingScheduledIntegrationEvent
        {
            ViewingId = viewingId,
            ListingId = Guid.NewGuid(),
            LandlordId = landlordId,
            SeekerUserId = seeker,
            ScheduledFor = DateTimeOffset.UtcNow.AddDays(5),
            DurationMinutes = 30,
            IsVirtual = false,
            ScheduledAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();
        var horizons = scope.ServiceProvider
            .GetRequiredService<IAutomationSubjectHorizonRepository>();

        var horizon = (await horizons.ListForSubjectAsync(viewingId)).Should().ContainSingle().Subject;

        horizon.TriggerKey.Should().Be("housing.viewingReminder");
        horizon.Owners().Should().ContainSingle().Which.Should().Be(seeker);
        horizon.Owners().Should().NotContain(landlordId, "the landlord arranged it");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // W12-GAP-009 — a registration read from the organization's side.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One RSVP produces the attendee's run and the organization's, and they are two runs.
    /// <para>
    /// Two triggers over one contract, from one consumer — the shape DSP1003 requires, because two
    /// consumers over one contract would be two settlement owners inside one module.
    /// </para>
    /// </summary>
    [Fact]
    public async Task One_registration_produces_the_attendees_run_and_the_organizations()
    {
        var (attendee, attendeeToken) = await NewMemberAsync();

        var attendeeRule = await CreateAutomationAsync(
            "When I register",
            trigger: "event.rsvpConfirmed",
            activate: true);

        // A real organization, created through its own route, so its creator is a real administrator
        // in B12's own membership table. The scope authority reads that table, which is why the test
        // below — a fabricated identifier — is refused.
        var (organizer, organizerToken) = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync();

        // `notify.organization`, the broadcast Wave 11 shipped granted to nobody. Creating and
        // activating it at all is the assertion that B12's new authority grants
        // `automations.organization` to an administrator — the creator of the body. (Since 11 Sep 2026
        // an organization's automation may also carry `notify.self`, which tells the member who wrote
        // it; this test is about the grant, so it keeps the action only the grant unlocks.)
        var organizationRule = await CreateAutomationAsync(
            "When somebody registers for ours",
            trigger: "organization.registrationReceived",
            action: "notify.organization",
            ownerKind: "organization",
            ownerId: organizationId,
            activate: true);

        await PublishAsync(new RsvpSubmittedIntegrationEvent
        {
            RsvpId = Guid.NewGuid(),
            CommunityEventId = Guid.NewGuid(),
            OrganizationId = organizationId,
            AttendeeUserId = attendee,
            Status = "Going",
            PartySize = 2,
            GoingCount = 41,
            RespondedAt = DateTimeOffset.UtcNow,
            OrganizerUserId = organizer,
            EventStartsAt = DateTimeOffset.UtcNow.AddDays(10),
            EventTitle = "Lagos Supper Club"
        });

        await Factory.DrainOutboxAsync();

        Api.Authenticate(attendeeToken);
        (await RunsForAsync(attendeeRule.Id)).Items
            .Should().ContainSingle("the attendee registered");

        Api.Authenticate(organizerToken);
        var organizationRuns = await RunsForAsync(organizationRule.Id);
        var run = organizationRuns.Items.Should().ContainSingle().Subject;

        run.TriggerSummary.Should().Contain("Lagos Supper Club");
        run.TriggerSummary.Should().NotContain(
            attendee.ToString(),
            "a broadcast about somebody registering must not carry who registered");
    }

    /// <summary>
    /// Somebody with no role in an organization may not automate for it.
    /// <para>
    /// <b>The half of the new authority that matters most.</b> Wave 11 granted
    /// <c>automations.organization</c> to nobody, which was safe by being useless; Wave 12 grants it,
    /// and what must stay true is that it is granted to the right people. A fabricated organization
    /// identifier is the shape an IDOR takes here.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_stranger_may_not_create_an_automation_for_an_organization()
    {
        await NewVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            Automations,
            new
            {
                name = "Not mine to automate",
                trigger = new { key = "organization.registrationReceived" },
                actions = new[] { new { key = "notify.self" } },
                status = "DRAFT",
                scope = new { ownerKind = "organization", ownerId = Guid.NewGuid() },
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        response.IsSuccessStatusCode.Should().BeFalse(
            "a member with no role in an organization may not automate for it");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // W12-GAP-014 … 018 — the conditions no payload may answer.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A trigger payload can never satisfy an externally-sourced condition.
    /// <para>
    /// <b>The security property behind the whole read-port design.</b> The consumer that publishes a
    /// listing update writes a values bag; if <c>walletBalance</c> were read from it, any module that
    /// publishes an event could decide whether a member's balance condition matched. This asserts the
    /// separation end to end rather than at unit level, because the separation lives in the shape of
    /// the context the engine builds.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_balance_condition_is_never_satisfied_by_a_trigger_payload()
    {
        var member = await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Only when I am flush",
            trigger: "job.applicationStatusChanged",
            activate: true,
            conditions:
            [
                new { key = "walletBalance", @operator = "greaterThan", value = "1" }
            ]);

        await PublishAsync(new ApplicationStatusChangedIntegrationEvent
        {
            ApplicationId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            EmployerId = Guid.NewGuid(),
            CandidateId = Guid.NewGuid(),
            PreviousStatus = "Submitted",
            CurrentStatus = "Shortlisted",
            IsFirstEmployerResponse = true,
            ChangedAt = DateTimeOffset.UtcNow,
            CandidateUserId = member
        });

        await Factory.DrainOutboxAsync();

        var run = (await RunsForAsync(automation.Id)).Items.Should().ContainSingle().Subject;

        // The member has no wallet in this host, so the Financial source omits the key and the
        // condition fails closed. What must never happen is a match — and it must not happen whether
        // the source answered nothing or a payload carried a number.
        run.Status.Should().Be("SKIPPED");
        run.ConditionOutcomes.Should().ContainSingle();
        run.ConditionOutcomes[0].Matched.Should().BeFalse(
            "an unanswerable condition must never let a run through");

        run.ActionOutcomes.Should().BeEmpty("nothing was attempted");
    }

    /// <summary>
    /// The three read ports are registered by their owning modules, not by the automation platform.
    /// <para>
    /// Resolved from the host's own container, so a module that stopped registering one — or a seam
    /// moved back somewhere no module can reference — fails here rather than at the first run whose
    /// condition silently starts failing closed.
    /// </para>
    /// </summary>
    [Fact]
    public void The_owning_modules_answer_the_conditions_the_platform_cannot()
    {
        using var scope = Factory.Services.CreateScope();

        var sources = scope.ServiceProvider
            .GetServices<IAutomationConditionSource>()
            .ToList();

        sources.SelectMany(source => source.Answers)
            .Should().Contain(
            [
                AutomationConditionKeys.WalletBalance,
                AutomationConditionKeys.SubscriptionActive,
                AutomationConditionKeys.CreatorStatus,
                AutomationConditionKeys.MetricAbove,
                AutomationConditionKeys.MetricBelow
            ],
            "every condition the catalogue marks as externally sourced has an owner");

        // No two sources claim one key: two opinions about a balance is the same problem two
        // capability sets over one community is.
        sources.SelectMany(source => source.Answers)
            .Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// The organization, creator and community scopes are answered by their owning modules.
    /// <para>
    /// Wave 11 shipped both broadcasts working and granted to nobody, because the seam lived in an
    /// assembly no owning module could reference. This is the assertion that the move made it
    /// reachable — and it names the owner kinds, so a module answering for everything cannot pass.
    /// </para>
    /// <para>
    /// <b>It used to assert that nothing answered for a community</b>, because the Community Directory
    /// had no backend and an authority for it would have been a module claiming a scope it did not
    /// own. Communities have had owners and administrators since 11 Sep 2026, and the module that
    /// records them answers for the scope; the assertion failed from that day until 17 Sep 2026.
    /// It now pins the owner instead.
    /// </para>
    /// </summary>
    [Fact]
    public void The_owning_modules_answer_for_the_scopes_the_platform_cannot()
    {
        using var scope = Factory.Services.CreateScope();

        var authorities = scope.ServiceProvider
            .GetServices<IAutomationScopeAuthority>()
            .ToList();

        var everyKind = Enum.GetValues<AutomationOwnerKind>().Length;

        var specific = authorities
            .Where(authority => authority.Handles.Count < everyKind)
            .SelectMany(authority => authority.Handles)
            .ToList();

        specific.Should().Contain(AutomationOwnerKind.Organization);
        specific.Should().Contain(AutomationOwnerKind.Creator);

        authorities
            .Where(authority => authority.Handles.Count < everyKind
                && authority.Handles.Contains(AutomationOwnerKind.Community))
            .Should().ContainSingle()
            .Which.Should().BeOfType<CommunityAutomationAuthority>(
                "the module that records who runs a community is the one that answers for it");

        specific.Should().OnlyHaveUniqueItems(
            "two authorities for one owner kind would be two opinions about who may act there");

        authorities.Should().Contain(
            authority => authority.Handles.Count == everyKind,
            "the deny-by-default floor stays registered underneath");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<Guid> NewVerifiedMemberAsync() => (await NewMemberAsync()).Id;

    /// <summary>
    /// A verified member, and the token to become them again.
    /// <para>
    /// Execution history is owner-filtered, so a test with two members has to read each one's runs as
    /// that member. Returning the token is what makes that possible without a second registration.
    /// </para>
    /// </summary>
    private async Task<(Guid Id, string Token)> NewMemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        return (auth.User.Id, auth.Tokens.AccessToken);
    }

    private async Task<Guid> CreateOrganizationAsync()
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/community-life/organizations",
            new
            {
                name = $"Association {Guid.NewGuid():N}",
                summary = "A body created by the integration suite.",
                organizationTypeKey = "cultural-association",
                countryCode = "GB",
                cityName = "London"
            },
            JsonOptions);

        return (await ReadAsync<OrganizationDto>(response)).OrganizationId;
    }

    private static JobPublishedIntegrationEvent JobPublication(
        Guid jobId, DateTimeOffset closingAt) => new()
    {
        JobId = jobId,
        EmployerId = Guid.NewGuid(),
        EmployerName = "An employer",
        IsEmployerVerified = true,
        Title = "Software engineer",
        Slug = "software-engineer",
        CategoryKey = "technology",
        Tags = [],
        SkillKeys = [],
        CountryCode = "GB",
        WorkplaceType = "Remote",
        JobType = "FullTime",
        ExperienceLevel = "Mid",
        OffersVisaSponsorship = false,
        OffersRelocationAssistance = false,
        IsSalaryDisclosed = false,
        ClosingAt = closingAt,
        VacancyCount = 1,
        IsFirstPublication = true,
        PublishedAt = DateTimeOffset.UtcNow
    };

    private async Task PublishAsync<T>(T integrationEvent)
        where T : Diaspora.Contracts.Events.IIntegrationEvent
    {
        using var scope = Factory.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<IIntegrationEventPublisher>()
            .PublishAsync(integrationEvent);

        await scope.ServiceProvider
            .GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>()
            .SaveChangesAsync();
    }

    private async Task<PageDto<RunDto>> RunsForAsync(string automationId) =>
        await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?automationId={automationId}"));

    private async Task<AutomationDto> CreateAutomationAsync(
        string name,
        string trigger = "manual.run",
        string action = "notify.self",
        bool activate = false,
        object[]? conditions = null,
        string? ownerKind = null,
        Guid? ownerId = null)
    {
        var response = await HttpClient.PostAsJsonAsync(
            Automations,
            new
            {
                name,
                trigger = new { key = trigger },
                conditions = conditions ?? [],
                actions = new[] { new { key = action } },
                status = activate ? "ACTIVE" : "DRAFT",

                // The owner travels under `scope`, matching the client's own request shape: an
                // automation carries a scope and the server derives who it reaches from it.
                scope = ownerKind is null ? null : new { ownerKind, ownerId },
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        return await ReadAsync<AutomationDto>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded; body was {await response.Content.ReadAsStringAsync()}");

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        envelope.Should().NotBeNull();
        return envelope!.Data;
    }

    private sealed record Envelope<T>(T Data);

    private sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor, int? TotalCount);

    private sealed record AutomationDto(string Id, string Name, string Status);

    private sealed record OrganizationDto(Guid OrganizationId);

    private sealed record RunDto(
        string Id,
        string AutomationId,
        string Status,
        string TriggerSummary,
        string? FailureKind,
        IReadOnlyList<ConditionOutcomeDto> ConditionOutcomes,
        IReadOnlyList<ActionOutcomeDto> ActionOutcomes);

    private sealed record ConditionOutcomeDto(bool Matched, string EvaluatedBy, string Explanation);

    private sealed record ActionOutcomeDto(string ActionKey, string Status, string? SuppressionReason);
}
