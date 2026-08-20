using Diaspora.SharedKernel.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.SharedKernel;

public class ValueObjectTests
{
    [Fact]
    public void PhoneNumber_should_validate_e164_format()
    {
        var phone = PhoneNumber.Create("+447700900123");

        phone.Value.Should().Be("+447700900123");
    }

    [Fact]
    public void PhoneNumber_should_reject_invalid_format()
    {
        FluentActions.Invoking(() => PhoneNumber.Create("not-a-phone"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PhoneNumber_should_mask_correctly()
    {
        var phone = PhoneNumber.Create("+447700900123");

        phone.Masked.Should().Contain("***");
        phone.Masked.Should().EndWith("0123");
    }

    [Fact]
    public void Money_should_add_same_currency()
    {
        var a = Money.Create(10.50m, "GBP");
        var b = Money.Create(5.25m, "GBP");

        var result = a + b;

        result.Amount.Should().Be(15.75m);
        result.CurrencyCode.Should().Be("GBP");
    }

    [Fact]
    public void Money_should_reject_different_currencies()
    {
        var gbp = Money.Create(10, "GBP");
        var usd = Money.Create(10, "USD");

        FluentActions.Invoking(() => gbp + usd)
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Coordinates_should_validate_range()
    {
        var coords = Coordinates.Create(51.5074, -0.1278);

        coords.Latitude.Should().Be(51.5074);
        coords.Longitude.Should().Be(-0.1278);
    }

    [Fact]
    public void Coordinates_should_reject_invalid_latitude()
    {
        FluentActions.Invoking(() => Coordinates.Create(91, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EmailAddress_should_validate()
    {
        var email = EmailAddress.Create("user@diaspora.app");

        email.Value.Should().Be("user@diaspora.app");
    }

    [Fact]
    public void DateRange_should_reject_invalid_range()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(-1);

        FluentActions.Invoking(() => DateRange.Create(start, end))
            .Should().Throw<ArgumentException>();
    }
}
