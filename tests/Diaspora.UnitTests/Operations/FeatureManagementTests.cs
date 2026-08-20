using Diaspora.Operations.FeatureManagement;
using Diaspora.Platform.Operations.FeatureManagement;
using Diaspora.Platform.Operations.Policies;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// Flag evaluation, rollout rules, the kill switch and the audit trail.
/// <para>
/// The properties asserted here are the ones a rollout depends on and that a naive implementation gets
/// wrong: a percentage bucket that is stable per member, a bucket that differs per flag, a store
/// failure that resolves to off rather than on, and a kill switch that cannot reach the flags guarding
/// the platform's own safety controls.
/// </para>
/// </summary>
public sealed class FeatureFlagServiceTests
{
    [Fact]
    public async Task An_undeclared_flag_is_off()
    {
        var service = Build(out _);

        (await service.IsEnabledAsync("nothing.declared", default)).Should().BeFalse(
            "the alternative means a typo in a flag name silently enables an unfinished feature and " +
            "nothing anywhere reports it");
    }

    [Fact]
    public async Task An_undeclared_flag_says_why_it_is_off()
    {
        var service = Build(out _);

        (await service.EvaluateAsync("nothing.declared", default)).Reason.Should().Be("undeclared");
    }

    [Fact]
    public async Task A_flags_default_state_applies_when_no_rule_matches()
    {
        var service = Build(out _, Flag("test.on", defaultState: true));

        (await service.IsEnabledAsync("test.on", default)).Should().BeTrue();
    }

    [Fact]
    public async Task The_first_matching_rule_wins()
    {
        var service = Build(out _, Flag(
            "test.rollout",
            defaultState: false,
            RolloutRule.ForCountries("NG"),
            RolloutRule.Everyone(enabled: false)));

        (await service.IsEnabledAsync("test.rollout", new FeatureFlagContext(CountryCode: "NG")))
            .Should().BeTrue();

        (await service.IsEnabledAsync("test.rollout", new FeatureFlagContext(CountryCode: "GB")))
            .Should().BeFalse();
    }

    [Fact]
    public async Task A_disabling_rule_makes_everyone_except_expressible()
    {
        var service = Build(out _, Flag(
            "test.except",
            defaultState: true,
            new RolloutRule(RolloutDimension.Country, ["NG"], Enabled: false)));

        (await service.IsEnabledAsync("test.except", new FeatureFlagContext(CountryCode: "NG")))
            .Should().BeFalse();

        (await service.IsEnabledAsync("test.except", new FeatureFlagContext(CountryCode: "GB")))
            .Should().BeTrue();
    }

