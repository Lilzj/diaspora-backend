using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// The guide's short description, in two lengths.
/// <para>
/// <see cref="Summary"/> is what the author wrote; <see cref="Excerpt"/> is what a card
/// renders, derived from the first prose block when the author wrote nothing. Both live here so
/// the derivation happens once, at write time, rather than on every card render — a list of
/// twenty guides must not parse twenty markdown documents to show twenty teasers.
/// </para>
/// </summary>
public sealed class GuideSummary : ValueObject
{
    private GuideSummary(string? summary, string? excerpt)
    {
        Summary = summary;
        Excerpt = excerpt;
    }

    private GuideSummary() { }

    /// <summary>What the author wrote. Null when they wrote nothing.</summary>
    public string? Summary { get; private set; }

    /// <summary>What a card shows. Derived from content when <see cref="Summary"/> is null.</summary>
    public string? Excerpt { get; private set; }

    public static Result<GuideSummary> Create(string? summary, string? excerpt = null)
    {
        var trimmedSummary = Trim(summary);

        if (trimmedSummary is { Length: > GuideLimits.MaxSummary })
        {
            return Result.Failure<GuideSummary>(GuideErrors.SummaryTooLong);
        }

        // Truncate after the fallback, not before it. An author who writes a summary and no
        // excerpt is the normal path, and the summary's ceiling is higher than the excerpt's —
        // so truncating only the supplied excerpt lets the derived one exceed its own column.
        var derivedExcerpt = Trim(excerpt) ?? trimmedSummary;

        if (derivedExcerpt is { Length: > GuideLimits.MaxExcerpt })
        {
            derivedExcerpt = derivedExcerpt[..GuideLimits.MaxExcerpt];
        }

        return Result.Success(new GuideSummary(trimmedSummary, derivedExcerpt));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Summary;
        yield return Excerpt;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
