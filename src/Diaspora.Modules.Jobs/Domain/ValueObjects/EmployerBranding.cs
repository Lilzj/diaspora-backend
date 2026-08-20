using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// How an employer looks on a card and a profile page.
/// <para>
/// <b>Two media references, not an ordered collection.</b> An employer has one logo and one
/// banner; modelling it as a list would let it have four logos and force every read path to
/// decide which one wins. Workplace photographs, which genuinely are a list, hang off the
/// posting instead.
/// </para>
/// <para>
/// References and never URLs — ADR-005. B5 owns the bytes, the variants and the signed URL, and
/// a stored URL would break on the next storage migration while a reference survives it.
/// </para>
/// </summary>
public sealed class EmployerBranding : ValueObject
{
    private EmployerBranding(Guid? logoMediaId, Guid? coverMediaId, string? tagline)
    {
        LogoMediaId = logoMediaId;
        CoverMediaId = coverMediaId;
        Tagline = tagline;
    }

    private EmployerBranding() { }

    public Guid? LogoMediaId { get; private set; }

    public Guid? CoverMediaId { get; private set; }

    /// <summary>The one line under the name. "Care that feels like family."</summary>
    public string? Tagline { get; private set; }

    /// <summary>Nothing set. What a new employer starts with, and a card renders an initial.</summary>
    // Fresh per call: a shared owned instance is tracked against one owner only. See SalaryRange.
    public static EmployerBranding Empty => new(null, null, null);

    public static Result<EmployerBranding> Create(
        Guid? logoMediaId, Guid? coverMediaId, string? tagline)
    {
        var trimmed = string.IsNullOrWhiteSpace(tagline) ? null : tagline.Trim();

        if (trimmed is { Length: > JobLimits.MaxDisplayName })
        {
            return Result.Failure<EmployerBranding>(JobErrors.EmployerNameInvalid);
        }

        return Result.Success(new EmployerBranding(
            logoMediaId == Guid.Empty ? null : logoMediaId,
            coverMediaId == Guid.Empty ? null : coverMediaId,
            trimmed));
    }

    /// <summary>The same branding with a different logo. The attach path's mutator.</summary>
    public EmployerBranding WithLogo(Guid? logoMediaId) =>
        new(logoMediaId == Guid.Empty ? null : logoMediaId, CoverMediaId, Tagline);

    /// <summary>The same branding with a different banner.</summary>
    public EmployerBranding WithCover(Guid? coverMediaId) =>
        new(LogoMediaId, coverMediaId == Guid.Empty ? null : coverMediaId, Tagline);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LogoMediaId;
        yield return CoverMediaId;
        yield return Tagline;
    }
}
