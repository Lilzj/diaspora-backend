using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>Clears the caller's recent-locations list. The selector's "Clear all".</summary>
[RequirePermission(LocationPermissions.ManageProfile)]
public sealed record ClearRecentLocationsCommand : ICommand;
