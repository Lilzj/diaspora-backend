using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Currencies;

/// <summary>
/// One currency the platform knows how to denominate, format and store.
/// <para>
/// <b>The exponent is the field that earns this table.</b> 2500 minor units is £25.00 in GBP, ¥2500
/// in JPY and 2.500 KWD in Kuwaiti dinar. A platform that assumed two decimal places would be wrong
/// about roughly a quarter of the world's currencies and would be wrong silently, by a factor of a
/// hundred, in the direction of charging too much.
/// </para>
/// <para>
/// <b><see cref="Symbol"/> exists because the client hard-codes two of them.</b>
/// <c>JobCard.tsx</c> maps GBP to a pound sign, MUR to "Rs " and everything else to the bare code —
/// a per-component table that will be copied into the next card and disagree with the first.
/// Serving the symbol from here costs one column and deletes the table.
/// </para>
/// <para>
/// <b>No rate lives here.</b> This is what a currency <em>is</em>, not what it is worth; the
/// platform never converts, and an exchange rate would be the first step towards a stored figure
/// that silently means something else tomorrow.
/// </para>
/// </summary>
public sealed class CurrencyDefinition : AggregateRoot<CurrencyDefinitionId>, IAuditableEntity
{
    private CurrencyDefinition(
        CurrencyDefinitionId id,
        string code,
        string name,
        string symbol,
        int exponent,
        DateTimeOffset createdAt) : base(id)
    {
        Code = code;
        Name = name;
        Symbol = symbol;
        Exponent = exponent;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private CurrencyDefinition() { }

    /// <summary>ISO 4217, upper case. Unique.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>What a client renders in front of an amount.</summary>
    public string Symbol { get; private set; } = string.Empty;

    /// <summary>Minor digits. 2 for GBP, 0 for JPY, 3 for KWD.</summary>
    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>Whether new money may be denominated in it. Existing balances are unaffected.</summary>
    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CurrencyDefinition Define(
        CurrencyDefinitionId id,
        string code,
        string name,
        string symbol,
        int exponent,
        DateTimeOffset createdAt,
        int sortOrder = 100)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = Money.NormalizeCurrency(code)
            ?? throw new ArgumentException(
                $"'{code}' is not an ISO 4217 currency code.", nameof(code));

        if (exponent is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exponent), exponent, "No ISO 4217 currency has more than four minor digits.");
        }

        return new CurrencyDefinition(
            id, normalized, name.Trim(),
            string.IsNullOrWhiteSpace(symbol) ? normalized : symbol.Trim(),
            exponent, createdAt)
        {
            SortOrder = sortOrder
        };
    }

    public void Update(string? name, string? symbol, int sortOrder, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(symbol))
        {
            Symbol = symbol.Trim();
        }

        SortOrder = sortOrder;
        Revision++;
        ModifiedAt = now;
    }

    /// <summary>
    /// Stops new money being denominated in this currency.
    /// <para>
    /// Existing wallets and ledger entries are untouched, and always will be. Deactivating a currency
    /// somebody holds a balance in must never make that balance unreadable.
    /// </para>
    /// </summary>
    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        Revision++;
        ModifiedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        Revision++;
        ModifiedAt = now;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
