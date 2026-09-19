namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Puts a provider into the featured carousel, or takes it out.
/// <para>
/// <b>Editorial, not earned.</b> Nothing about a provider's own record decides this — not its
/// rating, not its completeness, not whether it paid. Somebody on the platform chose it, and the
/// reason is recorded so the next person can tell a considered choice from a forgotten one.
/// </para>
/// </summary>
public sealed record SetProviderFeaturedRequest
{
    public required bool IsFeatured { get; init; }

    /// <summary>Why this one. Required when featuring; a removal needs no defence.</summary>
    public string? Reason { get; init; }
}
