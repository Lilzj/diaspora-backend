using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;

namespace Diaspora.Modules.Housing.Application.Moderation;

/// <summary>
/// Reports a property.
/// <para>
/// Granted when restricted, deliberately: reporting a rental scam must survive a restriction, or
/// the restriction silences the person best placed to flag the single most damaging content this
/// platform can carry.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.Report)]
[Transactional(Reason = "Writes the report and may flag the listing in the same unit of work.")]
[Auditable]
public sealed record ReportHousingCommand : ICommand<Guid>
{
    public required Guid ListingId { get; init; }

    public required ReportHousingRequest Request { get; init; }
}
