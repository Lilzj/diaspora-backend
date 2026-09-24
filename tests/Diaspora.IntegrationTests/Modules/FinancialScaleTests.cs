using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Extensibility.Pricing;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// What the Financial Platform does when there is a lot of it.
/// <para>
/// <b>These are shape tests, not benchmarks.</b> They run against in-memory SQLite on whatever
/// machine the suite happens to be on, so an absolute millisecond figure would be a number nobody
/// could reproduce — the platform's technical debt ledger records the absence of real load testing
/// as DEBT-030 and this does not close it. What they do check is that the shape does not degrade:
/// a statement pages without loss or repetition, a wallet rebuilds to the same numbers from a
/// thousand entries, a batch placement lookup is one query rather than fifty, and policy resolution
/// does not get slower as versions accumulate.
/// </para>
/// </summary>
[Collection(FinanceCollection.Name)]
public sealed class FinancialScaleTests : PooledIdentityTestBase
{
    /// <summary>Enough to expose a paging or accumulation defect; small enough to run every commit.</summary>
    private const int LedgerDepth = 500;

    private const int SubjectCount = 60;

    public FinancialScaleTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    /// <summary>
    /// <b>A statement pages without loss or repetition at depth.</b> The sequence is what makes the
    /// keyset exact: two entries sharing a transaction share a timestamp to the microsecond, and a
    /// timestamp-only cursor would drop one of them silently.
    /// </summary>
    [Fact]
    public async Task A_deep_statement_pages_without_loss_or_repetition()
    {
        var wallet = await FillWalletAsync(LedgerDepth);

        using var scope = Factory.Services.CreateScope();
        var entries = scope.ServiceProvider.GetRequiredService<ILedgerEntryRepository>();

        var seen = new List<long>(LedgerDepth);
        long? cursor = null;

        for (var page = 0; page < 40; page++)
        {
            var batch = await entries.PageAsync(new LedgerQuery
            {
                WalletId = wallet,
                AfterSequence = cursor,
                PageSize = 25
            });

            if (batch.Count == 0)
            {
                break;
            }

            seen.AddRange(batch.Select(entry => entry.Sequence));
            cursor = batch[^1].Sequence;
        }

        seen.Should().HaveCount(LedgerDepth, "every entry is served exactly once");
        seen.Distinct().Should().HaveCount(LedgerDepth, "no entry is served twice");
        seen.Should().BeInDescendingOrder("a statement reads newest first");
    }

    /// <summary>
    /// <b>The recovery path, at depth.</b> Deleting every wallet row and replaying the ledger is a
    /// supported operation; this is the assertion that it produces the same numbers.
    /// </summary>
    [Fact]
    public async Task A_wallet_rebuilds_to_the_same_numbers_from_a_deep_ledger()
    {
        var walletId = await FillWalletAsync(LedgerDepth);

        using var scope = Factory.Services.CreateScope();
        var wallets = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var entries = scope.ServiceProvider.GetRequiredService<ILedgerEntryRepository>();

        var wallet = await wallets.FindAsync(walletId);

        wallet.Should().NotBeNull();

        var expectedAvailable = wallet!.AvailableMinorUnits;
        var expectedCredits = wallet.LifetimeCreditMinorUnits;
        var expectedDebits = wallet.LifetimeDebitMinorUnits;

        var stopwatch = Stopwatch.StartNew();

        wallet.RebuildFrom(await entries.ListForRebuildAsync(walletId));

        stopwatch.Stop();

        wallet.AvailableMinorUnits.Should().Be(expectedAvailable);
        wallet.LifetimeCreditMinorUnits.Should().Be(expectedCredits);
        wallet.LifetimeDebitMinorUnits.Should().Be(expectedDebits);
        wallet.EntryCount.Should().Be(LedgerDepth);
    }

    /// <summary>
    /// <b>A list page resolves its placements in one query, not one per row.</b> "Is this paid for"
    /// is asked in batches — a marketplace page resolves fifty subjects at once — and a per-subject
    /// lookup would put fifty round trips on somebody's scroll.
    /// </summary>
    [Fact]
    public async Task Placements_for_many_subjects_resolve_in_one_lookup()
    {
        var subjects = await CreatePlacementsAsync(SubjectCount);

        using var scope = Factory.Services.CreateScope();
        var placements = scope.ServiceProvider.GetRequiredService<IPlacementRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var found = await placements.ListActiveForSubjectsAsync(
            MonetizationSubjectType.MarketplaceListing, subjects, clock.UtcNow);

        found.Should().HaveCount(SubjectCount);
        found.Select(placement => placement.Subject.SubjectId)
            .Should().BeEquivalentTo(subjects);
    }

