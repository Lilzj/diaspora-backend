using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Trust.Application.Consumers;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The races step 3b's guarantees rest on, run against real Postgres.
/// <para>
/// <b>SQLite cannot lose these races.</b> It serialises every writer, and a unique violation there
/// leaves the transaction usable, so the SQLite suite passes whatever the code does when two requests
/// arrive together. These run only where <see cref="PostgresTestDatabase"/> names a migrated,
/// disposable database (see <see cref="PostgresFactAttribute"/>), and each asserts the invariant, not
/// that a race happened: two requests that happen to run one after the other must leave the same
/// state as two that collide.
/// </para>
/// <list type="bullet">
///   <item>Two members converting the last of the rewards pot (D-6): it pays once and never goes
///   negative.</item>
///   <item>One member sending one idempotency key twice at once (step 1, CS-4): one conversion, and
///   never a 500 from a transaction the unique violation aborted.</item>
///   <item>Two deliveries of one removal (D-9): the award is reversed once.</item>
/// </list>
/// </summary>
public sealed class PostgresRaceTests : IdentityTestBase<PostgresWebApplicationFactory>
{
    private const int Rounds = 5;

    private const string Conversions = "/api/v1/financial/rewards/conversions";

    public PostgresRaceTests(PostgresWebApplicationFactory factory) : base(factory)
    {
        factory.EnsureDatabaseCreated();
    }

    [PostgresFact]
    public async Task Two_members_racing_the_last_of_the_pot_are_paid_once_and_the_pot_never_goes_negative()
    {
        var (first, firstToken) = await RegisterMemberAsync();
        var (second, secondToken) = await RegisterMemberAsync();
        var currency = await MemberCurrencyAsync(first);

        (await MemberCurrencyAsync(second)).Should().Be(currency, "both convert from one pot");

        try
        {
            await PublishConversionRateAsync(currency);
            await GrantPointsAsync(first, 500 * Rounds);
            await GrantPointsAsync(second, 500 * Rounds);

            using var firstClient = ClientFor(firstToken);
            using var secondClient = ClientFor(secondToken);

            for (var round = 0; round < Rounds; round++)
            {
                // The pot holds exactly one conversion's credit.
                await SetPlatformBalanceAsync(WalletType.Rewards, currency, 500);

                var responses = await Task.WhenAll(
                    ConvertAsync(firstClient, 500, Guid.NewGuid().ToString("N")),
                    ConvertAsync(secondClient, 500, Guid.NewGuid().ToString("N")));

                responses.Should().OnlyContain(
                    response => response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict,
                    "a lost race is a refusal, never a 500 (round {0})", round);
                responses.Count(response => response.StatusCode == HttpStatusCode.OK)
                    .Should().Be(1, "the pot held one conversion's credit (round {0})", round);

                (await PlatformBalanceAsync(WalletType.Rewards, currency))
                    .Should().Be(0, "the pot is never overdrawn (round {0})", round);
            }

            (await ConversionCountAsync(first) + await ConversionCountAsync(second)).Should().Be(Rounds);
        }
        finally
        {
            await ResetConversionRatesAsync(currency);
        }
    }

    [PostgresFact]
    public async Task One_key_sent_twice_at_once_converts_once_and_never_answers_500()
    {
        var (member, token) = await RegisterMemberAsync();
        var currency = await MemberCurrencyAsync(member);

        try
        {
            await PublishConversionRateAsync(currency);
            await GrantPointsAsync(member, 500 * Rounds);
            await SetPlatformBalanceAsync(WalletType.Rewards, currency, 1_000_000);

            using var client = ClientFor(token);
            using var twin = ClientFor(token);

            for (var round = 0; round < Rounds; round++)
            {
                var key = Guid.NewGuid().ToString("N");

                var responses = await Task.WhenAll(
                    ConvertAsync(client, 500, key),
                    ConvertAsync(twin, 500, key));

                responses.Should().OnlyContain(
                    response => response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict,
                    "the second request replays or is refused; an aborted transaction must not surface as a 500 (round {0})",
                    round);
                responses.Should().Contain(response => response.StatusCode == HttpStatusCode.OK);
            }

            (await ConversionCountAsync(member)).Should().Be(Rounds, "each key converted once");
            (await PointsAsync(member)).Should().Be(0, "each key took its points once");
        }
        finally
        {
            await ResetConversionRatesAsync(currency);
        }
    }

