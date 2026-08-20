namespace Diaspora.Platform.Modules.Seeding;

/// <summary>
/// A module's reference-data catalogue, seeded at startup and re-runnable afterwards.
/// <para>
/// <b>This interface exists because its absence has cost the platform a full test suite more than
/// once.</b> Until PSR-1 the platform's seeders were found by name — a <c>BackgroundService</c>
/// called <c>…Seeder</c> — and invoked by a hand-written list in the integration harness. Both
/// halves failed in the same way: forgetting to add a new module's seeder to the list does not fail
/// one test, it fails <em>every</em> test in the suite at host start, with an error that names
/// neither the seeder nor the module. B8 recorded it, B8.1 recorded it again, and B9, B10, B11 and
/// B8.2 each restated the registration contract from memory.
/// </para>
/// <para>
/// <b>Name-based discovery was never quite right either.</b> Location's
/// <c>LocationCatalogSeedRunner</c> — the platform's first reference-data seeder — is not called
/// <c>…Seeder</c> and is not a <c>BackgroundService</c>, so the validator written to protect
/// seeders had never once looked at it. A marker interface is the only thing that finds a seeder
/// because it <em>is</em> one rather than because it was named like one.
/// </para>
/// <para>
/// <b>Implementations are also hosted services.</b> A seeder runs itself at startup — that is what
/// makes a fresh deployment work — and exposes <see cref="SeedAsync"/> publicly so a host that
/// applies migrations from a separate job can seed again once the schema exists. Register with
/// <see cref="ReferenceDataSeederRegistration.AddReferenceDataSeeder{TSeeder}"/>, which wires both
/// faces to one instance.
/// </para>
/// <para>
/// <b>Seeding must never take the host down.</b> Every implementation swallows its own failures and
/// logs them: a platform whose sign-in is broken because a category list was already present is a
/// worse outcome than an incomplete catalogue. <see cref="ReferenceDataSeedRunner"/> holds the same
/// contract for the same reason.
/// </para>
/// </summary>
public interface IReferenceDataSeeder
{
    /// <summary>
    /// The module that owns this catalogue — the same string the module's
    /// <see cref="IPlatformModule.Name"/> returns.
    /// <para>
    /// Load-bearing rather than decorative: the runner orders seeders by their module's registered
    /// order, so a catalogue lands in the same slot as the module that owns it without anybody
    /// repeating the number.
    /// </para>
    /// </summary>
    string Module { get; }

    /// <summary>
    /// Tie-break <em>within</em> one module, for the rare case of two catalogues where one reads
    /// the other. Lower runs first.
    /// <para>
    /// Deliberately not the module's order. A seeder that repeated its module's order would be a
    /// second copy of a number that already exists, free to drift from it — which is the failure
    /// the architecture suite already has on record for hard-coded module lists.
    /// </para>
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Stable identity, for duplicate detection and for the line an operator reads at three in the
    /// morning. The type name is right for every seeder the platform ships.
    /// </summary>
    string Name => GetType().Name;

    /// <summary>
    /// Seeds the catalogue. <b>Idempotent by contract</b> — it runs at host start, again when a
    /// separately-migrated deployment calls it, and once per integration-test host.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
