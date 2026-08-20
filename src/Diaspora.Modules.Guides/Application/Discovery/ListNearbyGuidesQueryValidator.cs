using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Rejects a nearby request whose geography could not mean anything.
/// <para>
/// Coordinates outside the earth and a page size below one are client bugs worth naming. The
/// radius is deliberately <em>not</em> range-checked beyond its sentinels: what a kilometre count
/// means, and which values are allowed, is B4's contract, and a second opinion here would drift
/// from it the first time B4 changed.
/// </para>
/// </summary>
public sealed class ListNearbyGuidesQueryValidator : AbstractValidator<ListNearbyGuidesQuery>
{
    /// <summary>B4's sentinels: <c>-1</c> is "this city", <c>-2</c> is "this country".</summary>
    private const double SmallestSentinel = -2d;

    public ListNearbyGuidesQueryValidator()
    {
        RuleFor(query => query.Request).NotNull();

        When(query => query.Request is not null, () =>
        {
            RuleFor(query => query.Request.Latitude)
                .InclusiveBetween(-90d, 90d)
                .When(query => query.Request.Latitude is not null);

            RuleFor(query => query.Request.Longitude)
                .InclusiveBetween(-180d, 180d)
                .When(query => query.Request.Longitude is not null);

            RuleFor(query => query.Request.RadiusKm)
                .GreaterThanOrEqualTo(SmallestSentinel)
                .When(query => query.Request.RadiusKm is not null);

            RuleFor(query => query.Request.CountryCode)
                .Length(2)
                .When(query => !string.IsNullOrWhiteSpace(query.Request.CountryCode));

            RuleFor(query => query.Request.CityName).MaximumLength(GuideLimits.MaxLocationName);

            RuleFor(query => query.Request.Category).MaximumLength(GuideLimits.MaxCategoryKey);

            RuleFor(query => query.Request.Limit)
                .InclusiveBetween(1, GuideLimits.MaxPageSize)
                .When(query => query.Request.Limit is not null);
        });
    }
}
