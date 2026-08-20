using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// How a candidate presents themselves: a one-line headline and an optional longer summary.
/// <para>
/// The headline is the only piece of a candidate's own words an employer sees before deciding
/// whether to open the application, which is why it is a required field on the profile and a
/// validated value object rather than a nullable string.
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

    /// <summary>The single line beside a candidate's name. "Registered nurse, 8 years, ICU."</summary>
    public string Headline { get; private set; } = string.Empty;

    /// <summary>The longer self-description. Optional.</summary>
    public string? Summary { get; private set; }

    public static Result<CandidateHeadline> Create(string? headline, string? summary = null)
    {
        if (string.IsNullOrWhiteSpace(headline))
        {
            return Result.Failure<CandidateHeadline>(JobErrors.HeadlineInvalid);
        }

        var line = string.Join(' ', headline.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (line.Length == 0 || line.Length > JobLimits.MaxHeadline)
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
