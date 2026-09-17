using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Infrastructure.Statistics;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// The arithmetic behind the rent index: a rent per month, and a median. Small, and wrong in ways
/// nobody would see on a screen — a week taken as a quarter of a month publishes an index eight
/// per cent low everywhere weekly rents are common.
/// </summary>
public sealed class HousingRentStatisticsTests
{
    [Theory]
    [InlineData(RentPeriod.PerMonth, 1200, 1200)]
    [InlineData(RentPeriod.PerQuarter, 3600, 1200)]
    [InlineData(RentPeriod.PerYear, 14400, 1200)]
    public void A_rent_is_expressed_per_month(RentPeriod period, int amount, int monthly)
    {
        HousingRentStatistics.Monthly(Price(amount, period)).Should().Be(monthly);
    }

    [Fact]
    public void A_weekly_rent_is_fifty_two_weeks_over_twelve_months_not_four_weeks_a_month()
    {
        HousingRentStatistics.Monthly(Price(300m, RentPeriod.PerWeek)).Should().Be(1300m);
    }

    [Fact]
    public void The_median_is_the_middle_value_or_the_mean_of_the_middle_two()
    {
        HousingRentStatistics.Median([900m, 100m, 500m]).Should().Be(500m);
        HousingRentStatistics.Median([900m, 100m, 500m, 700m]).Should().Be(600m);
        HousingRentStatistics.Median([]).Should().BeNull();
    }

    private static HousingRentPrice Price(decimal amount, RentPeriod period) =>
        new("/gb/greater-london/london/peckham", amount, "GBP", period, DateTimeOffset.UtcNow);
}
