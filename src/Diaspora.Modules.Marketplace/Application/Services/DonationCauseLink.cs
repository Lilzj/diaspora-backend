using Diaspora.Contracts.Financial.V1.Giving;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Checks a donated item's cause and records the cause's own title on the attribute.
/// <para>
/// Applied wherever attributes are written (create and replace). A listing with no
/// <see cref="DonationTerms.CauseKey"/> attribute passes through untouched.
/// </para>
/// </summary>
public sealed class DonationCauseLink
{
    private readonly IDonationCauseDirectory _causes;

    public DonationCauseLink(IDonationCauseDirectory causes)
    {
        _causes = causes;
    }

    public async Task<Result<IReadOnlyList<ListingAttributeDraft>>> ApplyAsync(
        IReadOnlyList<ListingAttributeDraft> drafts,
        ListingType listingType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        var index = drafts.ToList().FindIndex(
            draft => ListingAttribute.NormalizeKey(draft.Key) == DonationTerms.CauseKey);

        if (index < 0)
        {
            return Result.Success(drafts);
        }

        if (listingType != ListingType.Giveaway
            || !Guid.TryParse(drafts[index].Value, out var causeId))
        {
            return Result.Failure<IReadOnlyList<ListingAttributeDraft>>(MarketplaceErrors.DonationCauseInvalid);
        }

        var cause = await _causes.FindAsync(causeId, cancellationToken);

        if (cause is null || !cause.IsActive || !cause.AcceptsItems)
        {
            return Result.Failure<IReadOnlyList<ListingAttributeDraft>>(MarketplaceErrors.DonationCauseInvalid);
        }

        var linked = drafts.ToList();
        linked[index] = drafts[index] with { Value = cause.CauseId.ToString(), DisplayName = cause.Title };

        return Result.Success<IReadOnlyList<ListingAttributeDraft>>(linked);
    }
}
