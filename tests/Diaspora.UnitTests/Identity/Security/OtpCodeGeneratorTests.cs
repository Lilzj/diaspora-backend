using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Infrastructure.Identity.Otp;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Identity.Security;

public class OtpCodeGeneratorTests
{
    private static SecureOtpCodeGenerator CreateGenerator(int codeLength = 6, string key = "otp-test-key") =>
        new(
            Options.Create(new OtpOptions { CodeLength = codeLength }),
            Options.Create(new JwtOptions { SigningKey = key }));

    [Fact]
    public void Codes_have_the_configured_length()
    {
        CreateGenerator(codeLength: 6).Generate().Length.Should().Be(6);
        CreateGenerator(codeLength: 8).Generate().Length.Should().Be(8);
    }

    [Fact]
    public void Codes_are_numeric()
    {
        var generator = CreateGenerator();

        for (var i = 0; i < 100; i++)
        {
            generator.Generate().Should().MatchRegex("^[0-9]+$");
        }
    }

    [Fact]
    public void Codes_use_the_whole_range_including_leading_zeros()
    {
        var generator = CreateGenerator();
        var codes = Enumerable.Range(0, 2000).Select(_ => generator.Generate()).ToArray();

        codes.Should().Contain(code => code.StartsWith('0'),
            "trimming leading zeros would silently shrink the code space");
    }

    [Fact]
    public void Generated_codes_are_not_trivially_repetitive()
    {
        var generator = CreateGenerator();
        var codes = Enumerable.Range(0, 500).Select(_ => generator.Generate()).ToArray();

        codes.Distinct().Should().HaveCountGreaterThan(400,
            "a predictable generator would collide far more often");
    }

    [Fact]
    public void Hashing_is_deterministic()
    {
        var generator = CreateGenerator();

        generator.ComputeHash("123456").Should().Be(generator.ComputeHash("123456"));
    }

    [Fact]
    public void Different_codes_hash_differently()
    {
        var generator = CreateGenerator();

        generator.ComputeHash("123456").Should().NotBe(generator.ComputeHash("123457"));
    }

    [Fact]
    public void The_hash_is_keyed_so_a_leaked_table_cannot_be_enumerated()
    {
        // An unkeyed digest of a six-digit code offers no protection at all: the whole
        // space precomputes in under a second.
        var first = CreateGenerator(key: "key-one");
        var second = CreateGenerator(key: "key-two");

        first.ComputeHash("123456").Should().NotBe(second.ComputeHash("123456"));
    }

    [Fact]
    public void Surrounding_whitespace_is_ignored_when_hashing()
    {
        var generator = CreateGenerator();

        generator.ComputeHash(" 123456 ").Should().Be(generator.ComputeHash("123456"));
    }
}
