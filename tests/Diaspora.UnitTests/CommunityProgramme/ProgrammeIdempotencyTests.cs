using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// The programme idempotency primitive: whose key it is, and what a duplicate does inside a
/// transaction.
/// </summary>
public sealed class ProgrammeIdempotencyTests
{
    /// <summary>The longest operation name the module uses.</summary>
    private const string Operation = "membership-application";

    private static readonly Guid MemberA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid MemberB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>The shape of a voter fingerprint: base64 of an HMAC-SHA256, 44 characters.</summary>
    private static readonly string Fingerprint = Convert.ToBase64String(new byte[32]);

    [Fact]
    public void Two_members_sending_the_same_key_get_different_keys()
    {
        ProgrammeIdempotency.Normalise(Operation, MemberA, "retry-1", null)
            .Should().NotBe(ProgrammeIdempotency.Normalise(Operation, MemberB, "retry-1", null));
    }

    [Fact]
    public void A_key_names_the_operation_the_caller_and_the_client_key()
    {
        ProgrammeIdempotency.Normalise(Operation, MemberA, "  retry-1 ", null)
            .Should().Be($"{Operation}:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:retry-1");
    }

    /// <summary>An anonymous poll stores no voter, so a vote's key carries the fingerprint instead.</summary>
    [Fact]
    public void A_vote_key_is_scoped_by_the_fingerprint_and_names_no_voter()
    {
        var key = ProgrammeIdempotency.NormaliseForVoter("poll-vote", Fingerprint, "retry-1", null);

        key.Should().Be($"poll-vote:{Fingerprint}:retry-1");
        key.Should().NotContain(MemberA.ToString("N"));
    }

    [Fact]
    public void The_header_wins_over_the_body_and_a_missing_key_is_null()
    {
        ProgrammeIdempotency.Normalise(Operation, MemberA, "header", "body")
            .Should().EndWith(":header");

        ProgrammeIdempotency.Normalise(Operation, MemberA, " ", null).Should().BeNull();
        ProgrammeIdempotency.NormaliseForVoter("poll-vote", Fingerprint, null, "").Should().BeNull();
    }

    /// <summary>
    /// The column is 200. The longest operation name plus the widest scope leaves 128 for the
    /// client's part; anything longer is cut, so the key always fits.
    /// </summary>
    [Fact]
    public void The_longest_operation_with_an_overlong_client_key_still_fits_the_column()
    {
        var overlong = new string('k', 500);

        var member = ProgrammeIdempotency.Normalise(Operation, MemberA, overlong, null);
        var vote = ProgrammeIdempotency.NormaliseForVoter(Operation, Fingerprint, overlong, null);

        member!.Length.Should().BeLessThanOrEqualTo(200);
        vote!.Length.Should().BeLessThanOrEqualTo(200);
        member.Should().EndWith(new string('k', 128));
    }

    [Fact]
    public async Task Outside_a_transaction_a_duplicate_replays_the_first_record()
    {
        var first = new object();

        var result = await ProgrammeIdempotency.GuardAsync(
            "k",
            _ => throw UniqueViolation(),
            _ => Task.FromResult<object?>(first),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Replayed.Should().BeSameAs(first);
    }

    /// <summary>
    /// Inside a transaction the violation has aborted it, so the guard must not run a lookup and
    /// must not return a failed result that the transaction behaviour would then commit.
    /// </summary>
    [Fact]
    public async Task Inside_a_transaction_a_duplicate_is_rethrown_as_a_concurrency_conflict()
    {
        var lookedUp = false;

        var guarded = () => ProgrammeIdempotency.GuardAsync(
            "k",
            _ => throw UniqueViolation(),
            _ =>
            {
                lookedUp = true;
                return Task.FromResult<object?>(new object());
            },
            CancellationToken.None,
            inTransaction: true);

        await guarded.Should().ThrowAsync<ConcurrencyConflictException>();
        lookedUp.Should().BeFalse("the aborted transaction cannot run the lookup");
    }

    /// <summary>A keyless request that loses a race on a natural index aborts the same transaction.</summary>
    [Fact]
    public async Task Inside_a_transaction_a_keyless_duplicate_is_rethrown_too()
    {
        var guarded = () => ProgrammeIdempotency.GuardAsync(
            key: null,
            _ => throw UniqueViolation(),
            _ => Task.FromResult<object?>(null),
            CancellationToken.None,
            inTransaction: true);

        await guarded.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    private static DbUpdateException UniqueViolation() =>
        new("Saving failed.", new InvalidOperationException(
            "23505: duplicate key value violates unique constraint \"ux_community_programme_poll_responses_voter\""));
}
