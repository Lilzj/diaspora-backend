using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Extensibility.Ranking;
using Diaspora.Modules.Guides.Extensibility.Verification;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The two registries every later module binds to, tested against code that does not exist yet.
/// <para>
/// Both follow the same two rules, for the same reasons. A <b>collision fails construction</b>
/// rather than letting registration order decide the winner: a race resolves differently in
/// production than in test, and the symptom is a browse order or a verification decision nobody
/// can reproduce. And a <b>contributor's failure is isolated</b> rather than propagated: an
/// unreachable fact-checking API must degrade to the next provider, not turn a submission Trust
/// would have handled perfectly well into an outage.
/// </para>
/// </summary>
public class GuideExtensibilityTests
{
    // ── The ranking registry ────────────────────────────────────────────────────────────────

    [Fact]
    public void The_module_ships_the_four_rankings_a_deployment_gets_with_nothing_else_installed()
    {
        var registry = ShippedRankings();

        registry.Keys.Should().BeEquivalentTo(
            new[]
            {
                GuideRankingKeys.Newest,
                GuideRankingKeys.MostHelpful,
                GuideRankingKeys.RecentlyUpdated,
                GuideRankingKeys.Trending
            });

        registry.Contains(GuideRankingKeys.Trending).Should().BeTrue();
        registry.Contains("relevance").Should().BeFalse(
            "a search module contributes that one; B8 must not pretend to own it");
    }

    [Fact]
    public void Two_strategies_claiming_one_key_fail_construction_rather_than_racing()
    {
        var duplicate = () => new GuideRankingStrategyRegistry(
        [
            new NewestGuideRankingStrategy(),
            new StubRankingStrategy(GuideRankingKeys.Newest, "Search")
        ]);

        duplicate.Should().Throw<InvalidOperationException>()
            .WithMessage("*newest*", "the message has to name the key an operator must go and fix");
    }

    [Fact]
    public void A_collision_names_both_claimants_so_the_owner_is_findable()
    {
        var duplicate = () => new GuideRankingStrategyRegistry(
        [
            new StubRankingStrategy(GuideRankingKeys.Trending, "Search"),
            new StubRankingStrategy(GuideRankingKeys.Trending, "Recommendations")
        ]);

        duplicate.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Search").And.Contain("Recommendations");
    }

    [Fact]
    public void Keys_are_matched_without_regard_to_case_because_they_travel_in_query_strings()
    {
        var registry = ShippedRankings();

        registry.Resolve("MOST-HELPFUL").Key.Should().Be(GuideRankingKeys.MostHelpful);
        registry.Contains("Trending").Should().BeTrue();
    }

    [Fact]
    public void A_key_this_deployment_does_not_have_falls_back_rather_than_erroring()
    {
        // A client that saved a ranking key a later deployment removed would otherwise get an
        // error on a browse screen, and a browse screen that errors is worse than one ordered
        // differently from what was asked.
        var registry = ShippedRankings();

        registry.Resolve("relevance").Key.Should().Be(GuideRankingKeys.Newest);
        registry.Resolve(null).Key.Should().Be(GuideRankingKeys.Newest);
        registry.Resolve("   ").Key.Should().Be(GuideRankingKeys.Newest);
    }

    [Fact]
    public void A_contributed_strategy_is_resolvable_the_moment_it_is_registered()
    {
        var registry = new GuideRankingStrategyRegistry(
        [
            new NewestGuideRankingStrategy(),
            new StubRankingStrategy("relevance", "Search")
        ]);

        registry.Resolve("relevance").Module.Should().Be("Search",
            "a new ranking is a new class, and no file in B8 changes when one arrives");
    }

    [Fact]
    public void A_deployment_with_no_default_still_resolves_something()
    {
        var registry = new GuideRankingStrategyRegistry(
        [
            new StubRankingStrategy("relevance", "Search")
        ]);

        registry.Resolve(null).Key.Should().Be("relevance",
            "an empty result from a browse endpoint would look like an outage");
    }

