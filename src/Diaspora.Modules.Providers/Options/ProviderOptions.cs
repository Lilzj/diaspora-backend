namespace Diaspora.Modules.Providers.Options;

/// <summary>
/// The knobs a deployment may turn.
/// <para>
/// <b>Deliberately few.</b> The domain's ceilings — how many services, how many gallery items,
/// how long a review stays editable — are constants in <c>ProviderLimits</c> and not options, for
/// the reason that file records: a bound that can be raised per deployment is a bound the domain
/// cannot rely on. What is here is operational rather than behavioural: how often background
/// work runs, and whether it runs at all.
/// </para>
/// </summary>
public sealed class ProviderOptions
{
    public const string SectionName = "Providers";

    /// <summary>
    /// Whether to seed the service category catalogue at startup.
    /// <para>
    /// Off in a deployment that curates its own catalogue and does not want B7's opinion about
    /// what a diaspora directory contains. Seeding is idempotent — it matches on slug and leaves
    /// existing nodes alone — so leaving it on is safe.
    /// </para>
    /// </summary>
    public bool SeedCategories { get; set; } = true;

    /// <summary>How often the sweeper runs.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Providers examined per sweep pass.</summary>
    public int SweepBatchSize { get; set; } = 200;

    /// <summary>
    /// Whether the sweeper runs at all. Off for a single-purpose instance — an import worker, a
    /// test host — that should not be racing the primary one for the same rows.
    /// </summary>
    public bool EnableSweeper { get; set; } = true;
}
