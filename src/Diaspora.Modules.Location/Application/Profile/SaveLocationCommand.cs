using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Requests;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>
/// Saves or moves a labelled place. Idempotent on the label, matching the client's
/// <c>Record</c>-keyed store.
/// </summary>
[RequirePermission(LocationPermissions.ManageProfile)]
public sealed record SaveLocationCommand : ICommand<SavedLocationResponse>
{
    public required string Label { get; init; }

    /// <summary>Omit to save wherever the member currently is — the selector's "SAVE ACTIVE".</summary>
    public SetCurrentLocationRequest? Location { get; init; }
}
