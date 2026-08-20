using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>Removes a labelled place.</summary>
[Authorize(Permission = LocationPermissions.ManageProfile)]
public sealed record RemoveSavedLocationCommand : ICommand
{
    public required string Label { get; init; }
}
