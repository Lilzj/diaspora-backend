namespace Diaspora.Platform.Operations.Scalability;

/// <summary>How data is copied between regions.</summary>
public enum ReplicationMode
{
    /// <summary>One region. Everything else is a read of the same store.</summary>
    None = 0,

    /// <summary>Copies arrive after the write returns. Cheap, and lossy on a failover.</summary>
    Asynchronous = 1,

    /// <summary>The write returns when the copy is durable. Slower, and lossless on a failover.</summary>
    Synchronous = 2
}

/// <summary>What a region is for.</summary>
public enum RegionRole
{
    /// <summary>Takes writes.</summary>
    Primary = 0,

    /// <summary>Takes reads, and can be promoted.</summary>
    Secondary = 1,

    /// <summary>Holds copies and serves nothing.</summary>
    Passive = 2
}

/// <summary>
/// A region the platform could be deployed in.
/// </summary>
/// <param name="Name">The region's name. Provider-neutral: a deployment maps it to whatever its cloud calls it.</param>
/// <param name="Role">What it does.</param>
/// <param name="Zones">Availability zones inside it.</param>
/// <param name="Countries">
/// The countries this region serves. Diaspora is country-shaped by construction — every vertical is
/// scoped by country — so a region's catchment is a list of countries rather than a distance.
/// </param>
/// <param name="Replication">How its data is kept in step with the primary.</param>
public sealed record RegionDescriptor(
    string Name,
    RegionRole Role,
    IReadOnlyList<string> Zones,
    IReadOnlyList<string> Countries,
    ReplicationMode Replication);

/// <summary>
/// Where this instance is running, and what it knows about the rest.
/// <para>
/// <b>Design only, exactly as the phase brief asks.</b> Nothing here routes a request, replicates a
/// row or performs a failover, and the shipped implementation reports a single region. What it buys is
/// that the region is a value the platform can already read — so a telemetry attribute, a cache key
/// prefix, a lock name and a health report can carry it from now on rather than being retrofitted into
/// each of them later, which is where multi-region migrations actually spend their time.
/// </para>
/// </summary>
public interface IRegionContext
{
    /// <summary>The region this instance is in.</summary>
    string CurrentRegion { get; }

    /// <summary>The availability zone, when the deployment knows it.</summary>
    string? CurrentZone { get; }

    /// <summary>Whether this region takes writes.</summary>
    bool IsPrimary { get; }

    /// <summary>Every configured region.</summary>
    IReadOnlyList<RegionDescriptor> Regions { get; }

    /// <summary>The region that serves a country, or the primary when none is configured for it.</summary>
    RegionDescriptor ResolveForCountry(string countryCode);
}

/// <summary>
/// Chooses between the writer and a read replica.
/// <para>
/// <b>Abstraction only.</b> The shipped router sends everything to the primary, so behaviour is
/// unchanged. What matters is where the decision lives: a query handler must never choose its own
/// connection, because the one thing a replica router has to get right is
/// <see cref="RequiresConsistency"/> — read-your-own-writes. A member who posts and is immediately shown
/// a feed built from a replica that has not caught up sees their own post missing, decides the platform
/// lost it, and posts again.
/// </para>
/// </summary>
public interface IReadReplicaRouter
{
    /// <summary>Whether any replica is configured.</summary>
    bool HasReplicas { get; }

    /// <summary>
    /// Whether the current unit of work must read from the writer.
    /// <para>
    /// True inside a transaction, and true for the rest of a request that has written. Both cases are
    /// read-your-own-writes and neither is optional.
    /// </para>
    /// </summary>
    bool RequiresConsistency { get; }

    /// <summary>
    /// Marks the current scope as having written, so subsequent reads stay on the writer.
    /// </summary>
    void MarkWritten();

    /// <summary>
    /// The connection string a read should use. The writer's when consistency is required or no
    /// replica is configured.
    /// </summary>
    string ResolveReadConnection();
}

/// <summary>
/// Builds a public URL for an asset.
/// <para>
/// <b>The seam a CDN registers into, and the reason it exists now rather than later.</b> B5 established
/// that media travels as a reference and never as a URL — a rule that turned out to be exactly what
/// makes a CDN a configuration change. Every URL the platform emits is built at the edge of a response
/// from a reference, so switching to an edge host is one implementation of this interface.
/// </para>
/// </summary>
public interface IContentDeliveryUrlBuilder
{
    /// <summary>The provider's name, for diagnostics. <c>origin</c> when no CDN is configured.</summary>
    string Provider { get; }

    /// <summary>
    /// A URL for a stored object.
    /// </summary>
    /// <param name="storageKey">The object's key.</param>
    /// <param name="lifetime">
    /// How long the URL should remain valid, for a provider that signs. Ignored by an origin builder,
    /// which is why a caller must not treat a returned URL as unguessable.
    /// </param>
    Uri Build(string storageKey, TimeSpan? lifetime = null);

    /// <summary>
    /// Asks the edge to drop its copy.
    /// <para>
    /// The operation every CDN integration forgets until the first time a member replaces a photo and
    /// the old one keeps appearing for a day.
    /// </para>
    /// </summary>
    Task PurgeAsync(string storageKey, CancellationToken cancellationToken = default);
}
