using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The half of award deduplication a unit test cannot reach: the database.
/// <para>
/// <c>ReputationService</c> reads before it writes, and two outbox workers holding the same
/// message — a lease that lapsed while the first was still running — both read "not yet awarded".
/// <c>ix_reputation_events_profile_award_key</c> is what stops the second payment. Since the
/// profile gained a <c>Revision</c> token the loser may trip that first instead; either way it
/// throws, and these tests assert the outcome rather than which guard fired.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ReputationAwardKeyIndexTests : PooledIdentityTestBase
{
    public ReputationAwardKeyIndexTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Two_racing_awards_for_one_fact_save_once()
    {
        var member = await MemberWithProfileAsync();
        var key = ReputationAwardKey.Comment(Guid.NewGuid());

        using var first = Factory.Services.CreateScope();
        using var second = Factory.Services.CreateScope();

        // Both get past the read: neither has saved, so neither can see the other.
        (await ScoreAsync(first, member, key)).WasApplied.Should().BeTrue();
        (await ScoreAsync(second, member, key)).WasApplied.Should().BeTrue();

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var losing = () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        // A consumer lets this throw and the outbox retries the message — see the next test.
        await losing.Should().ThrowAsync<Exception>();

        (await CountAsync(key)).Should().Be(1, "the index is what makes 'once' true under a race");
    }

    /// <summary>The retry the outbox makes after the loser throws: it is skipped, not paid.</summary>
    [Fact]
    public async Task The_retry_after_a_lost_race_is_skipped()
    {
        var member = await MemberWithProfileAsync();
        var key = ReputationAwardKey.Comment(Guid.NewGuid());

        using (var scope = Factory.Services.CreateScope())
        {
            await ScoreAsync(scope, member, key);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        using var retry = Factory.Services.CreateScope();

        var outcome = await ScoreAsync(retry, member, key);

        outcome.WasApplied.Should().BeFalse();
        outcome.SkipReason.Should().Be("Already awarded for this.");
        (await CountAsync(key)).Should().Be(1);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A member whose reputation profile already exists, so the race is on the award key and
    /// not on <c>ix_reputation_profiles_user</c>.
    /// </summary>
    private async Task<UserId> MemberWithProfileAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = UserId.From(auth.User.Id);

        using var scope = Factory.Services.CreateScope();

        await ScoreAsync(scope, member, ReputationAwardKey.Comment(Guid.NewGuid()));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        return member;
    }

    private static Task<ScoringOutcome> ScoreAsync(
        IServiceScope scope, UserId member, ReputationAwardKey key) =>
        scope.ServiceProvider
            .GetRequiredService<ReputationService>()
            .ScoreAsync(member, ReputationActions.CommentWritten, key, "comment", key.Value);

    private async Task<int> CountAsync(ReputationAwardKey key)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<DiasporaDbContext>()
            .Set<ReputationEvent>()
            .CountAsync(recorded => recorded.AwardKey == key.Value);
    }
}
