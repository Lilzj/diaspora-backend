namespace Diaspora.Contracts.Financial.V1.Giving;

/// <summary>
/// Read access to donation causes for other modules.
/// <para>
/// Added 16 Oct 2026 for the marketplace: a donated item must name the cause it goes to, and the
/// cause belongs to Financial, which implements this. It lives in Diaspora.Contracts so the
/// marketplace (composed before Financial) can ask without referencing the module.
/// </para>
/// </summary>
public interface IDonationCauseDirectory
{
    /// <summary>The cause, or null when there is none with this identifier.</summary>
    Task<DonationCauseSnapshot?> FindAsync(Guid causeId, CancellationToken cancellationToken = default);
}

/// <summary>What another module may know about a donation cause.</summary>
/// <param name="CauseId">The cause's identifier.</param>
/// <param name="Title">Its title as members see it.</param>
/// <param name="IsActive">Whether it is still taking support.</param>
/// <param name="AcceptsItems">Whether it takes goods or supplies, not only money or time.</param>
/// <param name="CountryCode">The country it serves, when it names one.</param>
public sealed record DonationCauseSnapshot(
    Guid CauseId,
    string Title,
    bool IsActive,
    bool AcceptsItems,
    string? CountryCode);
