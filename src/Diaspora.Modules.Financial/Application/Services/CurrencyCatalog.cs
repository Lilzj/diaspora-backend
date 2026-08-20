using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.Modules.Financial.Extensibility.Caching;
using Diaspora.Modules.Financial.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// Resolves a currency's symbol and exponent, cached.
/// <para>
/// <b>One lookup per request rather than one per money value.</b> A statement page carries fifty
/// amounts in one currency; resolving the symbol inside the mapper would be fifty round trips for
/// one string. The whole catalogue is a few dozen rows and is cached whole.
/// </para>
/// <para>
/// <b>An unknown currency answers with the code and the platform default.</b> Refusing to render an
/// amount because its currency is not in a catalogue would make a wallet unreadable for a reason
/// that has nothing to do with the wallet.
/// </para>
/// </summary>
public sealed class CurrencyCatalog
{
    private const string CacheKey = "financial:currencies";

    private readonly ICurrencyRepository _currencies;
    private readonly IFinancialCache _cache;
    private readonly TimeSpan _ttl;

    public CurrencyCatalog(
        ICurrencyRepository currencies, IFinancialCache cache, IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _currencies = currencies;
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(30, options.Value.CatalogCacheSeconds));
    }

    /// <summary>Every active currency, keyed by code.</summary>
    public async Task<IReadOnlyDictionary<string, CurrencyFacts>> AllAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<CurrencyFactsBundle>(CacheKey, cancellationToken);

        if (cached is not null)
        {
            return cached.ByCode;
        }

        var currencies = await _currencies.ListActiveAsync(cancellationToken);

        var byCode = currencies.ToDictionary(
            currency => currency.Code,
            currency => new CurrencyFacts(currency.Code, currency.Symbol, currency.Exponent),
            StringComparer.Ordinal);

        await _cache.SetAsync(CacheKey, new CurrencyFactsBundle(byCode), _ttl, cancellationToken);

        return byCode;
    }

    /// <summary>What to render an amount in this currency with.</summary>
    public async Task<CurrencyFacts> FactsAsync(
        string currency, CancellationToken cancellationToken = default)
    {
        var normalized = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        var all = await AllAsync(cancellationToken);

        return all.TryGetValue(normalized, out var facts)
            ? facts
            : new CurrencyFacts(normalized, normalized, Domain.ValueObjects.Money.DefaultExponent);
    }

    /// <summary>The symbol, or null when the catalogue has nothing to say.</summary>
    public async Task<string?> SymbolAsync(
        string currency, CancellationToken cancellationToken = default) =>
        (await FactsAsync(currency, cancellationToken)).Symbol;

    /// <summary>Whether the platform will denominate new money in this currency.</summary>
    public async Task<bool> IsSupportedAsync(
        string currency, CancellationToken cancellationToken = default)
    {
        var normalized = currency?.Trim().ToUpperInvariant() ?? string.Empty;

        return (await AllAsync(cancellationToken)).ContainsKey(normalized);
    }

    /// <summary>Drops the cached catalogue. Called after a currency changes.</summary>
    public Task InvalidateAsync(CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(CacheKey, cancellationToken);

    /// <summary>What a client needs to render an amount.</summary>
    public sealed record CurrencyFacts(string Code, string Symbol, int Exponent);

    /// <summary>The cached shape. A class because the cache stores reference types.</summary>
    private sealed record CurrencyFactsBundle(IReadOnlyDictionary<string, CurrencyFacts> ByCode);
}
