namespace Diaspora.Contracts.Content.V1;

/// <summary>
/// One kind of platform inventory, counted by the module that owns it.
/// <para>
/// <b>Why a seam rather than a projection.</b> FRONTEND-PHASE15-GAP-001 asks for platform-wide totals
/// as a time series. Analytics composes after every vertical and references none of them, and its own
/// event store only knows what happened since analytics shipped and only for ninety days — so a
/// "how many guides exist" answered from it would be a count of recent publications under a name
/// that says it is the whole platform. The owning module answers from its own table instead, through
/// this interface, and Analytics resolves every registered implementation.
/// </para>
/// <para>
/// <b>Counts only.</b> No identifier, title or owner crosses this boundary: the platform overview is
/// open to every signed-in member, and aggregate counts are the only thing it may carry.
/// </para>
/// </summary>
public interface IPlatformInventorySource
{
    /// <summary>
    /// The inventory key — <c>guides</c>, <c>jobs</c>, <c>listings</c>, <c>housing</c>,
    /// <c>providers</c> or <c>members</c>. Stable; the analytics metric keys are built from it.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// The current total, and when each item that first appeared inside the window appeared.
    /// <para>
    /// <paramref name="from"/> is inclusive and <paramref name="to"/> exclusive, both UTC. Instants
    /// rather than per-day counts, because the day boundary belongs to the caller's reporting zone
    /// and a day cut in UTC here would disagree with every other analytics figure.
    /// </para>
    /// </summary>
    Task<PlatformInventoryCount> CountAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>
/// What one inventory source reports.
/// </summary>
/// <param name="CurrentTotal">Items live on the platform now, by the owning module's own definition.</param>
/// <param name="AddedAt">
/// The first-publication (or, for members, registration) instant of every item that first appeared
/// inside the window and still exists. Deleted items are not counted, here or in the total.
/// </param>
public sealed record PlatformInventoryCount(long CurrentTotal, IReadOnlyList<DateTimeOffset> AddedAt);

/// <summary>
/// Resolves the titles of one module's content by identifier.
/// <para>
/// For screens that show a member their own things by reference — a sponsored placement records
/// which guide it promotes, never what the guide is called. The owning module answers; a missing
/// identifier (deleted, or never existed) is simply absent from the result.
/// </para>
/// </summary>
public interface IContentTitleSource
{
    /// <summary>
    /// The content type, in the monetization subject vocabulary: <c>Guide</c>, <c>Job</c>,
    /// <c>MarketplaceListing</c>, <c>HousingListing</c> or <c>Provider</c>.
    /// </summary>
    string ContentType { get; }

    Task<IReadOnlyDictionary<Guid, string>> FindTitlesAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default);
}
