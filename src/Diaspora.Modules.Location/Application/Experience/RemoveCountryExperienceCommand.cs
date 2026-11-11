using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Location.Application.Experience;

/// <summary>
/// Withdraws a declared residence.
/// <para>
/// It does not touch the verification. A member who removes a claim Trust has already proven
/// has not un-proven it — Trust decides that, and revoking a verification because someone
/// edited a profile field would be Location making a trust decision.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.ManageExperience)]
public sealed record RemoveCountryExperienceCommand : ICommand
{
    public required string CountryCode { get; init; }
}