    [PostgresFact]
    public async Task Two_deliveries_of_one_removal_reverse_the_award_once()
    {
        var (member, _) = await RegisterMemberAsync();
        var userId = UserId.From(member);

        for (var round = 0; round < Rounds; round++)
        {
            var guideId = Guid.NewGuid();
            var before = await ProfileAsync(userId);

            using (var scope = Factory.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                await services.GetRequiredService<TrustProfileProvisioner>().GetOrCreateBothAsync(userId);

                // An award with no rule, sourced from the guide as GuidePublished's is. The rule itself
                // has a one-hour cooldown, so a second round would earn nothing to reverse.
                var awarded = await services.GetRequiredService<ReputationService>().ApplyAwardAsync(
                    userId,
                    ReputationAward.Of("PostgresRaceTest", ReputationDimension.contribution, 50),
                    ReputationAwardKey.GuidePublished(guideId),
                    "guide",
                    guideId.ToString());
                await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

                awarded.WasApplied.Should().BeTrue("each round needs an award to race over (round {0})", round);
            }

            var deleted = new GuideDeletedIntegrationEvent
            {
                GuideId = guideId,
                AuthorId = member,
                DeletedAt = DateTimeOffset.UtcNow
            };

            var outcomes = await Task.WhenAll(DeliverAsync(deleted), DeliverAsync(deleted));

            outcomes.Where(outcome => outcome is not null).Should().OnlyContain(
                outcome => outcome is DbUpdateException || outcome is ConcurrencyConflictException,
                "the losing delivery fails on the database's guard and is retried (round {0})", round);

            // The outbox retries a delivery that threw. The retry must find nothing left to undo.
            foreach (var _ in outcomes.Where(outcome => outcome is not null))
            {
                (await DeliverAsync(deleted)).Should().BeNull("a retried delivery is a no-op (round {0})", round);
            }

            (await ReversalCountAsync(guideId)).Should().Be(1, "the award is reversed once (round {0})", round);

            (await ProfileAsync(userId)).ContributionScore
                .Should().Be(before.ContributionScore, "the score is back where it was (round {0})", round);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<(Guid UserId, string Token)> RegisterMemberAsync()
    {
        // Not the suite's counter: it restarts at one in every process, and this database outlives
        // the process, so a second run would register the first run's numbers again. The counter
        // never reaches eight digits.
        var (_, auth) = await RegisterVerifiedMemberAsync(
            $"+23480{Random.Shared.Next(10_000_000, 100_000_000)}");

        // The financial account is opened by an integration event, delivered by hand in this host.
        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();

        var account = await accounts.FindByOwnerAsync(auth.User.Id, FinancialAccountType.Personal)
            ?? await identities.GetOrCreateAsync(auth.User.Id, FinancialAccountType.Personal, "GBP");

        // What Trust's approval does; see FinancialMovementTests.ActivateFinancialIdentityAsync.
        await identities.ActivateAsync(account);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        return (auth.User.Id, auth.Tokens.AccessToken);
    }

    private HttpClient ClientFor(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static Task<HttpResponseMessage> ConvertAsync(HttpClient client, long points, string key) =>
        client.PostAsJsonAsync(new Uri(Conversions, UriKind.Relative), new { points, idempotencyKey = key });

    /// <summary>Null when the delivery succeeded; the exception when it did not.</summary>
    private async Task<Exception?> DeliverAsync(GuideDeletedIntegrationEvent deleted)
    {
        try
        {
            using var scope = Factory.Services.CreateScope();

            var consumer = ActivatorUtilities.CreateInstance<GuideDeletedReversalConsumer>(scope.ServiceProvider);

            await consumer.Handle(
                new IntegrationEventNotification<GuideDeletedIntegrationEvent>(deleted), CancellationToken.None);

            return null;
        }
#pragma warning disable CA1031 // The test asserts which exceptions a lost race may raise.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return exception;
        }
    }

    private async Task<string> MemberCurrencyAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var account = await scope.ServiceProvider
            .GetRequiredService<IFinancialAccountRepository>()
            .FindByOwnerAsync(userId, FinancialAccountType.Personal);

        return account?.PrimaryCurrency
            ?? throw new InvalidOperationException($"Member {userId} has no financial account.");
    }

    /// <summary>Vested points, straight onto the account: what the race is about is the conversion.</summary>
    private async Task GrantPointsAsync(Guid userId, long points)
    {
        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var account = await accounts.FindByMemberAsync(userId);

        if (account is null)
        {
            account = RewardAccount.Open(RewardAccountId.From(guids.NewSequentialGuid()), userId, clock.UtcNow);
            accounts.Add(account);
        }

        account.Grant(guids.NewSequentialGuid(), points, "PostgresRaceTest", $"race:{Guid.NewGuid():N}", clock.UtcNow);
        accounts.Update(account);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private async Task<long> PointsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var account = await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(userId);

        return account?.Balance ?? 0;
    }

    private async Task<int> ConversionCountAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<RewardConversion>()
            .CountAsync(conversion => conversion.MemberUserId == userId);
    }

    private async Task<int> ReversalCountAsync(Guid guideId)
    {
        using var scope = Factory.Services.CreateScope();

        var source = guideId.ToString();

        return await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<ReputationEvent>()
            .CountAsync(item => item.SourceId == source && item.ReversesEventId != null);
    }

