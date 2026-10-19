using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Contracts.Financial.V1.Giving;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>The donation cause register, as other modules may read it.</summary>
public sealed class DonationCauseDirectory : IDonationCauseDirectory
{
    private readonly IDonationCauseRepository _causes;

    public DonationCauseDirectory(IDonationCauseRepository causes)
    {
        _causes = causes;
    }

    public async Task<DonationCauseSnapshot?> FindAsync(
        Guid causeId, CancellationToken cancellationToken = default)
    {
        var cause = await _causes.FindAsync(new DonationCauseId(causeId), cancellationToken);

        return cause is null
            ? null
            : new DonationCauseSnapshot(
                cause.Id.Value,
                cause.Title,
                cause.IsActive,
                cause.Accepts(ContributionKind.Goods) || cause.Accepts(ContributionKind.Supplies),
                cause.CountryCode);
    }
}
