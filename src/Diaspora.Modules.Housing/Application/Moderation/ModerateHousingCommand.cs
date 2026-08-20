using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;

namespace Diaspora.Modules.Housing.Application.Moderation;

/// <summary>Applies a moderation decision to a property.</summary>
[Authorize(Permission = HousingPermissions.Moderate)]
[Auditable]
public sealed record ModerateHousingCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public required ModerateHousingRequest Request { get; init; }
}
