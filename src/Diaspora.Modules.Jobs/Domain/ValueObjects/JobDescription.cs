using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// The prose of a posting: the body a candidate reads and the one-line summary a card shows.
/// <para>
/// <b>The summary is derived when the employer does not write one</b>, rather than left empty.
/// A results page of cards with blank subtitles is a results page nobody scans, and asking for
/// a second piece of copy is exactly the field employers skip. The derivation is a plain
/// first-sentence-or-truncation, which is honest: it never claims to summarise, only to preview.
/// </para>
/// </summary>
public sealed class JobDescription : ValueObject
{
    private JobDescription(string body, string summary)
    {
        Body = body;
        Summary = summary;
    }

    private JobDescription() { }

    /// <summary>The full posting text.</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>The single line a card renders. Never empty when a body exists.</summary>
    public string Summary { get; private set; } = string.Empty;

    public static Result<JobDescription> Create(string? body, string? summary = null)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure<JobDescription>(JobErrors.DescriptionInvalid);
        }

        var trimmedBody = body.Trim();

        if (trimmedBody.Length > JobLimits.MaxDescription)
        {
            return Result.Failure<JobDescription>(JobErrors.DescriptionInvalid);
        }

        var trimmedSummary = summary?.Trim();

        if (trimmedSummary is { Length: > JobLimits.MaxSummary })
        {
            return Result.Failure<JobDescription>(JobErrors.SummaryTooLong);
        }

        return Result.Success(new JobDescription(
            trimmedBody,
            string.IsNullOrEmpty(trimmedSummary) ? Derive(trimmedBody) : trimmedSummary));
    }

    public override string ToString() => Summary;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Body;
        yield return Summary;
    }

    /// <summary>
    /// The first sentence, or a clean truncation. Sentence-first because a posting almost
    /// always opens with what the role is, and that is the line worth showing.
    /// </summary>
    private static string Derive(string body)
    {
        var collapsed = string.Join(' ', body.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var terminator = collapsed.IndexOfAny(['.', '!', '?']);

        if (terminator > 0 && terminator < JobLimits.MaxSummary)
        {
            return collapsed[..(terminator + 1)];
        }

        if (collapsed.Length <= JobLimits.MaxSummary)
        {
            return collapsed;
        }

        // Cut on a word boundary and mark the elision, so nothing reads as if it ended.
        var cut = collapsed[..(JobLimits.MaxSummary - 1)];
        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > JobLimits.MaxSummary / 2 ? cut[..lastSpace] : cut).TrimEnd() + "…";
    }
}
