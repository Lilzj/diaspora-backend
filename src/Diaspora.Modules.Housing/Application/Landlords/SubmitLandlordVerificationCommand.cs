using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;

namespace Diaspora.Modules.Housing.Application.Landlords;

/// <summary>
/// Asks Trust to verify the caller landlord profile.
/// <para>
/// Returns the case identifier, never a decision. Trust decides, and the outcome arrives later on
/// its own published event.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.LandlordSubmitVerification)]
[Auditable]
public sealed record SubmitLandlordVerificationCommand : ICommand<Guid>
{
    public required SubmitHousingVerificationRequest Request { get; init; }
}
