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
/// Two awards for one member, saved at the same moment, under <em>different</em> keys.
/// <para>
/// Award keys stop one fact being paid twice. They cannot stop two different facts racing on
/// the same profile, and before <c>ReputationProfile.Revision</c> that race had two outcomes, both
/// wrong. A capped rule read "not yet scored" in both scopes and paid twice; and the score
/// columns were last-writer-wins, so both events persisted while one score change was lost.
/// </para>
/// <para>
/// Each test stages the race the same way: two scopes, both past every read, then both saving.
/// The loser throws <see cref="ConcurrencyConflictException"/>. An outbox consumer lets that
/// propagate and the message is retried, and the retry is what each test then performs.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ReputationProfileConcurrencyTests : PooledIdentityTestBase
{
    public ReputationProfileConcurrencyTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// Two producers for one capped action — the shape of the incident that paid CountryVerified
    /// twice — arriving together. Different keys, so the award index cannot help; the cap must.
    /// </summary>
    [Fact]
    public async Task Concurrent_awards_of_a_once_only_rule_pay_once()
    {
        var member = await MemberWithProfileAsync();

        using var first = Factory.Services.CreateScope();
        using var second = Factory.Services.CreateScope();

        // ProfilePhotoAdded: MaxOccurrences = 1, and nothing in the product pays it yet, so this
        // member has never had it.
        (await ScoreAsync(first, member, ReputationActions.ProfilePhotoAdded, Guid.NewGuid()))
            .WasApplied.Should().BeTrue();
        (await ScoreAsync(second, member, ReputationActions.ProfilePhotoAdded, Guid.NewGuid()))
            .WasApplied.Should().BeTrue("neither scope can see the other's unsaved award");

        await SaveAsync(first);

        var losing = () => SaveAsync(second);
        await losing.Should().ThrowAsync<ConcurrencyConflictException>();

        // The retry reads the first award's usage, and the cap holds.
        using var retry = Factory.Services.CreateScope();
        var outcome = await ScoreAsync(retry, member, ReputationActions.ProfilePhotoAdded, Guid.NewGuid());

        outcome.WasApplied.Should().BeFalse();
        (await EventCountAsync(member, ReputationActions.ProfilePhotoAdded)).Should().Be(1);
    }

    /// <summary>
    /// Two different comments, both legitimately paid — once the loser is retried. What must not
    /// happen is the old lost update: both events saved, one score change gone.
    /// </summary>
    [Fact]
    public async Task Concurrent_awards_leave_the_score_equal_to_the_sum_of_its_events()
    {
        var member = await MemberWithProfileAsync();

        using var first = Factory.Services.CreateScope();
        using var second = Factory.Services.CreateScope();

        await ScoreAsync(first, member, ReputationActions.CommentWritten, Guid.NewGuid());

        var retriedComment = Guid.NewGuid();
        await ScoreAsync(second, member, ReputationActions.CommentWritten, retriedComment);

        await SaveAsync(first);

        var losing = () => SaveAsync(second);
        await losing.Should().ThrowAsync<ConcurrencyConflictException>();

        using (var retry = Factory.Services.CreateScope())
        {
            (await ScoreAsync(retry, member, ReputationActions.CommentWritten, retriedComment))
                .WasApplied.Should().BeTrue("a different comment is a different award and is owed");
            await SaveAsync(retry);
        }

        using var read = Factory.Services.CreateScope();
        var context = read.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var profile = await context.Set<ReputationProfile>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == member);

        var contributionEvents = await context.Set<ReputationEvent>()
            .AsNoTracking()
            .Where(recorded => recorded.ProfileId == profile.Id
                && recorded.Dimension == ReputationDimension.contribution)
            .Select(recorded => recorded.Points)
            .ToListAsync();

        profile.ContributionScore.Should().Be(
            contributionEvents.Sum(), "the profile is a replay of its events, not a guess at them");
        profile.CommentsWritten.Should().Be(contributionEvents.Count);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A member whose reputation profile already exists, so the race is on the profile row.</summary>
    private async Task<UserId> MemberWithProfileAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = UserId.From(auth.User.Id);

        using var scope = Factory.Services.CreateScope();
        await ScoreAsync(scope, member, ReputationActions.CommentWritten, Guid.NewGuid());
        await SaveAsync(scope);

        return member;
    }

    /// <summary>
    /// Scores under a key unique to <paramref name="factId"/>. A comment key for CommentWritten;
    /// a verification key otherwise, which is how a second producer would name its own fact.
    /// </summary>
    private static Task<ScoringOutcome> ScoreAsync(
        IServiceScope scope, UserId member, string actionType, Guid factId)
    {
        var key = actionType == ReputationActions.CommentWritten
            ? ReputationAwardKey.Comment(factId)
            : ReputationAwardKey.Verification(actionType, factId);

        return scope.ServiceProvider
            .GetRequiredService<ReputationService>()
            .ScoreAsync(member, actionType, key, "test", factId.ToString());
    }

    private static Task<int> SaveAsync(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

    private async Task<int> EventCountAsync(UserId member, string actionType)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        return await context.Set<ReputationEvent>()
            .Join(
                context.Set<ReputationProfile>().Where(profile => profile.UserId == member),
                recorded => recorded.ProfileId,
                profile => profile.Id,
                (recorded, _) => recorded)
            .CountAsync(recorded => recorded.ActionType == actionType);
    }
}
