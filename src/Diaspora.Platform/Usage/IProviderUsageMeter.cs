namespace Diaspora.Platform.Usage;

/// <summary>
/// A hard monthly allowance on a paid third-party call, shared by every instance of the API.
/// <para>
/// <b>Why this exists.</b> Some vendors bill per request once a monthly free allowance is spent,
/// and the deployment's budget for them is the free allowance and nothing more. A limit enforced in
/// memory is wrong by a factor of the machine count, and the vendor's own console quota is a
/// per-day number that cannot express "this many this month". So the allowance is one database row
/// per provider, SKU and billing month, and taking a unit is a single guarded upsert: two machines
/// racing for the last unit cannot both get it.
/// </para>
/// <para>
/// <b>A unit is taken before the call, and never given back.</b> A request whose response was lost
/// may still have been billed, so returning its unit would let the meter under-count exactly when
/// it matters. Over-counting by a failed call is the safe direction.
/// </para>
/// </summary>
public interface IProviderUsageMeter
{
    /// <summary>
    /// Takes one unit of the current month's allowance for <paramref name="sku"/>, or refuses.
    /// </summary>
    /// <param name="provider">A stable adapter key, e.g. <c>google</c>. Never a URL or credential.</param>
    /// <param name="sku">The vendor's billing unit, in the adapter's own spelling.</param>
    /// <param name="monthlyAllowance">
    /// Units permitted per billing month. Zero or less refuses without touching the database, which
    /// is how a deployment switches one SKU off while keeping the others.
    /// </param>
    /// <param name="billingTimeZoneId">
    /// The time zone the vendor's billing month is measured in. It matters at the boundary: a meter
    /// that resets at UTC midnight while the vendor's month still has hours to run hands out a fresh
    /// allowance the vendor will bill.
    /// </param>
    Task<bool> TryConsumeAsync(
        string provider,
        string sku,
        int monthlyAllowance,
        string billingTimeZoneId,
        CancellationToken cancellationToken = default);

    /// <summary>Units taken so far this billing month. For diagnostics, never for gating.</summary>
    Task<int> GetUsedAsync(
        string provider,
        string sku,
        string billingTimeZoneId,
        CancellationToken cancellationToken = default);
}
