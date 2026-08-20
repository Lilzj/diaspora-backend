using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// What the provider says about itself.
/// <para>
/// Two lengths in one value object, because the client already needs both and currently has
/// one. The provider detail screen renders the full description under "About"; the discovery
/// card renders nothing at all today and would render a two-line summary if it had one, which
/// is why <see cref="Summary"/> exists and is derived when the provider does not supply it.
/// </para>
/// <para>
/// Deriving rather than truncating at render time matters: a client that truncates a
/// four-hundred-character description at a hundred characters cuts mid-word and mid-sentence on
/// every card. Deriving here cuts at a sentence boundary once, server-side, and every client
/// gets the same text.
/// </para>
/// </summary>
public sealed class ProviderDescription : ValueObject
{
    public const int MaxSummaryLength = 280;

    public const int MaxBodyLength = 8_000;

    private ProviderDescription(string body, string summary)
    {
        Body = body;
        Summary = summary;
    }

    private ProviderDescription() { }

    /// <summary>The full text. What the "About" section renders.</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>A card-sized precis. Supplied or derived; never null.</summary>
    public string Summary { get; private set; } = string.Empty;

    public static ProviderDescription Empty => new(string.Empty, string.Empty);

    public static Result<ProviderDescription> Create(string? body, string? summary = null)
    {
        var trimmedBody = body?.Trim() ?? string.Empty;

        if (trimmedBody.Length > MaxBodyLength)
        {
            return Result.Failure<ProviderDescription>(ProviderErrors.DescriptionTooLong);
        }

        var trimmedSummary = summary?.Trim();

        if (trimmedSummary is { Length: > MaxSummaryLength })
        {
            return Result.Failure<ProviderDescription>(ProviderErrors.DescriptionTooLong);
        }

        var resolvedSummary = string.IsNullOrWhiteSpace(trimmedSummary)
            ? Summarize(trimmedBody)
            : trimmedSummary;

        return Result.Success(new ProviderDescription(trimmedBody, resolvedSummary));
    }

    public override string ToString() => Summary;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Body;
        yield return Summary;
    }

    /// <summary>
    /// Cuts at the last sentence end that fits, and failing that at the last word boundary.
    /// A hard character cut is the last resort, not the first.
    /// </summary>
    private static string Summarize(string body)
    {
        if (body.Length <= MaxSummaryLength)
        {
            return body;
        }

        var window = body[..MaxSummaryLength];

        var sentenceEnd = window.LastIndexOfAny(['.', '!', '?', '。']);

        if (sentenceEnd > MaxSummaryLength / 2)
        {
            return window[..(sentenceEnd + 1)].TrimEnd();
        }

        var wordEnd = window.LastIndexOf(' ');

        return wordEnd > 0 ? window[..wordEnd].TrimEnd() + "…" : window;
    }
}
