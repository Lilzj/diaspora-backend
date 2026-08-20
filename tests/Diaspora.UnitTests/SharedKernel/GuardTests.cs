using Diaspora.SharedKernel;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.SharedKernel;

public class GuardTests
{
    [Fact]
    public void Guard_null_should_throw_for_null()
    {
        string? value = null;

        FluentActions.Invoking(() => Guard.Against.Null(value!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Guard_null_should_return_value_for_non_null()
    {
        var result = Guard.Against.Null("hello");
        result.Should().Be("hello");
    }

    [Fact]
    public void Guard_out_of_range_should_throw()
    {
        FluentActions.Invoking(() => Guard.Against.OutOfRange(100, "value", 0, 50))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Guard_empty_guid_should_throw()
    {
        FluentActions.Invoking(() => Guard.Against.Empty(Guid.Empty))
            .Should().Throw<ArgumentException>();
    }
}