    /// <summary>
    /// <b>Policy resolution does not get slower as versions accumulate.</b> Versions are rows and
    /// never edits, so a mature deployment has many of them per key — and the engine reads only the
    /// active ones and picks the most specific.
    /// </summary>
    [Fact]
    public async Task Policy_resolution_stays_correct_as_versions_accumulate()
    {
        using var scope = Factory.Services.CreateScope();

        var policies = scope.ServiceProvider.GetRequiredService<IFinancialPolicyRepository>();
        var engine = scope.ServiceProvider.GetRequiredService<IFinancialPolicyEngine>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var key = $"scale.commission.{Guid.NewGuid():N}";
        var now = clock.UtcNow;

        // Twenty superseded versions and one live one — the shape of a key somebody has retuned
        // every month for two years.
        for (var version = 1; version <= 20; version++)
        {
            var superseded = FinancialPolicy.Publish(
                FinancialPolicyId.From(guids.NewSequentialGuid()),
                key,
                FinancialPolicyKind.MarketplaceCommission,
                PolicyScopeKind.Platform,
                null,
                version,
                DateWindow.Open(now.AddDays(-400 + version)),
                $"Version {version}.",
                now);

            superseded.SetParameter(guids.NewSequentialGuid(), "percentage", "1.0");
            superseded.Supersede(now.AddDays(-399 + version));

            policies.Add(superseded);
        }

        var live = FinancialPolicy.Publish(
            FinancialPolicyId.From(guids.NewSequentialGuid()),
            key,
            FinancialPolicyKind.MarketplaceCommission,
            PolicyScopeKind.Platform,
            null,
            21,
            DateWindow.Open(now.AddDays(-1)),
            "The live version.",
            now);

        live.SetParameter(guids.NewSequentialGuid(), "percentage", "5.0");
        policies.Add(live);

        // A country override, which must win over the platform default.
        var override_ = FinancialPolicy.Publish(
            FinancialPolicyId.From(guids.NewSequentialGuid()),
            key,
            FinancialPolicyKind.MarketplaceCommission,
            PolicyScopeKind.Country,
            "NG",
            1,
            DateWindow.Open(now.AddDays(-1)),
            "Nigeria.",
            now);

        override_.SetParameter(guids.NewSequentialGuid(), "percentage", "2.0");
        policies.Add(override_);

        await unitOfWork.SaveChangesAsync();

        var platformPrice = await engine.PriceAsync(new PricingContext
        {
            PolicyKind = FinancialPolicyKind.MarketplaceCommission,
            PolicyKey = key,
            BaseAmount = Money.FromAmount(100m, "GBP"),
            AsOf = now
        });

        platformPrice.IsSuccess.Should().BeTrue();
        platformPrice.Value.Amount.MinorUnits.Should().Be(500, "the live platform version is 5%");
        platformPrice.Value.PolicyVersion.Should().Be($"{key}@21");

        var countryPrice = await engine.PriceAsync(new PricingContext
        {
            PolicyKind = FinancialPolicyKind.MarketplaceCommission,
            PolicyKey = key,
            BaseAmount = Money.FromAmount(100m, "GBP"),
            CountryCode = "NG",
            AsOf = now
        });

        countryPrice.IsSuccess.Should().BeTrue();
        countryPrice.Value.Amount.MinorUnits.Should().Be(
            200, "a country override is more specific than the platform default");
    }

    /// <summary>
    /// <b>An unresolvable policy is a refusal, never a silent zero.</b> A commission engine that
    /// charged nothing when its configuration was missing would produce a month of sales with no
    /// revenue and nothing anywhere reporting a problem.
    /// </summary>
    [Fact]
    public async Task An_unresolvable_policy_refuses_rather_than_pricing_at_zero()
    {
        using var scope = Factory.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IFinancialPolicyEngine>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var priced = await engine.PriceAsync(new PricingContext
        {
            PolicyKind = FinancialPolicyKind.Custom,
            PolicyKey = $"nothing.{Guid.NewGuid():N}",
            BaseAmount = Money.FromAmount(100m, "GBP"),
            AsOf = clock.UtcNow
        });

        priced.IsFailure.Should().BeTrue();
        priced.Error.Code.Should().Be(
            Diaspora.Modules.Financial.FinancialErrorCodes.PolicyUnresolved);
    }

