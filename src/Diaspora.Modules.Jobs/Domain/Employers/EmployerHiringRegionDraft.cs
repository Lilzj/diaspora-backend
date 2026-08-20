namespace Diaspora.Modules.Jobs.Domain.Employers;

/// <summary>One hiring region as the editor sends it, before the aggregate validates the set.</summary>
public sealed record EmployerHiringRegionDraft
{
    public required string? CountryCode { get; init; }

    public bool SponsorsVisas { get; init; }
}
