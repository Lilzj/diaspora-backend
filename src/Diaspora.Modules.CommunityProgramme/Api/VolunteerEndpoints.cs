using Diaspora.Modules.CommunityProgramme.Application.Volunteering;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityProgramme.Api;

/// <summary>
/// COMMUNITY-LIFE-GAP-009, -010 and -011: the volunteer programme.
/// <para>
/// The literal <c>/volunteer/applications/me</c> and <c>/volunteer/assignments/me</c> are mapped
/// before their parameterised siblings, so a route that would otherwise read "me" as a Guid never
/// gets the chance.
/// </para>
/// </summary>
internal static class VolunteerEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapOpportunities(group);
        MapApplications(group);
        MapAssignments(group);
    }

    private static void MapOpportunities(RouteGroupBuilder group)
    {
        group.MapGet("/volunteer/opportunities", async (
                [FromQuery] string? query,
                [FromQuery] string? cause,
                [FromQuery] Guid? organizationId,
                [FromQuery] string? countryCode,
                [FromQuery] string? cityName,
                [FromQuery] string? skill,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                [FromQuery] string? state,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListOpportunitiesQuery
                        {
                            Query = query,
                            Cause = cause,
                            OrganizationId = organizationId,
                            CountryCode = countryCode,
                            CityName = cityName,
                            Skill = skill,
                            From = from,
                            To = to,
                            State = state,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("ListVolunteerOpportunities")
            .WithSummary("Volunteer opportunities. Drafts are absent for anybody who cannot manage them.");

        group.MapPost("/volunteer/opportunities", async (
                SaveVolunteerOpportunityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateOpportunityCommand
                        {
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("CreateVolunteerOpportunity")
            .WithSummary("Creates a volunteer opportunity. Role-gated, idempotent on the key.");

        group.MapGet("/volunteer/opportunities/{opportunityId:guid}", async (
                Guid opportunityId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetOpportunityQuery { OpportunityId = opportunityId },
                        cancellationToken)))
            .WithName("GetVolunteerOpportunity")
            .WithSummary("One opportunity, with the server's decision on whether the caller may apply.");

        group.MapPut("/volunteer/opportunities/{opportunityId:guid}", async (
                Guid opportunityId,
                SaveVolunteerOpportunityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateOpportunityCommand
                        {
                            OpportunityId = opportunityId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("UpdateVolunteerOpportunity")
            .WithSummary("Rewrites an opportunity. Role-gated.");

        group.MapPost("/volunteer/opportunities/{opportunityId:guid}/{action}", async (
                Guid opportunityId,
                string action,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ActOnOpportunityCommand
                        {
                            OpportunityId = opportunityId,
                            Action = action
                        },
                        cancellationToken)))
            .WithName("ActOnVolunteerOpportunity")
            .WithSummary("Publishes, closes, completes or cancels an opportunity. Role-gated.");

        group.MapGet("/volunteer/shifts", async (
                [FromQuery] Guid opportunityId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListShiftsQuery { OpportunityId = opportunityId },
                        cancellationToken)))
            .WithName("ListVolunteerShifts")
            .WithSummary("The shifts on an opportunity.");
    }

    private static void MapApplications(RouteGroupBuilder group)
    {
        group.MapGet("/volunteer/applications/me", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListMyVolunteerApplicationsQuery(), cancellationToken)))
            .WithName("ListMyVolunteerApplications")
            .WithSummary("Every application the caller has made.");

        group.MapPost("/volunteer/opportunities/{opportunityId:guid}/applications", async (
                Guid opportunityId,
                SubmitVolunteerApplicationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new SubmitVolunteerApplicationCommand
                        {
                            OpportunityId = opportunityId,
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("SubmitVolunteerApplication")
            .WithSummary("Offers to volunteer. A full opportunity waitlists rather than refusing.");

        group.MapGet("/volunteer/opportunities/{opportunityId:guid}/applications", async (
                Guid opportunityId,
                [FromQuery] string? state,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListApplicantsQuery
                        {
                            OpportunityId = opportunityId,
                            State = state,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("ListVolunteerApplicants")
            .WithSummary("The organizer's applicant queue. Role-gated, oldest first.");

        group.MapPost("/volunteer/applications/{applicationId:guid}/withdraw", async (
                Guid applicationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new WithdrawVolunteerApplicationCommand { ApplicationId = applicationId },
                        cancellationToken)))
            .WithName("WithdrawVolunteerApplication")
            .WithSummary("The applicant withdraws.");

        group.MapPost("/volunteer/applications/{applicationId:guid}/decision", async (
                Guid applicationId,
                DecideVolunteerApplicationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideVolunteerApplicationCommand
                        {
                            ApplicationId = applicationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("DecideVolunteerApplication")
            .WithSummary("An organizer's decision about an applicant. Role-gated.");
    }

    private static void MapAssignments(RouteGroupBuilder group)
    {
        group.MapGet("/volunteer/assignments/me", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListMyAssignmentsQuery(), cancellationToken)))
            .WithName("ListMyVolunteerAssignments")
            .WithSummary("Every assignment the caller holds.");

        group.MapPost("/volunteer/assignments", async (
                AssignVolunteerRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new AssignVolunteerCommand { Request = request },
                        cancellationToken)))
            .WithName("AssignVolunteer")
            .WithSummary("Places an approved volunteer on a shift. 409 for an unapproved one.");

        group.MapPost("/volunteer/assignments/{assignmentId:guid}/hours", async (
                Guid assignmentId,
                ConfirmVolunteerHoursRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ConfirmVolunteerHoursCommand
                        {
                            AssignmentId = assignmentId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("ConfirmVolunteerHours")
            .WithSummary("An organizer confirms the hours worked, which issues the certificate.");

        // Mapped after /hours so the literal segment is not caught by {direction}.
        group.MapPost("/volunteer/assignments/{assignmentId:guid}/{direction}", async (
                Guid assignmentId,
                string direction,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CheckVolunteerCommand
                        {
                            AssignmentId = assignmentId,
                            Direction = direction
                        },
                        cancellationToken)))
            .WithName("CheckVolunteer")
            .WithSummary("Check-in or check-out. Records the moment; proves nothing on its own.");

        group.MapGet("/volunteer/hours/me", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetMyVolunteerHoursQuery(), cancellationToken)))
            .WithName("GetMyVolunteerHours")
            .WithSummary("The caller's volunteering record. Confirmed hours only.");

        group.MapGet("/volunteer/certificates/me", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListMyCertificatesQuery(), cancellationToken)))
            .WithName("ListMyVolunteerCertificates")
            .WithSummary("The caller's issued certificates.");
    }
}
