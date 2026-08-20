using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// What the landlord says about the property.
/// <para>
/// <b>A required summary and an optional body, rather than one blob.</b> The summary is what a
/// card, a search result, a nearby panel and a notification all render, and deriving it by
/// truncating the body gives every one of those a sentence cut in half. Requiring it costs the
/// landlord one line and gives every downstream surface something written for it.
/// </para>
/// </summary>
public sealed class HousingDescription : ValueObject
{
    private HousingDescription(string summary, string? body)
    {
        Summary = summary;
        Body = body;
    }

    private HousingDescription() { }

    /// <summary>The one-paragraph pitch. Required, and what every card renders.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>The full description. Optional.</summary>
    public string? Body { get; private set; }

    public static Result<HousingDescription> Create(string? summary, string? body = null)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return Result.Failure<HousingDescription>(HousingErrors.DescriptionInvalid);
        }

        var trimmedSummary = summary.Trim();

        if (trimmedSummary.Length > HousingLimits.MaxSummary)
        {
            return Result.Failure<HousingDescription>(HousingErrors.SummaryTooLong);
        }

        var trimmedBody = string.IsNullOrWhiteSpace(body) ? null : body.Trim();

        if (trimmedBody is { Length: > HousingLimits.MaxDescription })
        {
            return Result.Failure<HousingDescription>(HousingErrors.DescriptionInvalid);
        }

        return Result.Success(new HousingDescription(trimmedSummary, trimmedBody));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Summary;
        yield return Body;
    }
}
