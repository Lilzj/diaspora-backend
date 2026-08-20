using Diaspora.Modules.Financial.Extensibility.Search;

namespace Diaspora.Modules.Financial.Infrastructure.Search;

/// <summary>
/// The default search projector: it does nothing, deliberately and visibly.
/// <para>
/// <b>Nothing on the write path may depend on a search engine existing.</b> A projector that threw,
/// retried or logged an error on every transaction would make a missing adapter look like a payment
/// defect. A deployment registers a real one and the same documents start arriving somewhere useful;
/// until then the seam is present and inert.
/// </para>
/// <para>
/// This is the ninth such seam on the platform and the ninth such default, which the technical debt
/// ledger records as DEBT-020 — one registration each, all still open.
/// </para>
/// </summary>
public sealed class NoOpFinancialSearchProjector : IFinancialSearchProjector
{
    public Task ProjectAsync(
        FinancialSearchDocument document, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveAsync(string documentId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
