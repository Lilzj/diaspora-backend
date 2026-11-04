using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// <b>The Creator module sends only routes the app has a screen for</b> (UAT, 3 Nov 2026). Profile
/// completeness, the verification checklist, the challenges and a points rule sent members to
/// <c>/knowledge/create</c>, which the app does not have — its guide writer is <c>/guide/create</c> —
/// and a lesson's "Report something" and an events challenge pointed at two more routes that
/// match no screen. The app now rewrites the three it may still receive from stored rows; this
/// keeps new ones from being written.
/// </summary>
public sealed class CreatorActionRouteArchitectureTests
{
    private static readonly (string Route, string Instead)[] Dead =
    [
        ("\"/knowledge/create\"", "/guide/create"),
        ("\"/community-life/events/create\"", "/community-life/event/create"),
        ("\"/community/report\"", "/safety/trading"),
    ];

    [Fact]
    public void Creator_sends_no_action_route_the_app_has_no_screen_for()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.Contains("Diaspora.Modules.Creator", StringComparison.Ordinal)))
        {
            var text = file.Root.ToFullString();

            offenders.AddRange(Dead
                .Where(entry => text.Contains(entry.Route, StringComparison.Ordinal))
                .Select(entry => $"{file.Path} — {entry.Route}, which is {entry.Instead} in the app"));
        }

        offenders.Should().BeEmpty(
            "an action route is a screen in the app, and these match none:\n" + string.Join("\n", offenders));
    }
}
