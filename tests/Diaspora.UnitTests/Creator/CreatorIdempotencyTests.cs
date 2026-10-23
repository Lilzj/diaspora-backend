using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// The creator idempotency primitive: whose key it is, and what a duplicate does inside a
/// transaction.
/// </summary>
public sealed class CreatorIdempotencyTests
{
    private const string Operation = "creator-signup-bonus-acknowledge";

    private static readonly Guid MemberA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid MemberB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Two_members_sending_the_same_key_get_different_keys()
    {
        CreatorIdempotency.Normalise(Operation, MemberA, "retry-1", null)
            .Should().NotBe(CreatorIdempotency.Normalise(Operation, MemberB, "retry-1", null));
    }

    [Fact]
    public void A_key_names_the_operation_the_caller_and_the_client_key()
    {
        CreatorIdempotency.Normalise(Operation, MemberA, "  retry-1 ", null)
            .Should().Be($"{Operation}:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:retry-1");
    }

    [Fact]
    public void The_header_wins_over_the_body_and_a_missing_key_is_null()
    {
        CreatorIdempotency.Normalise(Operation, MemberA, "header", "body")
            .Should().EndWith(":header");

        CreatorIdempotency.Normalise(Operation, MemberA, " ", null).Should().BeNull();
    }

    /// <summary>
    /// The column is 200. The longest operation name plus the caller's id leaves 128 for the
    /// client's part; anything longer is cut, so the key always fits.
    /// </summary>
    [Fact]
    public void The_longest_operation_with_an_overlong_client_key_still_fits_the_column()
    {
        var key = CreatorIdempotency.Normalise(Operation, MemberA, new string('k', 500), null);

        key!.Length.Should().BeLessThanOrEqualTo(200);
        key.Should().EndWith(new string('k', 128));
    }

    [Fact]
    public async Task Outside_a_transaction_a_duplicate_replays_the_first_record()
    {
        var first = new object();

        var result = await CreatorIdempotency.GuardAsync(
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

        var guarded = () => CreatorIdempotency.GuardAsync(
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

    private static DbUpdateException UniqueViolation() =>
        new("Saving failed.", new InvalidOperationException(
            "23505: duplicate key value violates unique constraint \"ux_creator_redemptions_key\""));
}
