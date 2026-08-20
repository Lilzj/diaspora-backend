using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// What the seller wrote about the item, and the card-sized version of it.
/// <para>
/// <b>The summary is derived when the seller does not supply one, and stored either way.</b>
/// Deriving it at read time would mean every card in a results page re-truncating a twenty
/// thousand character body — and would make the search projection and the published integration
/// event carry a different string from the one the client renders.
/// </para>
/// <para>
/// <b>Nothing here is HTML, and that is the sanitisation policy in one line.</b> The body is
/// plain text as the seller typed it; control characters are stripped on the way in, and the
/// client renders it as text. A marketplace that accepted markup would be one where a listing can
/// carry a link that is not the link it appears to be, which on a page where money changes hands
/// is the single most valuable thing an attacker could plant.
/// </para>
/// </summary>
public sealed class ListingDescription : ValueObject
{
    private ListingDescription(string body, string summary)
    {
        Body = body;
        Summary = summary;
    }

    private ListingDescription() { }

    /// <summary>The full text, plain. Never markup.</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>What a card shows. Supplied or derived, never empty when the body is not.</summary>
    public string Summary { get; private set; } = string.Empty;

    public static Result<ListingDescription> Create(string? body, string? summary = null)
    {
        var cleanBody = Sanitize(body);

        if (cleanBody is null || cleanBody.Length > MarketplaceLimits.MaxDescription)
        {
            return Result.Failure<ListingDescription>(MarketplaceErrors.DescriptionInvalid);
        }

        var cleanSummary = Sanitize(summary);

        if (cleanSummary is { Length: > MarketplaceLimits.MaxSummary })
        {
            return Result.Failure<ListingDescription>(MarketplaceErrors.SummaryTooLong);
        }

        return Result.Success(
            new ListingDescription(cleanBody, cleanSummary ?? Derive(cleanBody)));
    }

    /// <summary>
    /// Trims, collapses runs of blank lines, and strips control characters other than newline
    /// and tab. Returns null for anything that was only whitespace.
    /// </summary>
    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var kept = value.Where(character =>
            !char.IsControl(character) || character is '\n' or '\t' or '\r');

        var cleaned = new string([.. kept])
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();

        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>
    /// The first sentence or so of the body, cut on a word boundary. Cutting mid-word produces
    /// card text that reads as broken rather than as abbreviated.
    /// </summary>
    private static string Derive(string body)
    {
        var single = body.Replace('\n', ' ').Replace('\t', ' ');

        var collapsed = string.Join(
            ' ', single.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (collapsed.Length <= MarketplaceLimits.MaxSummary)
        {
            return collapsed;
        }

        var cut = collapsed[..MarketplaceLimits.MaxSummary];
        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > 0 ? cut[..lastSpace] : cut).TrimEnd() + "…";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Body;
        yield return Summary;
    }
}
