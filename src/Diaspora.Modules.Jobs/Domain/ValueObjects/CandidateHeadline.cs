using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// How a candidate presents themselves: a one-line headline and an optional longer summary.
/// <para>
/// <b>Both are optional since 14 Sep 2026.</b> The headline used to be the one required field, and
/// that made applying a two-step errand: a signed-in member tapped Apply, was told to "create your
/// profile first", and had to invent a line about themselves before any employer would receive
/// anything. The member already has an account the employer sees them by, so a candidate profile
/// now starts empty when they first apply and the headline is something they may add to stand out.
/// </para>
/// <para>
/// <b>No headline is an empty string, never null.</b> The column has always been required, and
/// making it nullable would be a schema change on a table that loses nothing by keeping it. Read
/// <see cref="IsStated"/> rather than comparing against the empty string, and turn an unstated
/// headline back into a null at the wire, where a client should not have to know the convention.
/// </para>
/// </summary>
public sealed class CandidateHeadline : ValueObject
{
    private CandidateHeadline(string headline, string? summary)
    {
        Headline = headline;
        Summary = summary;
    }

    private CandidateHeadline() { }

    /// <summary>A candidate who has said nothing about themselves yet.</summary>
    public static CandidateHeadline None => new(string.Empty, null);

    /// <summary>
    /// The single line beside a candidate's name. "Registered nurse, 8 years, ICU." Empty when the
    /// candidate has not written one — see <see cref="IsStated"/>.
    /// </summary>
    public string Headline { get; private set; } = string.Empty;

    /// <summary>The longer self-description. Optional.</summary>
    public string? Summary { get; private set; }

    /// <summary>Whether the candidate has written a headline.</summary>
    public bool IsStated => Headline.Length > 0;

    /// <summary>
    /// A headline and summary as a candidate typed them. A blank headline is a valid answer and
    /// means none; only one that is too long is refused.
    /// </summary>
    public static Result<CandidateHeadline> Create(string? headline, string? summary = null)
    {
        var line = string.IsNullOrWhiteSpace(headline)
            ? string.Empty
            : string.Join(' ', headline.Split(
                (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (line.Length > JobLimits.MaxHeadline)
        {
            return Result.Failure<CandidateHeadline>(JobErrors.HeadlineInvalid);
        }

        var body = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();

        if (body is { Length: > JobLimits.MaxCandidateSummary })
        {
            return Result.Failure<CandidateHeadline>(JobErrors.HeadlineInvalid);
        }

        return Result.Success(new CandidateHeadline(line, body));
    }

    public override string ToString() => Headline;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Headline;
        yield return Summary;
    }
}
