using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.SharedKernel;

public class ResultTests
{
    [Fact]
    public void Success_should_create_successful_result()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_should_create_failed_result()
    {
        var error = Error.NotFound("Entity.NotFound", "Entity not found");

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Generic_success_should_contain_value()
    {
        var value = "test value";

        var result = Result<string>.Success(value);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void Generic_failure_should_throw_on_value_access()
    {
        var result = Result<string>.Failure(Error.NotFound("Test", "Not found"));

        result.Invoking(r => _ = r.Value)
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Implicit_conversion_from_value_should_create_success()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Implicit_conversion_from_error_should_create_failure()
    {
        var error = Error.Validation("Test", "Error");
        Result<int> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Map_should_transform_successful_value()
    {
        var result = Result<int>.Success(5);

        var mapped = result.Map(v => v * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_should_propagate_failure()
    {
        var error = Error.NotFound("Test", "Not found");
        var result = Result<int>.Failure(error);

        var mapped = result.Map(v => v * 2);

        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(error);
    }

    [Fact]
    public void Match_should_invoke_success_handler()
    {
        var result = Result<int>.Success(42);

        var output = result.Match(
            onSuccess: v => $"Value: {v}",
            onFailure: e => $"Error: {e.Code}");

        output.Should().Be("Value: 42");
    }
}
