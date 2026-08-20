using Diaspora.Modules.Guides.Application.Knowledge.Analytics;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The activity a client reports because the server cannot see it.
/// <para>
/// <b>Opening a country page is a request the server observes. Tapping through to a guide is
/// not.</b> The guide read is a request to a different endpoint that carries no memory of where the
/// reader came from — so without these routes the directory could count arrivals and never learn
/// whether any of them led anywhere, which is the one number that says whether it works.
/// </para>
/// <para>
/// <b>Unauthenticated, and no reader identity is accepted.</b> The counters behind these are
/// aggregate totals per country and per category. A directory that recorded which member read about
/// immigration would be holding something it has no business holding, so the contract has nowhere to
/// put one.
/// </para>
/// <para>
/// Four routes rather than one with a kind parameter: a kind a client can send is a kind a client
/// can send wrongly, and the four are the four things worth counting.
/// </para>
/// </summary>
internal static class KnowledgeAnalyticsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapSignal(group, "discovered", KnowledgeAnalyticsSignalKinds.GuideDiscovered,
            "RecordKnowledgeGuideDiscovered",
            "A reader opened a guide from the directory. The click-through.");

        MapSignal(group, "completed", KnowledgeAnalyticsSignalKinds.GuideCompleted,
            "RecordKnowledgeGuideCompleted",
            "A reader reached the end of a guide the directory sent them to.");

        MapSignal(group, "path-started", KnowledgeAnalyticsSignalKinds.LearningPathStarted,
            "RecordKnowledgeLearningPathStarted", "Somebody opened an onboarding route.");

        MapSignal(group, "path-completed", KnowledgeAnalyticsSignalKinds.LearningPathCompleted,
            "RecordKnowledgeLearningPathCompleted", "Somebody finished one.");
    }

    private static void MapSignal(
        RouteGroupBuilder group, string route, string kind, string name, string summary) =>
        group.MapPost($"/countries/{{countryCode}}/activity/{route}", async (
                string countryCode,
                RecordKnowledgeActivityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RecordKnowledgeActivityCommand
                        {
                            CountryCode = countryCode,
                            Kind = kind,
                            Request = request
                        },
                        cancellationToken)))
            .WithName(name)
            .WithSummary(summary);
}
