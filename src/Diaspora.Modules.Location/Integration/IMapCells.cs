using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// How much of the platform is in each cell of a viewport.
/// <para>
/// The counting half of B4's map. The geometry is <see cref="MapGrid"/> and needs nothing
/// registered; this is the part that fans out to every <c>ILocationResourceCounter</c> the
/// deployment loaded, so it is a service and it belongs behind a seam.
/// </para>
/// <para>
/// <b>Takes the viewport as the client sent it.</b> A string of four numbers rather than a parsed
/// box, because the caller's job is to pass a query parameter through and B4's is to decide what
/// an unparseable one means — which it does by failing, not by guessing a default viewport. The
/// level is a string for the same reason.
/// </para>
/// </summary>
public interface IMapCells
{
    /// <summary>
    /// Counts in each cell of the fitted grid, or a failure when the viewport cannot be read.
    /// <para>
    /// <paramref name="from"/> and <paramref name="to"/> narrow to rows in a time window, which is
    /// what separates a heat layer from a cluster map: the clusters show what is there, a layer
    /// shows what happened lately. Both null counts everything.
    /// </para>
    /// </summary>
    Task<Result<MapCellPage>> AggregateAsync(
        string bounds,
        string? level,
        IReadOnlyCollection<string>? resourceTypes,
        string? countryCode,
        double? cellSizeKm,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);
}
