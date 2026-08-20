using Diaspora.Modules.Notifications.Application.Templates;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Notifications.Api;

/// <summary>
/// The authoring surface. Staff only.
/// <para>
/// Publishing is its own route rather than a field on the update, because it is its own decision:
/// writing a message the platform will send in its own name and deciding it is ready should be able
/// to be two people, and a boolean on a save cannot express that.
/// </para>
/// </summary>
internal static class NotificationTemplateEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/templates", async (
                [FromQuery] string? search,
                [FromQuery] string? category,
                [FromQuery] string? status,
                [FromQuery] string? language,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListNotificationTemplatesQuery
                        {
                            Search = search,
                            CategoryKey = category,
                            Status = status,
                            Language = language,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListNotificationTemplates")
            .WithSummary("The template catalogue.");

        group.MapGet("/templates/{key}", async (
                string key, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetNotificationTemplateQuery { Key = key }, cancellationToken)))
            .WithName("GetNotificationTemplate")
            .WithSummary("Every version under one template key, newest first.");

        group.MapPost("/templates", async (
                CreateNotificationTemplateRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateNotificationTemplateCommand { Request = request },
                        cancellationToken)))
            .WithName("CreateNotificationTemplate")
            .WithSummary("Creates a draft template.");

        group.MapPut("/templates/{id:guid}", async (
                Guid id,
                UpdateNotificationTemplateRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateNotificationTemplateCommand { TemplateId = id, Request = request },
                        cancellationToken)))
            .WithName("UpdateNotificationTemplate")
            .WithSummary("Edits a draft, or opens the next version of a published template.");

        group.MapPost("/templates/{id:guid}/publish", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishNotificationTemplateCommand { TemplateId = id }, cancellationToken)))
            .WithName("PublishNotificationTemplate")
            .WithSummary("Makes a template usable and retires what it replaces.");

        group.MapPost("/templates/{id:guid}/deactivate", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeactivateNotificationTemplateCommand { TemplateId = id },
                        cancellationToken)))
            .WithName("DeactivateNotificationTemplate")
            .WithSummary("Retires a template. Messages already sent are untouched.");
    }
}
