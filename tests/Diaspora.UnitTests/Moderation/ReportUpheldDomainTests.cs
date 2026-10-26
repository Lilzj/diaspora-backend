using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Reports;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Reports;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Moderation;

/// <summary>
/// Each of the five reporting modules says when a report is upheld, and only then (REP-13).
/// <para>
/// Reports were decided in Community, CommunityLife, Housing, Jobs and Marketplace, and none said
/// how: the reporter whose report a moderator agreed with was paid nothing, because nothing outside
/// the module could know. Each report now raises its module's "upheld" event from <c>Resolve</c>,
/// carrying the reporter, and a dismissed report raises nothing.
/// </para>
/// </summary>
public sealed class ReportUpheldDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Reporter = UserId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private static readonly UserId Moderator = UserId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

    [Fact]
    public void A_community_report_upheld_names_its_reporter_and_the_content_author()
    {
        var author = Guid.NewGuid();
        var content = ContentReference.Create(CommunityContentType.Post, Guid.NewGuid());
        var report = ContentReport.File(content, Reporter, ReportReasonCode.Spam, null, null, Now, author).Value;

        report.Resolve(Moderator.Value, "Spam.", upheld: true, Now);

        var upheld = report.DomainEvents.OfType<ContentReportUpheldDomainEvent>().Should().ContainSingle().Subject;
        upheld.ReportId.Should().Be(report.Id);
        upheld.Content.Should().Be(content);
        upheld.ReporterId.Should().Be(Reporter);
        upheld.ContentAuthorId.Should().Be(author);
    }

    [Fact]
    public void A_community_report_dismissed_says_nothing()
    {
        var report = ContentReport.File(
            ContentReference.Create(CommunityContentType.Post, Guid.NewGuid()), Reporter,
            ReportReasonCode.Spam, null, null, Now).Value;

        report.Resolve(Moderator.Value, null, upheld: false, Now);

        report.DomainEvents.OfType<ContentReportUpheldDomainEvent>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(CommunityLifeReportStatus.Upheld, true)]
    [InlineData(CommunityLifeReportStatus.Dismissed, false)]
    [InlineData(CommunityLifeReportStatus.Duplicate, false)]
    public void A_community_life_report_says_so_only_when_upheld(CommunityLifeReportStatus outcome, bool announced)
    {
        var subject = Guid.NewGuid();
        var report = CommunityLifeReport.File(
            CommunityLifeReportId.New(), ReportSubjectType.Event, subject, Reporter,
            CommunityLifeReportReason.Spam, null, Now).Value;

        report.Resolve(outcome, Moderator, null, Now);

        var events = report.DomainEvents.OfType<CommunityLifeReportUpheldDomainEvent>().ToList();
        events.Should().HaveCount(announced ? 1 : 0);
        if (announced)
        {
            events[0].SubjectId.Should().Be(subject);
            events[0].ReportedByUserId.Should().Be(Reporter);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_housing_report_says_so_only_when_upheld(bool upheld)
    {
        var landlord = LandlordProfileId.New();
        var report = HousingReport.File(
            HousingReportId.New(), HousingListingId.New(), landlord, Reporter,
            HousingReportReason.Scam, null, Now).Value;

        report.Resolve(Moderator, upheld, null, Now);

        var events = report.DomainEvents.OfType<HousingReportUpheldDomainEvent>().ToList();
        events.Should().HaveCount(upheld ? 1 : 0);
        if (upheld)
        {
            events[0].LandlordId.Should().Be(landlord);
            events[0].ReportedBy.Should().Be(Reporter);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_job_report_says_so_only_when_upheld(bool upheld)
    {
        var employer = EmployerProfileId.New();
        var report = JobReport.File(
            JobReportId.New(), JobPostingId.New(), employer, Reporter,
            JobReportReason.Scam, null, Now).Value;

        report.Resolve(Moderator, upheld, null, Now);

        var events = report.DomainEvents.OfType<JobReportUpheldDomainEvent>().ToList();
        events.Should().HaveCount(upheld ? 1 : 0);
        if (upheld)
        {
            events[0].EmployerId.Should().Be(employer);
            events[0].ReportedBy.Should().Be(Reporter);
        }
    }

    /// <summary>Marketplace already announced a resolution; it now says who reported and whose listing it was.</summary>
    [Fact]
    public void A_marketplace_resolution_names_the_reporter_and_the_seller()
    {
        var seller = SellerProfileId.New();
        var report = ListingReport.File(
            ListingReportId.New(), MarketplaceListingId.New(), seller, Reporter,
            ListingReportReason.Fraud, null, 1, Now).Value;

        report.Resolve(ListingReportStatus.Actioned, Moderator, null, Now);

        var resolved = report.DomainEvents.OfType<ListingReportResolvedDomainEvent>().Should().ContainSingle().Subject;
        resolved.Status.Should().Be(ListingReportStatus.Actioned);
        resolved.ReporterId.Should().Be(Reporter);
        resolved.SellerId.Should().Be(seller);
    }
}