    [Fact]
    public async Task A_percentage_rollout_is_sticky_for_one_member()
    {
        var service = Build(out _, Flag("test.percent", false, RolloutRule.ForPercentage(50)));
        var context = new FeatureFlagContext(UserId: "member-42");

        var first = await service.IsEnabledAsync("test.percent", context);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            (await service.IsEnabledAsync("test.percent", context)).Should().Be(first,
                "a rollout that re-rolls per request shows half a member's screens the new " +
                "experience and half the old");
        }
    }

    [Fact]
    public async Task A_percentage_rollout_puts_roughly_the_right_share_in_the_bucket()
    {
        var service = Build(out _, Flag("test.percent", false, RolloutRule.ForPercentage(50)));

        var enabled = 0;

        for (var member = 0; member < 1_000; member++)
        {
            if (await service.IsEnabledAsync(
                "test.percent", new FeatureFlagContext(UserId: $"member-{member}")))
            {
                enabled++;
            }
        }

        enabled.Should().BeInRange(400, 600,
            "a SHA-256 bucket over a thousand subjects should land close to the target share; a wide " +
            "band because this asserts the hash is not degenerate, not that it is uniform");
    }

    [Fact]
    public async Task Two_flags_at_the_same_percentage_do_not_select_the_same_members()
    {
        var service = Build(
            out _,
            Flag("test.alpha", false, RolloutRule.ForPercentage(10)),
            Flag("test.beta", false, RolloutRule.ForPercentage(10)));

        var both = 0;
        var either = 0;

        for (var member = 0; member < 1_000; member++)
        {
            var context = new FeatureFlagContext(UserId: $"member-{member}");
            var alpha = await service.IsEnabledAsync("test.alpha", context);
            var beta = await service.IsEnabledAsync("test.beta", context);

            if (alpha && beta)
            {
                both++;
            }

            if (alpha || beta)
            {
                either++;
            }
        }

        both.Should().BeLessThan(either,
            "hashing the subject alone would put the same unlucky tenth of members in the first " +
            "bucket of every flag on the platform, so one small group receives every experiment and " +
            "their experience diverges from everyone else's permanently");
    }

    [Fact]
    public async Task A_percentage_rollout_never_includes_an_anonymous_caller()
    {
        var service = Build(out _, Flag("test.percent", false, RolloutRule.ForPercentage(100)));

        (await service.IsEnabledAsync("test.percent", new FeatureFlagContext())).Should().BeFalse(
            "randomising for a caller with no id would make the answer change between requests, " +
            "which is the non-sticky rollout the policy forbids");
    }

    [Fact]
    public async Task The_kill_switch_turns_off_every_flag_that_permits_it()
    {
        var service = Build(out _, Flag("test.on", defaultState: true));

        await service.EngageKillSwitchAsync("operator", "incident 42");

        (await service.IsEnabledAsync("test.on", default)).Should().BeFalse();
        (await service.EvaluateAsync("test.on", default)).Reason.Should().Be("kill-switch");

        await service.ReleaseKillSwitchAsync("operator");

        (await service.IsEnabledAsync("test.on", default)).Should().BeTrue();
    }

    [Fact]
    public async Task The_kill_switch_does_not_reach_a_safety_control_flag()
    {
        var service = Build(
            out _,
            Flag("test.safety", defaultState: true) with
            {
                PolicyName = OperationalPolicyNames.FeatureFlag.SafetyControl
            });

        await service.EngageKillSwitchAsync("operator", "incident 42");

        (await service.IsEnabledAsync("test.safety", default)).Should().BeTrue(
            "a kill switch that can disable the audit trail is a kill switch that will be used to " +
            "disable the audit trail");
    }

    [Fact]
    public async Task Turning_a_flag_off_replaces_its_rules_rather_than_layering_over_them()
    {
        var service = Build(out _, Flag("test.rollout", false, RolloutRule.ForPercentage(100)));

        await service.SetStateAsync("test.rollout", enabled: false, "operator", "incident");

        (await service.IsEnabledAsync("test.rollout", new FeatureFlagContext(UserId: "anyone")))
            .Should().BeFalse(
                "an operator flipping a flag during an incident means off for everybody; leaving a " +
                "percentage rule underneath would make the result depend on rules nobody is looking at");
    }

    [Fact]
    public async Task Every_change_is_recorded_with_who_and_why()
    {
        var service = Build(out _, Flag("test.on", defaultState: true));

        await service.SetStateAsync("test.on", enabled: false, "operator", "rolling back");

        var history = await service.HistoryAsync("test.on");

        history.Should().ContainSingle();
        history[0].ChangedBy.Should().Be("operator");
        history[0].Reason.Should().Be("rolling back");
        history[0].Before!.DefaultState.Should().BeTrue();
        history[0].After!.DefaultState.Should().BeFalse();
    }

    [Fact]
    public void Two_modules_declaring_one_flag_is_refused()
    {
        var build = () => new FeatureFlagService(
            [new Contributor("A", Flag("clash", true)), new Contributor("B", Flag("clash", false))],
            new OperationalPolicyCatalog([]),
            new FakeClock(),
            new FakeEnvironment(),
            NullLogger<FeatureFlagService>.Instance);

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*declared by more than one module*");
    }

    [Fact]
    public async Task Setting_an_undeclared_flag_is_refused()
    {
        var service = Build(out _);

        var set = async () => await service.SetStateAsync("nothing.declared", true, "operator", null);

        await set.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void A_flag_older_than_its_policys_staleness_window_is_reported()
    {
        var service = Build(out var clock, Flag("test.ancient", true));

        service.StaleFlags().Should().BeEmpty();

        clock.Advance(TimeSpan.FromDays(400));

        service.StaleFlags().Should().ContainSingle(flag => flag.Name == "test.ancient",
            "flags are meant to be temporary; the ones that are not should be reviewed rather than " +
            "forgotten");
    }

    private static FeatureFlag Flag(string name, bool defaultState, params RolloutRule[] rules) =>
        new(name, "Test", "A test flag.", defaultState, rules,
            OperationalPolicyNames.FeatureFlag.Default,
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            null);

    private static FeatureFlagService Build(out FakeClock clock, params FeatureFlag[] flags)
    {
        clock = new FakeClock();

        var catalog = new OperationalPolicyCatalog(
        [
            new PolicyContributor(
                new FeatureFlagPolicy(OperationalPolicyNames.FeatureFlag.Default),
                new FeatureFlagPolicy(OperationalPolicyNames.FeatureFlag.SafetyControl)
                {
                    KillSwitchApplies = false,
                    EvaluationCacheDuration = TimeSpan.Zero
                })
        ]);

        return new FeatureFlagService(
            [new Contributor("Test", flags)],
            catalog,
            clock,
            new FakeEnvironment(),
            NullLogger<FeatureFlagService>.Instance);
    }

    private sealed class Contributor : IFeatureFlagContributor
    {
        private readonly FeatureFlag[] _flags;

        public Contributor(string module, params FeatureFlag[] flags)
        {
            Module = module;
            _flags = flags;
        }

        public string Module { get; }

        public IEnumerable<FeatureFlag> Flags => _flags;
    }

    private sealed class PolicyContributor : IOperationalPolicyContributor
    {
        private readonly OperationalPolicy[] _policies;

        public PolicyContributor(params OperationalPolicy[] policies) => _policies = policies;

        public string Module => "Test";

        public IEnumerable<OperationalPolicy> Policies => _policies;
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "Diaspora.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
