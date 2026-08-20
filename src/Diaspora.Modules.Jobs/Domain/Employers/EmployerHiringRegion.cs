namespace Diaspora.Modules.Jobs.Domain.Employers;

/// <summary>
/// A country an employer says it hires into.
/// <para>
/// <b>Declared separately from where its postings happen to be, and the difference is the
/// point.</b> An employer with three open roles in Germany may hire across the whole EU, and a
/// candidate in Portugal deciding whether it is worth following them needs the second fact, not
/// the first. It is also what a "which employers hire into my country" browse runs on — a query
/// that would otherwise mean scanning every posting.
/// </para>
/// <para>
/// <see cref="SponsorsVisas"/> is per country because it genuinely varies: an employer may
/// sponsor into one jurisdiction and not another, and a single global flag would make it lie
/// about at least one of them.
/// </para>
/// </summary>
public sealed class EmployerHiringRegion
{
    private EmployerHiringRegion(string countryCode, bool sponsorsVisas, int order)
    {
        CountryCode = countryCode;
        SponsorsVisas = sponsorsVisas;
        Order = order;
    }

    private EmployerHiringRegion() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Whether the employer says it sponsors work visas into this country.</summary>
    public bool SponsorsVisas { get; private set; }

    public int Order { get; private set; }

    /// <summary>Normalises a country code, or null when it is not a valid alpha-2.</summary>
    public static string? NormalizeCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var candidate = countryCode.Trim().ToUpperInvariant();

        return candidate.Length == 2 && candidate.All(char.IsAsciiLetterUpper) ? candidate : null;
    }

    internal static EmployerHiringRegion Create(
        string countryCode, bool sponsorsVisas, int order) =>
        new(countryCode, sponsorsVisas, order);
}
