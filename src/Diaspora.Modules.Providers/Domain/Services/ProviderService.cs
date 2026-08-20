using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Services;

/// <summary>
/// One thing a provider offers.
/// <para>
/// The client's <c>services: [{ name, priceRange }]</c>, which its provider screen renders under
/// "Services &amp; Menu" — and which is doing three jobs at once in the mock data: a restaurant's
/// menu items, a braider's styles, and a money-transfer corridor. All three are services, and
/// treating them as one concept is right; what the client's two-field shape cannot carry is a
/// duration, a category, a delivery mode, or a price that is not a range.
/// </para>
/// <para>
/// <see cref="CategoryId"/> is optional and separate from the provider's own category. A
/// restaurant sits under Food &amp; Restaurants; one of its services might sit under Catering.
/// That is what lets a member searching for catering find a restaurant that caters, which a
/// provider-level category alone cannot do.
/// </para>
/// <para>
/// <b>Owned by <see cref="Providers.Provider"/>, not an aggregate of its own.</b> A service has
/// no independent lifecycle, is never referenced from outside its provider, and is always
/// written in the same transaction as the provider that offers it. Making it a root would buy a
/// separate consistency boundary nothing needs and cost a join on every profile read.
/// </para>
/// </summary>
public sealed class ProviderService : Entity<ProviderServiceId>
{
    public const int MaxNameLength = 200;

    public const int MaxDescriptionLength = 2_000;

    private readonly List<ServiceOffering> _offerings = [];

    private ProviderService(ProviderServiceId id, string name, ProviderPricing pricing) : base(id)
    {
        Name = name;
        Pricing = pricing;
    }

    private ProviderService() { }

    /// <summary>"Jollof Rice &amp; Chicken", "Box Braids", "Nigeria Transfer".</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>A category of the service itself, when it differs from the provider's.</summary>
    public ServiceCategoryId? CategoryId { get; private set; }

    public ProviderPricing Pricing { get; private set; } = ProviderPricing.Unspecified;

    /// <summary>Where this particular service happens. May differ per service.</summary>
    public ServiceDeliveryMode DeliveryMode { get; private set; } = ServiceDeliveryMode.OnSite;

    /// <summary>Typical duration in minutes. What a future booking module schedules against.</summary>
    public int? DurationMinutes { get; private set; }

    /// <summary>Whether the provider is currently taking this on. Distinct from being deleted.</summary>
    public bool IsAvailable { get; private set; } = true;

    /// <summary>The provider's own highlight. The client's "Special" or "Chef's choice".</summary>
    public bool IsFeatured { get; private set; }

    /// <summary>A photograph of the dish, the style, the room. A Media reference.</summary>
    public MediaId? ImageMediaId { get; private set; }

    public int Position { get; private set; }

    /// <summary>Time-bounded packagings of this service: promotions, packages, seasonal menus.</summary>
    public IReadOnlyList<ServiceOffering> Offerings => _offerings.AsReadOnly();

    public static Result<ProviderService> Create(
        ProviderServiceId id,
        string? name,
        ProviderPricing? pricing = null,
        string? description = null,
        ServiceCategoryId? categoryId = null,
        ServiceDeliveryMode deliveryMode = ServiceDeliveryMode.OnSite,
        int? durationMinutes = null,
        int position = 0)
    {
        ArgumentNullException.ThrowIfNull(id);

        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName))
        {
            return Result.Failure<ProviderService>(ProviderErrors.ServiceNameEmpty);
        }

        if (trimmedName.Length > MaxNameLength)
        {
            return Result.Failure<ProviderService>(ProviderErrors.ServiceNameTooLong);
        }

        return Result.Success(new ProviderService(id, trimmedName, pricing ?? ProviderPricing.Unspecified)
        {
            Description = Trim(description, MaxDescriptionLength),
            CategoryId = categoryId,
            DeliveryMode = deliveryMode,
            DurationMinutes = durationMinutes is > 0 ? durationMinutes : null,
            Position = position < 0 ? 0 : position
        });
    }

    public Result Update(
        string? name,
        ProviderPricing pricing,
        string? description,
        ServiceCategoryId? categoryId,
        ServiceDeliveryMode deliveryMode,
        int? durationMinutes)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName))
        {
            return Result.Failure(ProviderErrors.ServiceNameEmpty);
        }

        if (trimmedName.Length > MaxNameLength)
        {
            return Result.Failure(ProviderErrors.ServiceNameTooLong);
        }

        Name = trimmedName;
        Pricing = pricing;
        Description = Trim(description, MaxDescriptionLength);
        CategoryId = categoryId;
        DeliveryMode = deliveryMode;
        DurationMinutes = durationMinutes is > 0 ? durationMinutes : null;

        return Result.Success();
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;

    public void SetFeatured(bool isFeatured) => IsFeatured = isFeatured;

    public void SetImage(MediaId? mediaId) => ImageMediaId = mediaId;

    internal void SetPosition(int position) => Position = position < 0 ? 0 : position;

    public Result<ServiceOffering> AddOffering(
        ServiceOfferingId id,
        string? name,
        ProviderPricing pricing,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        string? description = null)
    {
        if (_offerings.Count >= ProviderLimits.MaxOfferingsPerService)
        {
            return Result.Failure<ServiceOffering>(ProviderErrors.OfferingLimitReached);
        }

        var offering = ServiceOffering.Create(id, Id, name, pricing, startsAt, endsAt, description);

        if (offering.IsFailure)
        {
            return offering;
        }

        _offerings.Add(offering.Value);

        return offering;
    }

    public Result RemoveOffering(ServiceOfferingId id)
    {
        var offering = _offerings.SingleOrDefault(candidate => candidate.Id == id);

        if (offering is null)
        {
            return Result.Failure(ProviderErrors.OfferingNotFound);
        }

        _offerings.Remove(offering);

        return Result.Success();
    }

    /// <summary>
    /// The offering that applies right now, if any. A service with a live promotion shows the
    /// promotional price; a service without one shows its own.
    /// </summary>
    public ServiceOffering? ActiveOffering(DateTimeOffset asAt) =>
        _offerings
            .Where(offering => offering.IsActiveAt(asAt))
            .OrderByDescending(offering => offering.StartsAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();

    /// <summary>The price a member actually sees: the live offering's, or the service's own.</summary>
    public ProviderPricing EffectivePricing(DateTimeOffset asAt) =>
        ActiveOffering(asAt)?.Pricing ?? Pricing;

    /// <summary>Case-insensitive comparison key. Two "Box Braids" on one provider is a mistake.</summary>
    public string DedupeKey => Name.ToUpperInvariant();

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