    [Fact]
    public void A_strategy_orders_the_candidates_it_was_given_and_neither_adds_nor_removes_one()
    {
        var older = GuideLifecycleTests.Drafted();
        older.Publish(GuideLifecycleTests.Now).IsSuccess.Should().BeTrue();

        var newer = GuideLifecycleTests.Drafted();
        newer.Publish(GuideLifecycleTests.Now.AddDays(1)).IsSuccess.Should().BeTrue();

        var candidates = new List<Guide> { older, newer };

        var ranked = new NewestGuideRankingStrategy().Rank(candidates, Context());

        ranked.Should().HaveCount(2, "removal is a visibility decision and belongs to the query");
        ranked[0].Should().BeSameAs(newer);
        ranked[1].Should().BeSameAs(older);
    }

    // ── The verification registry ───────────────────────────────────────────────────────────

    [Fact]
    public void A_deployment_with_no_provider_says_so_rather_than_pretending()
    {
        var registry = Verification();

        registry.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public async Task Nothing_deciding_a_claim_is_an_honest_failure_rather_than_a_pending_nobody_holds()
    {
        var outcome = await Verification().SubmitAsync(Request());

        outcome.IsFailure.Should().BeTrue();
        outcome.Error.Should().Be(GuideErrors.VerificationUnavailable,
            "recording a claim as pending with no system holding it produces a verification that " +
            "never resolves and that nobody can chase");
    }

    [Fact]
    public async Task The_first_provider_that_decides_wins()
    {
        var deciding = new StubVerificationProvider("Editorial", order: 10, StubOutcome.Deferred);
        var later = new StubVerificationProvider("Trust", order: 1_000, StubOutcome.Deferred);

        var outcome = await Verification(later, deciding).SubmitAsync(Request());

        outcome.IsSuccess.Should().BeTrue();
        deciding.Calls.Should().Be(1);
        later.Calls.Should().Be(0, "providers run in order and the first decision ends the search");
    }

    [Fact]
    public async Task A_provider_that_throws_is_isolated_and_the_next_one_runs()
    {
        // The whole job of the registry. An unreachable fact-checking API must not fail a
        // submission that Trust would have handled perfectly well.
        var broken = new StubVerificationProvider("FactCheck", order: 10, StubOutcome.Throws);
        var trust = new StubVerificationProvider("Trust", order: 1_000, StubOutcome.Deferred);

        var outcome = await Verification(broken, trust).SubmitAsync(Request());

        outcome.IsSuccess.Should().BeTrue();
        outcome.Value.State.Should().Be(GuideVerificationState.Pending);

        broken.Calls.Should().Be(1);
        trust.Calls.Should().Be(1, "the outage degraded the integration, it did not become one");
    }

    [Fact]
    public async Task A_provider_that_returns_a_failure_also_falls_through()
    {
        var failing = new StubVerificationProvider("FactCheck", order: 10, StubOutcome.Fails);
        var trust = new StubVerificationProvider("Trust", order: 1_000, StubOutcome.Deferred);

        var outcome = await Verification(failing, trust).SubmitAsync(Request());

        outcome.IsSuccess.Should().BeTrue();
        trust.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_provider_with_no_opinion_falls_through_without_being_an_error()
    {
        var abstaining = new StubVerificationProvider("Editorial", order: 10, StubOutcome.Undecided);
        var trust = new StubVerificationProvider("Trust", order: 1_000, StubOutcome.Deferred);

        var outcome = await Verification(abstaining, trust).SubmitAsync(Request());

        outcome.IsSuccess.Should().BeTrue();
        outcome.Value.VerificationId.Should().NotBeNull();
        trust.Calls.Should().Be(1,
            "abstaining and breaking both move on, but only one of them is worth alerting about");
    }

    [Fact]
    public async Task Every_provider_breaking_still_ends_in_an_answer()
    {
        var registry = Verification(
            new StubVerificationProvider("FactCheck", 10, StubOutcome.Throws),
            new StubVerificationProvider("Editorial", 20, StubOutcome.Fails),
            new StubVerificationProvider("Trust", 1_000, StubOutcome.Undecided));

        var outcome = await registry.SubmitAsync(Request());

        outcome.IsFailure.Should().BeTrue();
        outcome.Error.Should().Be(GuideErrors.VerificationUnavailable);
    }

    [Fact]
    public void Two_providers_claiming_one_module_name_fail_construction()
    {
        var duplicate = () => Verification(
            new StubVerificationProvider("Trust", 10, StubOutcome.Deferred),
            new StubVerificationProvider("Trust", 20, StubOutcome.Deferred));

        duplicate.Should().Throw<InvalidOperationException>()
            .WithMessage("*Trust*",
                "a registry with two entries it cannot tell apart in a log is one nobody can debug");
    }

    [Fact]
    public async Task A_cancellation_is_never_swallowed_as_a_provider_outage()
    {
        // A cancelled request is the caller going away, not a broken integration; treating it
        // as one would run every remaining provider for a response nobody will read.
        var cancelling = new StubVerificationProvider("FactCheck", 10, StubOutcome.Cancels);
        var trust = new StubVerificationProvider("Trust", 1_000, StubOutcome.Deferred);

        var registry = Verification(cancelling, trust);

        var submit = async () => await registry.SubmitAsync(Request());

        await submit.Should().ThrowAsync<OperationCanceledException>();

        trust.Calls.Should().Be(0);
    }

    // ── Builders and stubs ──────────────────────────────────────────────────────────────────

    private static GuideRankingStrategyRegistry ShippedRankings() =>
        new(
        [
            new NewestGuideRankingStrategy(),
            new MostHelpfulGuideRankingStrategy(),
            new RecentlyUpdatedGuideRankingStrategy(),
            new TrendingGuideRankingStrategy()
        ]);

    private static GuideVerificationProviderRegistry Verification(
        params IGuideVerificationProvider[] providers) =>
        new(providers, NullLogger<GuideVerificationProviderRegistry>.Instance);

    private static GuideRankingContext Context() => new()
    {
        Filter = new GuideQueryFilter(),
        At = GuideLifecycleTests.Now
    };

    private static GuideVerificationRequest Request() => new()
    {
        GuideId = Guid.NewGuid(),
        SubmittedBy = GuideLifecycleTests.Author.Value,
        AuthorId = GuideLifecycleTests.Author.Value,
        Title = "How to open a UK bank account",
        Slug = "how-to-open-a-uk-bank-account",
        CategoryKey = GuideLifecycleTests.CategoryKey,
        Language = "en",
        CountryCode = "GB",
        Version = 1
    };

    private enum StubOutcome
    {
        Deferred,
        Undecided,
        Fails,
        Throws,
        Cancels
    }

    /// <summary>A ranking that does nothing but claim a key — enough to test the registry.</summary>
    private sealed class StubRankingStrategy : IGuideRankingStrategy
    {
        public StubRankingStrategy(string key, string module)
        {
            Key = key;
            Module = module;
        }

        public string Key { get; }

        public string Module { get; }

        public IReadOnlyList<Guide> Rank(
            IReadOnlyList<Guide> candidates, GuideRankingContext context) => candidates;
    }

    /// <summary>A verification provider that behaves in exactly one interesting way.</summary>
    private sealed class StubVerificationProvider : IGuideVerificationProvider
    {
        private readonly StubOutcome _outcome;

        public StubVerificationProvider(string module, int order, StubOutcome outcome)
        {
            Module = module;
            Order = order;
            _outcome = outcome;
        }

        public string Module { get; }

        public int Order { get; }

        public int Calls { get; private set; }

        public Task<Result<GuideVerificationOutcome>> SubmitAsync(
            GuideVerificationRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;

            return _outcome switch
            {
                StubOutcome.Deferred => Task.FromResult(
                    Result.Success(GuideVerificationOutcome.Deferred(Guid.NewGuid()))),

                StubOutcome.Undecided => Task.FromResult(
                    Result.Success(GuideVerificationOutcome.Undecided)),

                StubOutcome.Fails => Task.FromResult(
                    Result.Failure<GuideVerificationOutcome>(GuideErrors.VerificationRefused)),

                StubOutcome.Cancels => throw new OperationCanceledException(),

                _ => throw new InvalidOperationException("The fact-checking API is unreachable.")
            };
        }
    }
}
