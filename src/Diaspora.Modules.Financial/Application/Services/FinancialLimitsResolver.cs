using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// What a member may move, resolved from policy rather than from a constant in a handler.
/// <para>
/// <b>Every figure a deposit or withdrawal screen shows comes from here, and none of them is ever
/// accepted from a client.</b> A minimum, a maximum, a daily ceiling and a fee are the four numbers a
/// caller would most like to choose, and the only place they are decided is a versioned policy row.
/// The resolver reads the same catalogue the pricing engine does, at the same specificity ordering —
/// a country override beats the platform default — so a deployment that caps Nigerian deposits
/// differently does it by publishing a policy version rather than by shipping a build.
/// </para>
/// <para>
/// <b>An absent policy means no limit rather than a refusal, and this is the one place that is the
/// right answer.</b> The pricing engine refuses when it cannot resolve, because a price it guessed at
/// zero would give something away. A limit it guessed at zero would stop every member moving any
/// money at all, over a configuration row nobody knew was required. The deposit and withdrawal
/// switches in <c>FinancialOptions</c> are what actually gate the capability; the policy shapes it.
/// </para>
/// </summary>
public sealed class FinancialLimitsResolver
{
    private readonly IFinancialPolicyRepository _policies;
    private readonly IClock _clock;

    public FinancialLimitsResolver(IFinancialPolicyRepository policies, IClock clock)
    {
        _policies = policies;
        _clock = clock;
    }

    /// <summary>Resolved limits for a policy key, in a currency, for a country.</summary>
    public async Task<MovementLimits> ResolveAsync(
        string policyKey,
        string currency,
        string? countryCode,
        int exponent,
        CancellationToken cancellationToken = default)
    {
        var policy = await MostSpecificAsync(policyKey, countryCode, cancellationToken);

        if (policy is null)
        {
            return MovementLimits.Unbounded(currency, exponent);
        }

        return new MovementLimits
        {
            Currency = currency,
            Exponent = exponent,
            Minimum = MoneyOrNull(
                policy.Int64Parameter(FinancialPolicyParameters.MinimumMinorUnits),
                currency,
                exponent),
            Maximum = MoneyOrNull(
                policy.Int64Parameter(FinancialPolicyParameters.MaximumMinorUnits),
                currency,
                exponent),
            DailyMaximum = MoneyOrNull(
                policy.Int64Parameter(FinancialPolicyParameters.DailyMaximumMinorUnits),
                currency,
                exponent),
            DailyCount = policy.Int64Parameter(FinancialPolicyParameters.DailyCount) is { } count
                and > 0
                ? (int)Math.Min(count, int.MaxValue)
                : null,
            FlatFee = MoneyOrNull(
                policy.Int64Parameter(FinancialPolicyParameters.FeeMinorUnits),
                currency,
                exponent),
            FeePercentage = policy.DecimalParameter(FinancialPolicyParameters.FeePercentage),
            PolicyVersion = policy.VersionLabel
        };
    }

    /// <summary>
    /// The most specific active version of a key that applies right now.
    /// <para>
    /// The same ordering the policy engine uses: a country override beats the platform default, and a
    /// later version of the same scope beats an earlier one.
    /// </para>
    /// </summary>
    public async Task<FinancialPolicy?> MostSpecificAsync(
        string policyKey, string? countryCode, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var candidates = await _policies.ListActiveByKeyAsync(policyKey, cancellationToken);

        return candidates
            .Where(policy => policy.AppliesAt(now))
            .Where(policy => policy.Matches(countryCode, moduleName: null, productCode: null))
            .OrderByDescending(policy => policy.Specificity)
            .ThenByDescending(policy => policy.Version)
            .FirstOrDefault();
    }

    private static Money? MoneyOrNull(long? minorUnits, string currency, int exponent) =>
        minorUnits is { } value and > 0 ? Money.FromMinorUnits(value, currency, exponent) : null;
}

/// <summary>
/// What a member may move, and what it costs them.
/// <para>
/// Every figure is nullable, and a null means "the platform states no limit" rather than zero. That
/// distinction is the whole contract: a client renders a stated ceiling and says nothing where there
/// is none, instead of drawing a bar at zero that reads as a refusal.
/// </para>
/// </summary>
public sealed record MovementLimits
{
    public required string Currency { get; init; }

    public required int Exponent { get; init; }

    public Money? Minimum { get; init; }

    public Money? Maximum { get; init; }

    /// <summary>The most that may move in a rolling day, across every request.</summary>
    public Money? DailyMaximum { get; init; }

    /// <summary>How many movements a day, when the deployment caps the count as well as the total.</summary>
    public int? DailyCount { get; init; }

    /// <summary>A fixed charge, applied on top of the percentage.</summary>
    public Money? FlatFee { get; init; }

    /// <summary>A proportion of the amount, as a percentage. 1.5 means one and a half per cent.</summary>
    public decimal? FeePercentage { get; init; }

    /// <summary>Which policy version decided this. Stamped onto whatever it prices.</summary>
    public string? PolicyVersion { get; init; }

    public static MovementLimits Unbounded(string currency, int exponent) =>
        new() { Currency = currency, Exponent = exponent };

    /// <summary>
    /// What the platform keeps on an amount.
    /// <para>
    /// Percentage first, then the flat fee, both rounded on the minor unit — the residual stays with
    /// the member, which is the direction <see cref="Money.MultiplyBy"/> already documents and which
    /// must not be chosen twice.
    /// </para>
    /// </summary>
    public Money FeeOn(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        var fee = Money.Zero(amount.Currency, amount.Exponent);

        if (FeePercentage is { } percentage and > 0)
        {
            fee = fee.Add(amount.MultiplyBy(percentage / 100m));
        }

        if (FlatFee is { } flat && flat.SharesCurrencyWith(amount))
        {
            fee = fee.Add(flat);
        }

        // A fee that swallowed the whole amount would send the member nothing and is refused by the
        // aggregate; clamping below it here keeps the refusal about the amount rather than the fee.
        return fee.MinorUnits >= amount.MinorUnits
            ? Money.FromMinorUnits(
                Math.Max(0, amount.MinorUnits - 1), amount.Currency, amount.Exponent)
            : fee;
    }
}
