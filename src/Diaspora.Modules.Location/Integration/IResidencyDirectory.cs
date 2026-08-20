using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// Answers "where has this member lived, and which of it is proven?" for other modules.
/// <para>
/// Two questions with different owners behind one interface. The residence history is
/// Location's own; the verified flag is Trust's decision, projected from Trust's published
/// events and never recomputed here. A consumer gets both without having to know which module
/// each half came from — and without either module having to expose its internals.
/// </para>
/// <para>
/// A Guides module gating "you may write about a country you have lived in" asks this. It
/// does not ask Trust, because the residence claim is not Trust's; it does not ask Location
/// for the claim and Trust for the proof and join them itself, because that join is the same
/// three lines in every consumer and one of them will get it wrong.
/// </para>
/// </summary>
public interface IResidencyDirectory
{
    /// <summary>Every country the member has declared living in, most recent arrival first.</summary>
    Task<IReadOnlyList<ResidencySummary>> GetResidenciesAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether Trust has proven this member's residency in this country, as at now.
    /// <para>
    /// Reads the projection rather than calling Trust. A stale projection can only be stale
    /// in the direction of a decision Trust has already made and this module has not yet
    /// heard — bounded by event delivery, not unbounded.
    /// </para>
    /// </summary>
    Task<bool> IsResidencyVerifiedAsync(
        UserId userId, CountryCode countryCode, CancellationToken cancellationToken = default);

    /// <summary>Residence summaries for several members at once.</summary>
    Task<IReadOnlyDictionary<UserId, IReadOnlyList<ResidencySummary>>> GetResidenciesAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default);
}
