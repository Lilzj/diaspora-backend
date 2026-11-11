using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>
/// The causes somebody can give to.
/// <para>
/// This is the endpoint that replaces <c>MOCK_CAUSES</c> in the client's donation-causes screen. The
/// mock's shape was the specification, so the swap is a data-source change rather than a redesign.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.GivingRead)]
public sealed record GetDonationCausesQuery : IQuery<IReadOnlyList<DonationCauseResponse>>
{
    public string? CountryCode { get; init; }

    /// <summary>Narrow to causes accepting one kind of support.</summary>
    public string? SupportKind { get; init; }
}
