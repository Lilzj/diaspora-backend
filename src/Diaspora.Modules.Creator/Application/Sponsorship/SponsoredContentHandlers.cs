using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Content.V1;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Extensibility.Sponsorship;
using Diaspora.SharedKernel.Results;
using PaidMoney = Diaspora.Modules.Financial.Contracts.V1.Responses.MoneyResponse;

namespace Diaspora.Modules.Creator.Application.Sponsorship;

/// <summary>
/// The caller's sponsored placements over their own content — <c>GET /creators/me/sponsored</c>.
/// <para>
/// <b>A view over B15, not a record of its own.</b> The facts — what was bought, its state, what was
/// paid, delivery — come from <see cref="ISponsoredContentDirectory"/>; the title comes from the
/// content's own module through <see cref="IContentTitleSource"/>. Nothing here is a figure this
/// module made up: a promotion B15 has no delivery rows for has no <c>performance</c>, and one whose
/// payment has not completed has no <c>amountPaid</c>.
/// </para>
/// <para>
/// <b>Content only.</b> A placement on a member profile, a community or a campaign page is something
/// the member bought, but it is not a piece of their content, so it is not on this list. B15's own
/// <c>/financial/placements/me</c> still shows everything.
/// </para>
/// <para>
/// <b><c>state</c></b> takes the client's three groups — <c>live</c>, <c>pending</c>,
/// <c>finished</c> — or one state name, or <c>all</c>.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record GetMySponsoredContentQuery : IQuery<IReadOnlyList<SponsoredContentItemResponse>>
{
    public string? State { get; init; }
}

public sealed class GetMySponsoredContentQueryHandler
    : IQueryHandler<GetMySponsoredContentQuery, IReadOnlyList<SponsoredContentItemResponse>>
{
    /// <summary>How many of each kind B15 is asked for.</summary>
    private const int Take = 100;

    /// <summary>B15 subject type → the client's content type. Absent means "not content".</summary>
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.Ordinal)
    {
        ["Guide"] = "Guide",
        ["Job"] = "Job",
        ["MarketplaceListing"] = "Marketplace",
        ["HousingListing"] = "Housing",
        ["Provider"] = "Provider",
        ["Event"] = "Event",
        ["Update"] = "Update",
        ["Organization"] = "Organization"
    };

    /// <summary>The client's own state groups, from its sponsored screen.</summary>
    private static readonly Dictionary<string, string[]> Groups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["live"] = ["Active", "Scheduled", "Approved"],
        ["pending"] = ["PendingPayment", "PendingApproval", "Draft"],
        ["finished"] = ["Expired", "Completed", "Cancelled", "Rejected", "Refunded", "Failed"]
    };

    private readonly CreatorContext _context;
    private readonly ISponsoredContentDirectory _directory;
    private readonly IEnumerable<IContentTitleSource> _titles;

    public GetMySponsoredContentQueryHandler(
        CreatorContext context,
        ISponsoredContentDirectory directory,
        IEnumerable<IContentTitleSource> titles)
    {
        _context = context;
        _directory = directory;
        _titles = titles;
    }

    public async Task<Result<IReadOnlyList<SponsoredContentItemResponse>>> Handle(
        GetMySponsoredContentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SponsoredContentItemResponse>>(caller.Error);
        }

        var snapshots = (await _directory.ListForMemberAsync(caller.Value, Take, cancellationToken))
            .Where(snapshot => ContentTypes.ContainsKey(snapshot.SubjectType))
            .Where(snapshot => Matches(snapshot.State, query.State))
            .ToList();

        // One lookup per content type, not per row.
        var titles = new Dictionary<(string Type, Guid Id), string>();

        foreach (var source in _titles)
        {
            var ids = snapshots
                .Where(snapshot => string.Equals(snapshot.SubjectType, source.ContentType, StringComparison.Ordinal))
                .Select(snapshot => snapshot.SubjectId)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                continue;
            }

            foreach (var (id, title) in await source.FindTitlesAsync(ids, cancellationToken))
            {
                titles[(source.ContentType, id)] = title;
            }
        }

        var response = snapshots
            .Select(snapshot =>
            {
                var contentType = ContentTypes[snapshot.SubjectType];

                var title = titles.TryGetValue((snapshot.SubjectType, snapshot.SubjectId), out var known)
                    ? known
                    : snapshot.CampaignTitle ?? contentType;

                return new SponsoredContentItemResponse
                {
                    PlacementId = snapshot.PlacementId,
                    CampaignId = snapshot.CampaignId,
                    ContentId = snapshot.SubjectId,
                    ContentType = contentType,
                    ContentTitle = title,
                    ProductCode = snapshot.ProductCode,
                    PackageLabel = snapshot.PackageLabel,
                    State = snapshot.State,
                    RejectionReason = snapshot.RejectionReason,
                    AmountPaid = snapshot.AmountPaid is { } paid ? Money(paid) : null,
                    StartsAt = snapshot.StartsAt,
                    EndsAt = snapshot.EndsAt,
                    IsRenewable = snapshot.IsRenewable,
                    Performance = snapshot.Performance is { } performance ? Metrics(performance) : null
                };
            })
            .ToList();

        return Result.Success<IReadOnlyList<SponsoredContentItemResponse>>(response);
    }

    private static bool Matches(string state, string? wanted)
    {
        if (string.IsNullOrWhiteSpace(wanted) || string.Equals(wanted, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Groups.TryGetValue(wanted.Trim(), out var members)
            ? members.Contains(state, StringComparer.Ordinal)
            : string.Equals(state, wanted.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static PaidMoney Money(SponsoredAmount amount) => new()
    {
        Amount = amount.MinorUnits / (decimal)Math.Pow(10, amount.Exponent),
        Currency = amount.Currency,
        MinorUnits = amount.MinorUnits,
        Exponent = amount.Exponent
    };

    private static List<SponsoredMetricResponse> Metrics(SponsoredPerformance performance)
    {
        var metrics = new List<SponsoredMetricResponse>
        {
            new()
            {
                Key = "impressions",
                Label = "Impressions",
                Value = performance.Impressions,
                Format = "count",
                HigherIsBetter = true,
                Definition = "Times the promoted slot was shown."
            },
            new()
            {
                Key = "clicks",
                Label = "Opens",
                Value = performance.Clicks,
                Format = "count",
                HigherIsBetter = true,
                Definition = "Times somebody opened the content from the promoted slot."
            }
        };

        // A rate over nothing is not zero, it is undefined — so it is absent rather than 0%.
        if (performance.Impressions > 0)
        {
            metrics.Add(new SponsoredMetricResponse
            {
                Key = "ctr",
                Label = "Open rate",
                Value = Math.Round(performance.Clicks * 100d / performance.Impressions, 1),
                Format = "percentage",
                HigherIsBetter = true,
                Definition = "Opens divided by impressions."
            });
        }

        return metrics;
    }
}