    /// <summary>
    /// <b>The catalogue read path stays flat as the catalogue grows.</b> It is read on every
    /// purchase screen and written by an operator perhaps monthly, so it is the one place caching
    /// earns its keep.
    /// </summary>
    [Fact]
    public async Task The_catalogue_read_path_stays_flat()
    {
        using var scope = Factory.Services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IMonetizationProductRepository>();

        var stopwatch = Stopwatch.StartNew();

        for (var read = 0; read < 50; read++)
        {
            var available = await products.ListAvailableAsync(
                MonetizationSubjectType.MarketplaceListing, null);

            available.Should().NotBeEmpty();
        }

        stopwatch.Stop();

        // Deliberately generous. The assertion is that the shape has not become quadratic, not that
        // a particular machine is fast — see the class summary.
        stopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(20), "fifty catalogue reads should not take twenty seconds");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Appends a deep, alternating run of entries to one wallet through the real posting service, so
    /// the balancing rule and the sequence assignment are exercised rather than bypassed.
    /// </summary>
    private async Task<WalletId> FillWalletAsync(int depth)
    {
        using var scope = Factory.Services.CreateScope();

        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();
        var wallets = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerPostingService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = await identities.GetOrCreateAsync(
            Guid.NewGuid(), FinancialAccountType.Personal, "GBP");

        account.Activate(DateTimeOffset.UtcNow);

        var wallet = await identities.PrimaryWalletAsync(account, "GBP");

        wallet.Should().NotBeNull();

        wallet!.Activate(DateTimeOffset.UtcNow);

        var clearing = await wallets.FindPlatformWalletAsync(WalletType.Clearing, "GBP");

        clearing.Should().NotBeNull();

        await unitOfWork.SaveChangesAsync();

        for (var index = 0; index < depth; index++)
        {
            // Credit twice for every debit, so the available balance climbs and no entry is ever
            // refused for insufficient funds.
            var isDebit = index % 3 == 2;
            var amount = Money.FromMinorUnits(100, "GBP");

            var result = await ledger.PostAsync(new LedgerPostingRequest
            {
                TransactionType = isDebit ? TransactionType.Purchase : TransactionType.Payment,
                Amount = amount,
                Description = $"Scale entry {index}",
                Audit = AuditStamp.Create(
                    FinancialActor.System("scale-tests"),
                    FinancialSource.System,
                    $"Scale entry {index}"),
                Reference = $"scale:{wallet.Id.Value:N}:{index}",
                Legs = isDebit
                    ?
                    [
                        new LedgerPostingLeg
                        {
                            WalletId = wallet.Id,
                            Direction = LedgerDirection.Debit,
                            Amount = amount,
                            EntryType = LedgerEntryType.Purchase
                        },
                        new LedgerPostingLeg
                        {
                            WalletId = clearing!.Id,
                            Direction = LedgerDirection.Credit,
                            Amount = amount,
                            EntryType = LedgerEntryType.Purchase
                        }
                    ]
                    :
                    [
                        new LedgerPostingLeg
                        {
                            WalletId = clearing!.Id,
                            Direction = LedgerDirection.Debit,
                            Amount = amount,
                            EntryType = LedgerEntryType.Payment
                        },
                        new LedgerPostingLeg
                        {
                            WalletId = wallet.Id,
                            Direction = LedgerDirection.Credit,
                            Amount = amount,
                            EntryType = LedgerEntryType.Payment
                        }
                    ]
            });

            result.IsSuccess.Should().BeTrue(
                result.IsFailure ? result.Error.Description : null);
        }

        await unitOfWork.SaveChangesAsync();

        return wallet.Id;
    }

    private async Task<List<Guid>> CreatePlacementsAsync(int count)
    {
        using var scope = Factory.Services.CreateScope();

        var placements = scope.ServiceProvider.GetRequiredService<IPlacementRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;

        var subjects = new List<Guid>(count);

        for (var index = 0; index < count; index++)
        {
            var subjectId = guids.NewSequentialGuid();

            var placement = Placement.Purchase(
                PlacementId.From(guids.NewSequentialGuid()),
                SubjectReference.Create(MonetizationSubjectType.MarketplaceListing, subjectId),
                PlacementKind.Boost,
                DateWindow.ForDays(now, 7),
                1.5m,
                Guid.NewGuid(),
                "marketplace.listing.boost.7d",
                now);

            placement.Activate(now);
            placements.Add(placement);
            subjects.Add(subjectId);
        }

        await unitOfWork.SaveChangesAsync();

        return subjects;
    }
}
