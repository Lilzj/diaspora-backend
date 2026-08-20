namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// One country an employer says it hires into.
/// <para>
/// <see cref="SponsorsVisas"/> is per country because it genuinely varies: an employer may
/// sponsor into one jurisdiction and not another, and a single global flag would make it lie
/// about at least one of them.
/// </para>
/// </summary>
public sealed record HiringRegionRequest
{
    public required string CountryCode { get; init; }

    public bool SponsorsVisas { get; init; }
}
