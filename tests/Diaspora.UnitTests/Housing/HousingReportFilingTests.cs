using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// Filing a housing report says so (7 Nov 2026). Nothing raised <see cref="HousingReportedDomainEvent"/>
/// before this date, so its handlers never ran, its contract never went out, and no housing report
/// ever reached a moderation case.
/// </summary>
public sealed class HousingReportFilingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Filing_raises_the_reported_event_with_the_count_and_without_the_reporter()
    {
        var listing = HousingListingId.New();
        var landlord = LandlordProfileId.New();

        var report = HousingReport.File(
            HousingReportId.New(), listing, landlord, UserId.New(),
            HousingReportReason.NotTheOwner, "The owner lives next door and is not letting it.", 3, Now).Value;

        var raised = report.DomainEvents.OfType<HousingReportedDomainEvent>().Should().ContainSingle().Subject;

        raised.ReportId.Should().Be(report.Id);
        raised.ListingId.Should().Be(listing);
        raised.LandlordId.Should().Be(landlord);
        raised.Reason.Should().Be(HousingReportReason.NotTheOwner);
        raised.TotalReports.Should().Be(3);
        raised.ReportedAt.Should().Be(Now);

        typeof(HousingReportedDomainEvent).GetProperties()
            .Should().NotContain(property => property.PropertyType == typeof(UserId),
                "the reporter stays on the report row");
    }
}
