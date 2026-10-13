using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Catalog;

/// <summary>One choice on the listing form: the value to send, and how to show it.</summary>
public sealed record ListingOptionResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }
}

/// <summary>The furnishing and lease choices a listing may be created with.</summary>
public sealed record ListingOptionsResponse
{
    public required IReadOnlyList<ListingOptionResponse> Furnishing { get; init; }

    public required IReadOnlyList<ListingOptionResponse> LeaseTypes { get; init; }
}

/// <summary>
/// Serves the furnishing and lease choices rather than leaving the app to guess them.
/// <para>
/// Added 13 Oct 2026. The app offered "Partially Furnished" and four lease lengths ("6 Months",
/// "1 Year", "Flexible", "Short-let") of its own; none is a member of <see cref="FurnishedStatus"/>
/// or <see cref="LeaseType"/>, and the create handler parses both leniently, so every one was
/// dropped without a word and the listing went out with no furnishing or lease at all.
/// </para>
/// </summary>
public sealed record GetListingOptionsQuery : IQuery<ListingOptionsResponse>;

public sealed class GetListingOptionsQueryHandler : IQueryHandler<GetListingOptionsQuery, ListingOptionsResponse>
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [nameof(FurnishedStatus.PartlyFurnished)] = "Partly furnished",
        [nameof(FurnishedStatus.FullyEquipped)] = "Fully equipped",
        [nameof(LeaseType.MonthToMonth)] = "Month to month",
        [nameof(LeaseType.FixedTerm)] = "Fixed term",
        [nameof(LeaseType.ShortTerm)] = "Short term",
        [nameof(LeaseType.LongTerm)] = "Long term",
        [nameof(LeaseType.AcademicYear)] = "Academic year",
    };

    public Task<Result<ListingOptionsResponse>> Handle(
        GetListingOptionsQuery request, CancellationToken cancellationToken)
    {
        static ListingOptionResponse Option(string name) => new()
        {
            Key = name,
            Label = Labels.GetValueOrDefault(name, name)
        };

        return Task.FromResult(Result.Success(new ListingOptionsResponse
        {
            Furnishing = Enum.GetNames<FurnishedStatus>().Select(Option).ToList(),
            LeaseTypes = Enum.GetNames<LeaseType>()
                .Where(name => name != nameof(LeaseType.Unspecified))
                .Select(Option)
                .ToList()
        }));
    }
}
