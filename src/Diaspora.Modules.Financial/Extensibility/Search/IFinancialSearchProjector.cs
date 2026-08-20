namespace Diaspora.Modules.Financial.Extensibility.Search;

/// <summary>
/// Publishes financial records to whatever indexes them.
/// <para>
/// Declared with a no-op default, so nothing on the write path depends on a search engine existing.
/// A deployment registers an adapter and the same documents start arriving somewhere useful.
/// </para>
/// </summary>
public interface IFinancialSearchProjector
{
    Task ProjectAsync(FinancialSearchDocument document, CancellationToken cancellationToken = default);

    Task RemoveAsync(string documentId, CancellationToken cancellationToken = default);
}
