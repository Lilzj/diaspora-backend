using Diaspora.Modules.Guides.Extensibility.Intelligence;

namespace Diaspora.Modules.Guides.Infrastructure.Intelligence;

/// <summary>
/// The one source rule that holds in every jurisdiction: an official notice cites something.
/// <para>
/// <b>Shipped because it is the only rule that is not a local policy.</b> What counts as an
/// acceptable domain, which ministries a deployment trusts and whether a printed gazette is
/// sufficient are all questions with different right answers in different places — which is why they
/// belong to contributed validators. But a notice that names no source at all is not an official
/// notice in any jurisdiction; it is a claim, and the platform should not present it under an
/// official badge.
/// </para>
/// <para>
/// <b>Either a URL or a reference number satisfies it, and requiring both would be wrong.</b> A great
/// deal of genuinely official information — a circular pinned to a consulate's noticeboard, a gazette
/// entry, a ministerial statement read on national radio — has a reference and no web page. A rule
/// demanding a URL would make the platform unable to carry it, which in the countries where the
/// platform is most useful is most of it.
/// </para>
/// </summary>
public sealed class CitedSourceValidator : IIntelligenceSourceValidator
{
    public string Module => GuidesModuleMetadata.Name;

    /// <summary>
    /// First, so an editor missing a citation is told that rather than being told about a domain
    /// policy they cannot satisfy without one.
    /// </summary>
    public int Order => 10;

    public Task<SourceValidationVerdict> ValidateAsync(
        SourceValidationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cited = !string.IsNullOrWhiteSpace(request.SourceUrl)
            || !string.IsNullOrWhiteSpace(request.ReferenceNumber);

        return Task.FromResult(cited
            ? SourceValidationVerdict.Accept()
            : SourceValidationVerdict.Reject(
                "An official notice must cite an official source URL or a reference number."));
    }
}
