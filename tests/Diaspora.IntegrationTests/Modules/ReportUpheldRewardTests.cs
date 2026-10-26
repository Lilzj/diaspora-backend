using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Primitives;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CommunityHandlers = Diaspora.Modules.Community.Application.EventHandlers;
using CommunityLifeHandlers = Diaspora.Modules.CommunityLife.Application.EventHandlers;
using HousingHandlers = Diaspora.Modules.Housing.Application.EventHandlers;
using JobsHandlers = Diaspora.Modules.Jobs.Application.EventHandlers;
using MarketplaceHandlers = Diaspora.Modules.Marketplace.Application.EventHandlers;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// An upheld report in any of the five reporting modules pays its reporter (REP-13, step 3e).
/// <para>
/// The unit tests prove each report says it was upheld and that Trust pays for it. This proves the
/// joins: that each module's handler is one the host registers, that its relay publishes the shared
/// contract through the outbox, and that Trust's consumer saves the reporter's award. The Jobs
/// moderation tests prove the same through a moderator's real decision.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ReportUpheldRewardTests : PooledIdentityTestBase
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public ReportUpheldRewardTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task An_upheld_community_report_pays_its_reporter()
    {
        var reporter = await MemberAsync();
        var reportId = ContentReportId.New();

        await RaiseAsync<CommunityHandlers.ContentReportUpheldEventHandler, ContentReportUpheldDomainEvent>(new()
        {
            ReportId = reportId,
            Content = ContentReference.Create(CommunityContentType.Post, Guid.NewGuid()),
            ReporterId = reporter,
            ContentAuthorId = Guid.NewGuid(),
            UpheldAt = Now
        });

        await ShouldBePaidAsync(reporter, "community", reportId.Value);
    }

    [Fact]
    public async Task An_upheld_community_life_report_pays_its_reporter()
    {
        var reporter = await MemberAsync();
        var reportId = CommunityLifeReportId.New();

        await RaiseAsync<CommunityLifeHandlers.CommunityLifeReportUpheldEventHandler, CommunityLifeReportUpheldDomainEvent>(new()
        {
            ReportId = reportId,
            SubjectType = ReportSubjectType.Event,
            SubjectId = Guid.NewGuid(),
            ReportedByUserId = reporter,
            UpheldAt = Now
        });

        await ShouldBePaidAsync(reporter, "community-life", reportId.Value);
    }

    [Fact]
    public async Task An_upheld_housing_report_pays_its_reporter()
    {
        var reporter = await MemberAsync();
        var reportId = HousingReportId.New();

        await RaiseAsync<HousingHandlers.HousingReportUpheldRelayEventHandler, HousingReportUpheldDomainEvent>(new()
        {
            ReportId = reportId,
            ListingId = HousingListingId.New(),
            LandlordId = LandlordProfileId.New(),
            ReportedBy = reporter,
            UpheldAt = Now
        });

        await ShouldBePaidAsync(reporter, "housing", reportId.Value);
    }

    [Fact]
    public async Task An_upheld_job_report_pays_its_reporter()
    {
        var reporter = await MemberAsync();
        var reportId = JobReportId.New();

        await RaiseAsync<JobsHandlers.JobReportUpheldEventHandler, JobReportUpheldDomainEvent>(new()
        {
            ReportId = reportId,
            PostingId = JobPostingId.New(),
            EmployerId = EmployerProfileId.New(),
            ReportedBy = reporter,
            UpheldAt = Now
        });

        await ShouldBePaidAsync(reporter, "jobs", reportId.Value);
    }

    [Fact]
    public async Task An_actioned_marketplace_report_pays_its_reporter_and_a_dismissed_one_does_not()
    {
        var reporter = await MemberAsync();
        var actioned = ListingReportId.New();

        await RaiseAsync<MarketplaceHandlers.ListingReportUpheldEventHandler, ListingReportResolvedDomainEvent>(
            Resolved(actioned, ListingReportStatus.Actioned, reporter));
        await RaiseAsync<MarketplaceHandlers.ListingReportUpheldEventHandler, ListingReportResolvedDomainEvent>(
            Resolved(ListingReportId.New(), ListingReportStatus.Dismissed, reporter));

        await ShouldBePaidAsync(reporter, "marketplace", actioned.Value);
    }

    private static ListingReportResolvedDomainEvent Resolved(
        ListingReportId reportId, ListingReportStatus status, UserId reporter) => new()
    {
        ReportId = reportId,
        ListingId = MarketplaceListingId.New(),
        Status = status,
        ResolvedBy = UserId.From(Guid.NewGuid()),
        ResolvedAt = Now,
        ReporterId = reporter,
        SellerId = SellerProfileId.New()
    };

    private async Task<UserId> MemberAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        return UserId.From(auth.User.Id);
    }

    /// <summary>
    /// Raises the domain event through the handler the host registered for it, as the module's own
    /// dispatch would after its save, then delivers what that enqueued.
    /// </summary>
    private async Task RaiseAsync<THandler, TEvent>(TEvent domainEvent)
        where THandler : DomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider
                .GetServices<INotificationHandler<DomainEventNotification<TEvent>>>()
                .OfType<THandler>()
                .Should().ContainSingle("the module registers {0}", typeof(THandler).Name).Subject;

            await handler.Handle(new DomainEventNotification<TEvent>(domainEvent), CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await Factory.DrainOutboxAsync();
    }

    private async Task ShouldBePaidAsync(UserId reporter, string module, Guid reportId)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(reporter, new PaginationRequest { PageSize = 50 });

        var award = page.Items.Should()
            .ContainSingle(item => item.ActionType == ReputationActions.ReportResolved).Subject;

        award.Points.Should().Be(5);
        award.AwardKey.Should().Be(ReputationAwardKey.Report(module, reportId).Value);
    }
}
