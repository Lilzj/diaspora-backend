using Diaspora.Modules.Recommendations.Application.Feedback;
using Diaspora.Modules.Recommendations.Application.Signals;
using Diaspora.Modules.Recommendations.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Recommendations.Api;

/// <summary>
/// What the client reports back.
/// <para>
/// <b>Two shapes, and keeping them apart matters.</b> A signal is an observation about the member
/// anywhere in the app; feedback is specifically about a card this module produced. Merging them
/// would make every impression on the platform look like a recommendation impression, and every
/// acceptance rate would be divided by a denominator that included screens this module never touched.
/// </para>
/// <para>
/// <b>The convenience verbs — dismiss, accept, bookmark — exist because they are what a client
/// actually calls.</b> One generic feedback endpoint would work and would put the kind in a body
/// somebody could mistype; a named route is unambiguous in a log and impossible to get wrong.
/// </para>
/// </summary>
internal static class SignalEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/signals", async (
                RecordSignalsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RecordSignalsCommand { Signals = request.Signals }, cancellationToken)))
            .WithName("RecordSignals")
            .WithSummary("Records a batch of observations about the caller.");

        group.MapPost("/feedback", async (
                RecordFeedbackRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RecordFeedbackCommand
                        {
                            Kind = request.Kind,
                            TargetType = request.TargetType,
                            TargetId = request.TargetId,
                            Surface = request.Surface,
                            SetId = request.SetId,
                            Position = request.Position,
                            OccurredAt = request.OccurredAt
                        },
                        cancellationToken)))
            .WithName("RecordRecommendationFeedback")
            .WithSummary("Records what the caller did with a recommendation.");

        // The named verbs. See the class summary.
        MapFeedbackVerb(group, "dismiss", "Dismissed", "Suppresses a recommendation for the caller.");
        MapFeedbackVerb(group, "accept", "Accepted", "Records that the caller acted on a recommendation.");
        MapFeedbackVerb(group, "bookmark", "Bookmarked", "Records that the caller saved a recommendation.");
        MapFeedbackVerb(group, "view", "Viewed", "Records that a recommendation was shown.");
        MapFeedbackVerb(group, "click", "Clicked", "Records that the caller tapped a recommendation.");
    }

    private static void MapFeedbackVerb(
        RouteGroupBuilder group, string route, string kind, string summary) =>
        group.MapPost($"/{route}", async (
                RecordFeedbackRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RecordFeedbackCommand
                        {
                            // The route decides the kind. A body that disagreed would be ambiguous,
                            // so the body's own kind is ignored on these routes rather than checked.
                            Kind = kind,
                            TargetType = request.TargetType,
                            TargetId = request.TargetId,
                            Surface = request.Surface,
                            SetId = request.SetId,
                            Position = request.Position,
                            OccurredAt = request.OccurredAt
                        },
                        cancellationToken)))
            .WithName($"Record{kind}Recommendation")
            .WithSummary(summary);
}
