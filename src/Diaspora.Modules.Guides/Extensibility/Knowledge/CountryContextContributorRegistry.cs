using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// Collects the contextual bands every registered contributor offers for one country.
/// <para>
/// <b>One failing vertical must not take a country page down.</b> A dashboard is a row of
/// independent bands, and a contributor that throws — because its module is mid-deploy, because its
/// own store is briefly unreachable — costs the reader one band and nothing else. The failure is
/// logged at error; the page renders.
/// </para>
/// <para>
/// Contributors run sequentially, for the same reason the section registry does: they share the
/// request's <c>DbContext</c>, and EF Core permits one operation on a context at a time.
/// </para>
/// </summary>
public sealed class CountryContextContributorRegistry
{
    private readonly IReadOnlyList<ICountryContextContributor> _contributors;
    private readonly ILogger<CountryContextContributorRegistry> _logger;

    public CountryContextContributorRegistry(
        IEnumerable<ICountryContextContributor> contributors,
        ILogger<CountryContextContributorRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        _logger = logger;

        _contributors =
        [
            .. contributors
                .OrderBy(contributor => contributor.Order)
                .ThenBy(contributor => contributor.Module, StringComparer.Ordinal)
        ];
    }

    /// <summary>The modules contributing bands. Reported by the capabilities endpoint.</summary>
    public IReadOnlyList<string> Modules =>
        [.. _contributors.Select(contributor => contributor.Module).Distinct(StringComparer.Ordinal)];

    public async Task<IReadOnlyList<CountryContextBlock>> CollectAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var blocks = new List<CountryContextBlock>();

        foreach (var contributor in _contributors)
        {
            blocks.AddRange(await CollectOneAsync(contributor, countryCode, cancellationToken));
        }

        return
        [
            .. blocks
                .Where(block => block.Count > 0)
                .OrderBy(block => block.Order)
                .ThenBy(block => block.Kind, StringComparer.Ordinal)
        ];
    }

    private async Task<IReadOnlyList<CountryContextBlock>> CollectOneAsync(
        ICountryContextContributor contributor,
        string countryCode,
        CancellationToken cancellationToken)
    {
        try
        {
            return await contributor.GetBlocksAsync(countryCode, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // One failing vertical must not fail the dashboard.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Country context contributor {Module} failed for {CountryCode}.",
                contributor.Module,
                countryCode);

            return [];
        }
#pragma warning restore CA1031
    }
}
