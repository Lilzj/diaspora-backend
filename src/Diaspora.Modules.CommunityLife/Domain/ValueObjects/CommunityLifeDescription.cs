using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// What an event or an organization says about itself.
/// <para>
/// Two fields rather than one, because the two are read in different places and at different
/// lengths. A card renders the summary; a page renders the body. Deriving the summary by truncating
/// the body produces a card that stops mid-sentence, which is what every list on the platform would
/// look like if this were a single column.
/// </para>
/// </summary>
public sealed class CommunityLifeDescription : ValueObject
{
    private CommunityLifeDescription(string summary, string? body)
    {
        Summary = summary;
        Body = body;
    }

    private CommunityLifeDescription() { }

    /// <summary>One or two sentences. Required; it is what every card renders.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>The full text. Optional — plenty of events are exactly as long as their summary.</summary>
    public string? Body { get; private set; }

    public static Result<CommunityLifeDescription> Create(string? summary, string? body = null)
    {
        var trimmedSummary = summary?.Trim();

        if (string.IsNullOrEmpty(trimmedSummary)
            || trimmedSummary.Length > CommunityLifeLimits.MaxSummary)
        {
            return Result.Failure<CommunityLifeDescription>(
                CommunityLifeErrors.DescriptionInvalid);
        }

        var trimmedBody = string.IsNullOrWhiteSpace(body) ? null : body.Trim();

        if (trimmedBody is { Length: > CommunityLifeLimits.MaxDescription })
        {
            return Result.Failure<CommunityLifeDescription>(
                CommunityLifeErrors.DescriptionInvalid);
        }

        return Result.Success(new CommunityLifeDescription(trimmedSummary, trimmedBody));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Summary;
        yield return Body;
    }
}
