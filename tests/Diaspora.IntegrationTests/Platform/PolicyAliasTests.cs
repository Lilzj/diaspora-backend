using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Authorization.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// B1.4.1's policy rename, checked against the host that registered it rather than against the
/// declarations that describe it.
/// <para>
/// <b>Renaming a policy is safe in exactly one way and unsafe in every other.</b> The constant is
/// renamed and the compiler fixes every reference it can see — but a policy name reached any other
/// way resolves to nothing, and nothing is not an error. ASP.NET Core denies the request. The
/// endpoint answers 403 to a correctly authorised caller and the log says nothing about why, which
/// is the failure mode <c>AuthorizationCatalogValidator</c> exists for and the reason B13's and
/// B14's old names were kept as aliases rather than deleted.
/// </para>
/// <para>
/// <b>These assertions read the composed <c>AuthorizationPolicy</c>, not the definition.</b> The
/// definition is the input; what a request is actually judged against is the object the policy
/// provider hands back, and asserting on that is the only version of "both names authorise
/// identically" that would survive somebody changing how definitions become policies.
/// </para>
/// </summary>
public sealed class PolicyAliasTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly IAuthorizationPolicyProvider _provider;
    private readonly IPolicyCatalog _catalogue;

    public PolicyAliasTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var client = factory.CreateClient();

        _provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        _catalogue = factory.Services.GetRequiredService<IPolicyCatalog>();
    }

    /// <summary>
    /// The six names B13 and B14 shipped before B1.4.1. Written out rather than read from the
    /// catalogue, because a test that asked the catalogue what the old names were would pass on a
    /// platform that had silently forgotten one of them.
    /// </summary>
    public static TheoryData<string, string> Renames => new()
    {
        { "Notifications.Recipient", "notifications.recipient" },
        { "Notifications.TemplateAuthor", "notifications.template-author" },
        { "Notifications.Operator", "notifications.operator" },
        { "Recommendations.Member", "recommendations.member" },
        { "Recommendations.Curator", "recommendations.curator" },
        { "Recommendations.Analyst", "recommendations.analyst" }
    };

    [Theory]
    [MemberData(nameof(Renames))]
    public async Task Both_names_are_registered_with_asp_net_core(string legacy, string canonical)
    {
        (await _provider.GetPolicyAsync(canonical)).Should().NotBeNull(
            "'{0}' is the name the module declares", canonical);

        (await _provider.GetPolicyAsync(legacy)).Should().NotBeNull(
            "'{0}' is what the platform shipped and something may still be asking for it; an " +
            "unregistered policy denies every caller and logs nothing",
            legacy);
    }

    /// <summary>
    /// <b>The assertion the whole mechanism exists for.</b> Same requirements, same authentication
    /// schemes — so a caller who was authorised before the rename is authorised after it, and one
    /// who was not still is not.
    /// </summary>
    [Theory]
    [MemberData(nameof(Renames))]
    public async Task Both_names_authorise_identically(string legacy, string canonical)
    {
        var under = await _provider.GetPolicyAsync(canonical);
        var over = await _provider.GetPolicyAsync(legacy);

        Describe(over!).Should().Equal(
            Describe(under!),
            "an alias that authorised differently would be a privilege change wearing a rename's " +
            "clothes");

        over!.AuthenticationSchemes.Should().BeEquivalentTo(under!.AuthenticationSchemes);
    }

    [Theory]
    [MemberData(nameof(Renames))]
    public void The_catalogue_resolves_both_names_to_one_definition(string legacy, string canonical)
    {
        _catalogue.Find(legacy).Should().BeSameAs(
            _catalogue.Find(canonical),
            "two definitions could drift; one cannot");
    }

    /// <summary>
    /// The rename actually happened. Without this, a platform that had quietly reverted to the
    /// PascalCase names would pass every assertion above — both names would still resolve, because
    /// one of them would be doing both jobs.
    /// </summary>
    [Fact]
    public void No_module_declares_a_pascal_case_policy_any_more()
    {
        _catalogue.All
            .Select(policy => policy.Name)
            .Where(name => !string.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal))
            .Should().BeEmpty("B1.4.1 closed F3; the old names live on as aliases, not as policies");
    }

    /// <summary>
    /// And the aliases are aliases: six extra registered names, not six extra policies. A catalogue
    /// that counted them would double every consumer that walks it.
    /// </summary>
    [Fact]
    public void An_alias_is_not_a_second_policy_in_the_catalogue()
    {
        _catalogue.All
            .Select(policy => policy.Name)
            .Should().NotContain(
                Renames.Select(entry => (string)entry[0]),
                "an alias is a second name for a policy, never a second policy");

        _catalogue.All.SelectMany(policy => policy.Aliases)
            .Should().HaveCount(6, "B13's three and B14's three, and nothing has added a seventh");
    }

    /// <summary>
    /// A requirement set as comparable strings.
    /// <para>
    /// <b>Projected rather than compared structurally.</b> Half the requirements ASP.NET Core builds
    /// carry no public state at all — <c>DenyAnonymousAuthorizationRequirement</c> is a marker — so a
    /// structural comparison either throws for want of members or passes for the same reason. What
    /// distinguishes one permission requirement from another is the permission, and that is what
    /// this reads.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> Describe(AuthorizationPolicy policy) =>
        [.. policy.Requirements
            .Select(requirement => requirement switch
            {
                PermissionRequirement permission => $"permission:{permission.Permission}",
                AccessTierRequirement tier => $"tier:{tier.MinimumTier}:{tier.RequiresExactTier}",
                _ => requirement.GetType().FullName!
            })
            .Order(StringComparer.Ordinal)];
}