    private async Task<ReputationProfile> ProfileAsync(UserId userId)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<Diaspora.Modules.Trust.Persistence.IReputationProfileRepository>()
            .GetByUserAsync(userId)
            ?? ReputationProfile.Open(userId, DateTimeOffset.UtcNow);
    }

    /// <summary>A country rate for the members' currency, at one minor unit a point, spendable at once.</summary>
    private async Task PublishConversionRateAsync(string currency)
    {
        using var scope = Factory.Services.CreateScope();

        var policies = scope.ServiceProvider.GetRequiredService<IFinancialPolicyRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

        foreach (var superseded in (await policies.ListActiveByKeyAsync(FinancialPolicyKeys.RewardConversion))
            .Where(policy => policy.Scope == PolicyScopeKind.Platform))
        {
            superseded.Supersede(clock.UtcNow);
            policies.Update(superseded);
        }

        var version = await policies.HighestVersionAsync(
            FinancialPolicyKeys.RewardConversion, PolicyScopeKind.Platform, scopeValue: null);

        var policy = FinancialPolicy.Publish(
            FinancialPolicyId.From(guids.NewSequentialGuid()),
            FinancialPolicyKeys.RewardConversion,
            FinancialPolicyKind.RewardConversion,
            PolicyScopeKind.Platform,
            scopeValue: null,
            version + 1,
            DateWindow.Open(clock.UtcNow.AddMinutes(-1)),
            "A Postgres race test's conversion rate.",
            clock.UtcNow);

        policy.SetParameter(guids.NewSequentialGuid(), FinancialPolicyParameters.MinorUnitsPerPoint, "1");
        policy.SetParameter(guids.NewSequentialGuid(), FinancialPolicyParameters.MinimumPoints, "10");
        policy.SetParameter(guids.NewSequentialGuid(), FinancialPolicyParameters.Currency, currency);
        policy.SetParameter(guids.NewSequentialGuid(), FinancialPolicyParameters.VestingDays, "0");

        policies.Add(policy);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    /// <summary>Supersedes this run's rate, so the database is left converting nothing again.</summary>
    private async Task ResetConversionRatesAsync(string currency)
    {
        using var scope = Factory.Services.CreateScope();

        var policies = scope.ServiceProvider.GetRequiredService<IFinancialPolicyRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var policy in (await policies.ListActiveByKeyAsync(FinancialPolicyKeys.RewardConversion))
            .Where(policy => policy.Scope == PolicyScopeKind.Platform
                && policy.Parameter(FinancialPolicyParameters.Currency) == currency))
        {
            policy.Supersede(clock.UtcNow);
            policies.Update(policy);
        }

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    /// <summary>Sets a platform wallet to an exact balance, moving the difference to or from Clearing.</summary>
    private async Task SetPlatformBalanceAsync(WalletType walletType, string currency, long minorUnits)
    {
        using var scope = Factory.Services.CreateScope();

        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerPostingService>();

        var wallet = (await identities.PlatformWalletAsync(walletType, currency))!;
        var clearing = (await identities.PlatformWalletAsync(WalletType.Clearing, currency))!;
        var delta = minorUnits - wallet.AvailableMinorUnits;

        if (delta == 0)
        {
            return;
        }

        var amount = Money.FromMinorUnits(Math.Abs(delta), wallet.Currency, wallet.Exponent);
        var (from, to) = delta > 0 ? (clearing, wallet) : (wallet, clearing);

        var posted = await ledger.PostAsync(new LedgerPostingRequest
        {
            TransactionType = TransactionType.Adjustment,
            Amount = amount,
            Description = "Postgres race test: set a platform balance.",
            Audit = AuditStamp.Create(
                FinancialActor.System("PostgresRaceTests"),
                FinancialSource.System,
                "Postgres race test: set a platform balance."),
            Legs =
            [
                new LedgerPostingLeg
                {
                    WalletId = from.Id,
                    Direction = LedgerDirection.Debit,
                    Amount = amount,
                    EntryType = LedgerEntryType.Adjustment
                },
                new LedgerPostingLeg
                {
                    WalletId = to.Id,
                    Direction = LedgerDirection.Credit,
                    Amount = amount,
                    EntryType = LedgerEntryType.Adjustment
                }
            ],
            Reference = $"postgres-race-test:{Guid.NewGuid():N}"
        });

        posted.IsSuccess.Should().BeTrue(posted.IsFailure ? posted.Error.Description : null);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private async Task<long> PlatformBalanceAsync(WalletType walletType, string currency)
    {
        using var scope = Factory.Services.CreateScope();

        var wallet = await scope.ServiceProvider
            .GetRequiredService<IWalletRepository>()
            .FindPlatformWalletAsync(walletType, currency);

        return wallet!.AvailableMinorUnits;
    }
}
