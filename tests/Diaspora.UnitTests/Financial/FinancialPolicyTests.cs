using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Versioned financial rules.
/// <para>
/// Versions are rows and never edits, because every ledger entry stamps the version that priced it —
/// and recomputing a historical amount from today's configuration produces a plausible, wrong number
/// with nothing anywhere reporting a problem.
/// </para>
/// </summary>
public class FinancialPolicyTests
{
    [Fact]
    public void A_published_version_is_active_and_carries_a_label()
    {
        var policy = Policy();

        policy.IsActive.Should().BeTrue();
        policy.VersionLabel.Should().Be("marketplace.commission@1");
        policy.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void A_narrower_scope_needs_a_scope_value()
    {
        var publish = () => Policy(scope: PolicyScopeKind.Country, scopeValue: null);

        publish.Should().Throw<DomainException>().WithMessage("*needs a scope value*");
    }

    /// <summary>Product beats country beats module beats platform.</summary>
    [Theory]
    [InlineData(PolicyScopeKind.Platform, 1)]
    [InlineData(PolicyScopeKind.Module, 2)]
    [InlineData(PolicyScopeKind.Country, 3)]
    [InlineData(PolicyScopeKind.Product, 4)]
    public void Specificity_orders_the_scopes(PolicyScopeKind scope, int expected)
    {
        Policy(scope: scope, scopeValue: "x").Specificity.Should().Be(expected);
    }

    [Fact]
    public void A_country_scoped_version_matches_only_that_country()
    {
        var policy = Policy(scope: PolicyScopeKind.Country, scopeValue: "NG");

        policy.Matches("NG", null, null).Should().BeTrue();
        policy.Matches("GB", null, null).Should().BeFalse();
        policy.Matches(null, null, null).Should().BeFalse();
    }

    [Fact]
    public void A_platform_scoped_version_matches_everything()
    {
        Policy().Matches(null, null, null).Should().BeTrue();
        Policy().Matches("NG", "marketplace", "anything").Should().BeTrue();
    }

    [Fact]
    public void Superseding_closes_the_window_and_deactivates_the_version()
    {
        var policy = Policy();
        var supersededAt = FinancialTestData.Now.AddDays(30);

        policy.Supersede(supersededAt);

        policy.IsActive.Should().BeFalse();
        policy.Effective.EndsAt.Should().Be(supersededAt);
        policy.AppliesAt(supersededAt).Should().BeFalse();
    }

    [Fact]
    public void A_version_applies_only_inside_its_window()
    {
        var policy = Policy();

        policy.AppliesAt(FinancialTestData.Now).Should().BeTrue();
        policy.AppliesAt(FinancialTestData.Now.AddDays(-1)).Should().BeFalse();
    }

    /// <summary>
    /// The refund window and the platform-fee floor are guarantees rather than settings; a
    /// deployment publishes a new version instead.
    /// </summary>
    [Fact]
    public void A_system_policy_cannot_be_retired()
    {
        var policy = Policy(isSystem: true);

        var retire = () => policy.Retire(FinancialTestData.Now);

        retire.Should().Throw<DomainException>().WithMessage("*cannot be retired*");
    }

    [Fact]
    public void A_parameter_is_readable_as_a_decimal_and_as_a_whole_number()
    {
        var policy = Policy();

        policy.SetParameter(Guid.NewGuid(), "percentage", "7.5");
        policy.SetParameter(Guid.NewGuid(), "maximum_minor_units", "50000");

        policy.DecimalParameter("percentage").Should().Be(7.5m);
        policy.Int64Parameter("maximum_minor_units").Should().Be(50000);
        policy.DecimalParameter("nothing").Should().BeNull();
    }

    [Fact]
    public void Setting_a_parameter_twice_replaces_it_rather_than_duplicating_it()
    {
        var policy = Policy();

        policy.SetParameter(Guid.NewGuid(), "percentage", "5.0");
        policy.SetParameter(Guid.NewGuid(), "percentage", "7.0");

        policy.Parameters.Should().ContainSingle();
        policy.DecimalParameter("percentage").Should().Be(7.0m);
    }

    private static FinancialPolicy Policy(
        PolicyScopeKind scope = PolicyScopeKind.Platform,
        string? scopeValue = null,
        bool isSystem = false) =>
        FinancialPolicy.Publish(
            FinancialPolicyId.New(),
            FinancialPolicyKeys.MarketplaceCommission,
            FinancialPolicyKind.MarketplaceCommission,
            scope,
            scopeValue,
            version: 1,
            DateWindow.Open(FinancialTestData.Now),
            "What the platform takes.",
            FinancialTestData.Now,
            isSystem);
}
