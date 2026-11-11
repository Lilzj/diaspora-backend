using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Presence;

/// <summary>
/// One foreground location report — 21 Oct 2026. Resolved to an area and discarded; see
/// <see cref="ReportPresenceCommandHandler"/>.
/// </summary>
[RequirePermission(LocationPermissions.ManageProfile)]
public sealed record ReportPresenceCommand : ICommand<PresencePingResponse>
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public double? AccuracyMetres { get; init; }
}
