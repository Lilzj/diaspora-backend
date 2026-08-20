using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>Retrieval and staging for donation causes.</summary>
public interface IDonationCauseRepository
{
    void Add(DonationCause cause);

    void Update(DonationCause cause);

    Task<DonationCause?> FindAsync(
        DonationCauseId causeId, CancellationToken cancellationToken = default);

    Task<DonationCause?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>What the client's donation-causes screen renders.</summary>
    Task<IReadOnlyList<DonationCause>> ListActiveAsync(
        string? countryCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DonationCause>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default);
}
