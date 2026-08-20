using Diaspora.Modules.Guides.Application.Intelligence.Directory;
using Diaspora.Modules.Guides.Application.Intelligence.Discovery;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The public holiday calendar.
/// <para>
/// <b>No cursor, deliberately.</b> A country has ten to thirty holidays in a year, and the two real
/// queries — a year, and what is coming up — both return a set small enough to hand over whole. A
/// cursor over twenty rows is machinery nobody needs and a second ordering nobody can see is wrong.
/// </para>
/// </summary>
internal static class PublicHolidayEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/countries/{countryCode}/holidays", async (
                string countryCode,
                int? year,
                DateTimeOffset? from,
                DateTimeOffset? until,
                string? locale,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListPublicHolidaysQuery
                        {
                            CountryCode = countryCode,
                            Year = year,
                            From = from,
                            Until = until,
                            Locale = locale
                        },
                        cancellationToken)))
            .WithName("ListPublicHolidays")
            .WithSummary(
                "One country's calendar for a year, or what falls inside a range. A range wins " +
                "over a year when both are given.");

        group.MapPost("/holidays", async (
                CreatePublicHolidayRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreatePublicHolidayCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageDirectory)
            .WithName("CreatePublicHoliday")
            .WithSummary("Add a day. The closure flags are what change somebody's week.");

        group.MapPut("/holidays/{holidayId:guid}", async (
                Guid holidayId,
                UpdatePublicHolidayRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdatePublicHolidayCommand
                        {
                            PublicHolidayId = holidayId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageDirectory)
            .WithName("UpdatePublicHoliday")
            .WithSummary("Rewrite a calendar entry. The country cannot change.");

        group.MapDelete("/holidays/{holidayId:guid}", async (
                Guid holidayId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchivePublicHolidayCommand { PublicHolidayId = holidayId },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageDirectory)
            .WithName("ArchivePublicHoliday")
            .WithSummary("Remove a calendar entry. A soft delete; restorable.");

        group.MapPost("/holidays/{holidayId:guid}/restore", async (
                Guid holidayId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestorePublicHolidayCommand { PublicHolidayId = holidayId },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageDirectory)
            .WithName("RestorePublicHoliday")
            .WithSummary("Return an archived calendar entry, exactly as it was.");
    }
}
