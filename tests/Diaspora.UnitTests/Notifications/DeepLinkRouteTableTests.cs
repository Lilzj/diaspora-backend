using Diaspora.Modules.Notifications.Infrastructure.DeepLinks;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// The deep-link prefix contract, pinned.
/// <para>
/// This test and <c>src/lib/__tests__/deep-link-contract.test.ts</c> in the mobile repository assert
/// the same literal set. Neither can see the other repository, and that is deliberate: a shared
/// fixture would have to live somewhere, and wherever it lived one side would own it. Two
/// independent copies mean a one-sided edit turns red on the side that made it.
/// </para>
/// <para>
/// <b>What this catches.</b> Before Wave G this table said <c>/jobs</c>, <c>/guides</c>,
/// <c>/providers</c>, <c>/events</c>, <c>/organizations</c> and <c>/trust</c>, while the client's
/// router serves <c>/job/[id]</c>, <c>/guide/[id]</c>, <c>/provider/[id]</c>,
/// <c>/community-life/event/[id]</c>, <c>/community-life/org/[id]</c> and
/// <c>/profile/trust-center</c>. Ten of twenty-one prefixes could not be opened.
/// </para>
/// <para>
/// The failure was invisible from either side. This resolver accepted the routes because they passed
/// its own table; the client refused them on arrival and fell back to the inbox, which is correct
/// degradation and therefore indistinguishable from nothing being wrong. Roughly half of the
/// platform's notification templates pointed into that hole.
/// </para>
/// </summary>
public sealed class DeepLinkRouteTableTests
{
    /// <summary>
    /// The agreed contract, written as a literal.
    /// <para>
    /// Deliberately not derived from <see cref="DeepLinkRouteTable.KnownPrefixes"/> — a derived
    /// expectation passes for any edit, which is the one thing this must not do.
    /// </para>
    /// </summary>
    private static readonly string[] AgreedPrefixes =
    [
        "/notifications",
        "/settings",
        "/profile",
        "/verification",
        "/saved",
        "/search",
        "/explore",
        "/job",
        "/employer",
        "/marketplace",
        "/housing",
        "/provider",
        "/guide",
        "/knowledge",
        "/neighborhood",
        "/country",
        "/update",
        "/contributor",
        "/community",
        "/community-life",
        "/messages",
        "/conversation",
        "/wallet",
        "/monetization",
        "/creator",
        "/automation",
        "/moderation"
    ];

    [Fact]
    public void KnownPrefixes_MatchesTheClientRouterExactly()
    {
        Assert.Equal(
            AgreedPrefixes.OrderBy(prefix => prefix, StringComparer.Ordinal),
            DeepLinkRouteTable.KnownPrefixes.OrderBy(prefix => prefix, StringComparer.Ordinal));
    }

    [Fact]
    public void KnownPrefixes_ContainsNoDuplicates()
    {
        Assert.Equal(
            DeepLinkRouteTable.KnownPrefixes.Count,
            DeepLinkRouteTable.KnownPrefixes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void KnownPrefixes_AreAllRootedPaths()
    {
        Assert.All(
            DeepLinkRouteTable.KnownPrefixes,
            prefix =>
            {
                Assert.StartsWith("/", prefix, StringComparison.Ordinal);
                Assert.DoesNotContain("//", prefix, StringComparison.Ordinal);
                Assert.False(prefix.EndsWith('/'), $"'{prefix}' must not carry a trailing slash.");
            });
    }

    /// <summary>
    /// The six the client could not open, one per notification family that was affected.
    /// </summary>
    [Theory]
    [InlineData("/job/8f2c9e1a-0000-0000-0000-000000000000")]
    [InlineData("/guide/8f2c9e1a-0000-0000-0000-000000000000")]
    [InlineData("/provider/8f2c9e1a-0000-0000-0000-000000000000")]
    [InlineData("/community-life/event/8f2c9e1a-0000-0000-0000-000000000000")]
    [InlineData("/community-life/org/8f2c9e1a-0000-0000-0000-000000000000")]
    [InlineData("/messages")]
    public void Resolver_AcceptsTheRoutesTheClientCanActuallyOpen(string route)
    {
        var resolver = new DefaultDeepLinkResolver();

        Assert.True(resolver.IsKnownRoute(route));
    }

    /// <summary>
    /// A revert to the API's own prefixes must fail here rather than in production.
    /// </summary>
    [Theory]
    [InlineData("/jobs/8f2c")]
    [InlineData("/guides/8f2c")]
    [InlineData("/providers/8f2c")]
    [InlineData("/events/8f2c")]
    [InlineData("/organizations/8f2c")]
    [InlineData("/trust/profile")]
    [InlineData("/learning/paths")]
    public void Resolver_RefusesThePluralApiPrefixes(string route)
    {
        var resolver = new DefaultDeepLinkResolver();

        Assert.False(resolver.IsKnownRoute(route));
    }

    [Theory]
    [InlineData("/jobsearch")]
    [InlineData("/settings-export")]
    [InlineData("https://example.com/job/1")]
    [InlineData("job/1")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolver_RefusesPartialSegmentsAndNonPaths(string? route)
    {
        var resolver = new DefaultDeepLinkResolver();

        Assert.False(resolver.IsKnownRoute(route));
    }
}
