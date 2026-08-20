using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Infrastructure.Identity.Passwords;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Identity.Security;

public class PasswordHasherTests
{
    // Deliberately low for test speed. Production uses the configured 210,000.
    private static PbkdfPasswordHasher CreateHasher(int iterations = 10_000) =>
        new(Options.Create(new PasswordPolicyOptions { HashIterations = iterations }));

    [Fact]
    public void A_hashed_password_verifies()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("Correct-Horse-Battery-7");

        hasher.Verify(hash, "Correct-Horse-Battery-7").Should().Be(PasswordVerificationOutcome.Succeeded);
    }

    [Fact]
    public void A_wrong_password_fails()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("Correct-Horse-Battery-7");

        hasher.Verify(hash, "Wrong-Horse-Battery-7").Should().Be(PasswordVerificationOutcome.Failed);
    }

    [Fact]
    public void Verification_is_case_sensitive()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("Correct-Horse-Battery-7");

        hasher.Verify(hash, "correct-horse-battery-7").Should().Be(PasswordVerificationOutcome.Failed);
    }

    [Fact]
    public void The_same_password_hashes_differently_each_time()
    {
        var hasher = CreateHasher();

        var first = hasher.Hash("Correct-Horse-Battery-7");
        var second = hasher.Hash("Correct-Horse-Battery-7");

        first.Should().NotBe(second, "each hash must carry its own salt");
    }

    [Fact]
    public void An_empty_password_never_verifies()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("Correct-Horse-Battery-7");

        hasher.Verify(hash, string.Empty).Should().Be(PasswordVerificationOutcome.Failed);
    }

    [Fact]
    public void A_hash_from_weaker_parameters_is_flagged_for_upgrade()
    {
        // The mechanism that lets the iteration count rise without a migration nobody
        // can run against one-way data.
        var weak = CreateHasher(iterations: 10_000);
        var strong = CreateHasher(iterations: 50_000);

        var legacyHash = weak.Hash("Correct-Horse-Battery-7");

        strong.Verify(legacyHash, "Correct-Horse-Battery-7")
            .Should().Be(PasswordVerificationOutcome.SucceededNeedsRehash);
    }

    [Fact]
    public void The_dummy_hash_is_stable_and_verifies_against_nothing_real()
    {
        var hasher = CreateHasher();

        hasher.DummyHash.Should().Be(hasher.DummyHash);
        hasher.Verify(hasher.DummyHash, "Correct-Horse-Battery-7")
            .Should().Be(PasswordVerificationOutcome.Failed);
    }

    [Fact]
    public void A_password_hash_never_renders_its_value()
    {
        var hash = PasswordHash.Create("hashed::secret");

        hash.ToString().Should().Be("***");
    }
}
