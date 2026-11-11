using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Presence;

/// <summary>Switches "when the app is open" location on or off — 21 Oct 2026.</summary>
[RequirePermission(LocationPermissions.ManageProfile)]
public sealed record SetMyPresenceCommand : ICommand<PresenceResponse>
{
    public required bool Enabled { get; init; }
}
